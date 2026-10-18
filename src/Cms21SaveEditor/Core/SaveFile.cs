using System;
using System.Collections.Generic;
using System.Text;

namespace Cms21SaveEditor.Core
{
    /// <summary>
    /// CMS21 save-file binary codec. Direct port of the original codec.js / decode.py.
    /// All binary I/O is little-endian, with IEEE-754 floats and length-prefixed
    /// UTF-8 strings (1-byte length + data). Dynamic offsets are located by
    /// byte-pattern signatures rather than hard-coded positions.
    /// </summary>
    public static class SaveFile
    {
        public const string Magic = "PJOOOTER";

        const int _STANDARD_SIZE = 47;
        const int _BODY_SIZE = 63;
        const int _RIM_SIZE = 64;

        static readonly string[] _BODY_PART_PREFIXES = {
            "car_","window","mirror","door","trunk","hood","taillight","bumper","fender" };
        static readonly string[] _RIM_PREFIXES = { "rim_","tire_","wheel_" };

        public static readonly string[] GarageNames = {
            "paintshop","scraps","dyno","warehouse","path_test","car_wash",
            "unlock_tablet","unlock_obd","unlock_fuel","unlock_electronic",
            "garage_upgrade","garage_customization","lifter","unlock_cylinder",
            "unlock_tires","brake_lathe","repair_parts","welder","battery",
            "crane","repair_body","bus_upgrade","windowtint",
        };

        // Byte patterns used for dynamic-offset lookups in the tail section.
        static readonly byte[] _STATS_ANCHOR   = { 0x5c, 0xfe, 0xff, 0xff, 0x00, 0x00, 0xf0, 0x41 };
        static readonly byte[] _GARAGE_NEEDLE  = { 0x17, 0x00, 0x00, 0x00, 0x09, 0x70, 0x61, 0x69, 0x6e, 0x74, 0x73, 0x68, 0x6f, 0x70 }; // uint32(23)+str8 "paintshop"
        static readonly byte[] _SKILL_NEEDLE   = Encoding.ASCII.GetBytes("fast_movement");
        static readonly byte[] _PAINTSHOP      = Encoding.ASCII.GetBytes("paintshop");

        // ── Low-level helpers ──────────────────────────────────────────────────

        static string BytesToHex(byte[] data, int offset, int length)
        {
            var sb = new StringBuilder(length * 2);
            for (int i = 0; i < length; i++)
                sb.Append(data[offset + i].ToString("x2"));
            return sb.ToString();
        }

        static byte[] HexToBytes(string hex)
        {
            var arr = new byte[hex.Length >> 1];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return arr;
        }

        static byte[] ConcatBytes(IEnumerable<byte[]> arrays)
        {
            var list = new List<byte[]>(arrays);
            int total = 0;
            foreach (var a in list) total += a.Length;
            var outB = new byte[total];
            int off = 0;
            foreach (var a in list) { Array.Copy(a, 0, outB, off, a.Length); off += a.Length; }
            return outB;
        }

        /// <summary>Find first occurrence of needle in haystack from fromIndex. -1 if absent.</summary>
        static int BytesIndexOf(byte[] haystack, byte[] needle, int fromIndex = 0)
        {
            for (int i = fromIndex; i <= haystack.Length - needle.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j]) { ok = false; break; }
                }
                if (ok) return i;
            }
            return -1;
        }

        /// <summary>Find last occurrence of needle in haystack. -1 if absent.</summary>
        static int BytesLastIndexOf(byte[] haystack, byte[] needle)
        {
            int result = -1, i = 0;
            while (true)
            {
                int idx = BytesIndexOf(haystack, needle, i);
                if (idx == -1) break;
                result = idx;
                i = idx + 1;
            }
            return result;
        }

        /// <summary>Read a length-prefixed UTF-8 string. Returns [string, nextPos].</summary>
        static Tuple<string, int> ReadStr8(byte[] data, int pos)
        {
            int n = data[pos];
            string s = Encoding.UTF8.GetString(data, pos + 1, n);
            return Tuple.Create(s, pos + 1 + n);
        }

        static byte[] WriteStr8(string s)
        {
            var b = Encoding.UTF8.GetBytes(s);
            var res = new byte[b.Length + 1];
            res[0] = (byte)b.Length;
            Array.Copy(b, 0, res, 1, b.Length);
            return res;
        }

        static bool IsValidPartName(string s)
        {
            if (s.Length < 3) return false;
            foreach (char c in s)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-')) return false;
            bool hasAlpha = false;
            bool allUpper = true;
            foreach (char c in s)
            {
                if (char.IsLetter(c))
                {
                    hasAlpha = true;
                    if (char.IsLower(c)) allUpper = false;
                }
            }
            if (hasAlpha && allUpper) return false;
            return true;
        }

        /// <summary>Scan forward from start to find the offset of the next valid part-name length byte.</summary>
        static int FindNextEntry(byte[] data, int start)
        {
            int max = Math.Min(256, data.Length - start);
            for (int size = 0; size < max; size++)
            {
                int pos = start + size;
                if (pos >= data.Length) break;
                int n = data[pos];
                if (n >= 2 && n <= 60)
                {
                    int end = pos + 1 + n;
                    if (end <= data.Length && AllPrintable(data, pos + 1, n))
                    {
                        string cand = Encoding.ASCII.GetString(data, pos + 1, n);
                        if (IsValidPartName(cand)) return pos;
                    }
                }
            }
            return -1;
        }

        static bool AllPrintable(byte[] data, int offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                byte b = data[offset + i];
                if (b < 32 || b >= 127) return false;
            }
            return true;
        }

        static int GuessBlockSize(string name)
        {
            string low = name.ToLowerInvariant();
            foreach (var p in _RIM_PREFIXES) if (low.StartsWith(p)) return _RIM_SIZE;
            foreach (var p in _BODY_PART_PREFIXES) if (low.StartsWith(p)) return _BODY_SIZE;
            return _STANDARD_SIZE;
        }

        // ── Block decode / encode ──────────────────────────────────────────────

        static PartBlock DecodeBlock(string name, byte[] block)
        {
            var entry = new PartBlock
            {
                Name = name,
                Raw = BytesToHex(block, 0, block.Length),
            };
            int n = block.Length;
            if (n >= 47)
            {
                entry.PartId = BitConverter.ToUInt16(block, 0);
                entry.Condition = BitConverter.ToSingle(block, 9);
                entry.Quality = BitConverter.ToSingle(block, 13);
                entry.Flag = BitConverter.ToUInt32(block, 17);
                entry.Extra = BitConverter.ToSingle(block, 39);
                entry.HasCondition = true;
            }
            if (n >= 63)
            {
                entry.PaintR = BitConverter.ToSingle(block, 47);
                entry.PaintG = BitConverter.ToSingle(block, 51);
                entry.PaintB = BitConverter.ToSingle(block, 55);
                entry.PaintA = BitConverter.ToSingle(block, 59);
                entry.HasPaint = true;
            }
            return entry;
        }

        static byte[] EncodeBlock(PartBlock entry)
        {
            var block = HexToBytes(entry.Raw);
            if (block.Length >= 47)
            {
                WriteSingle(block, 9, entry.Condition);
                WriteSingle(block, 13, entry.Quality);
                WriteUInt32(block, 17, entry.Flag);
                WriteSingle(block, 39, entry.Extra);
            }
            if (block.Length >= 63 && entry.HasPaint)
            {
                WriteSingle(block, 47, entry.PaintR);
                WriteSingle(block, 51, entry.PaintG);
                WriteSingle(block, 55, entry.PaintB);
                WriteSingle(block, 59, entry.PaintA);
            }
            return block;
        }

        static void WriteSingle(byte[] block, int offset, float value)
        {
            byte[] b = BitConverter.GetBytes(value);
            Array.Copy(b, 0, block, offset, 4);
        }

        static void WriteUInt32(byte[] block, int offset, uint value)
        {
            byte[] b = BitConverter.GetBytes(value);
            Array.Copy(b, 0, block, offset, 4);
        }

        // ── Parts section ──────────────────────────────────────────────────────

        static Tuple<PartsSection, int> ParsePartsSection(byte[] data, int start)
        {
            int pos = start;
            if (pos >= data.Length) return null;
            var sr = ReadStr8(data, pos);
            var section = new PartsSection { SectionName = sr.Item1 };
            pos = sr.Item2;

            if (pos + 4 > data.Length) return null;
            int count = (int)BitConverter.ToUInt32(data, pos);
            pos += 4;

            if (count > 0xFFFF || (count * 40 > data.Length)) return null;

            for (int i = 0; i < count; i++)
            {
                if (pos >= data.Length) break;
                int nameLen = data[pos];
                if (pos + 1 + nameLen > data.Length) break;
                string name = Encoding.UTF8.GetString(data, pos + 1, nameLen);
                pos += 1 + nameLen;

                int dataStart = pos;
                int blockSize;

                if (i < count - 1)
                {
                    int guessed = GuessBlockSize(name);
                    int cand = dataStart + guessed;
                    bool ok = false;
                    if (cand < data.Length)
                    {
                        int n = data[cand];
                        if (n >= 2 && n <= 60)
                        {
                            int end = cand + 1 + n;
                            if (end <= data.Length && AllPrintable(data, cand + 1, n))
                            {
                                string candStr = Encoding.ASCII.GetString(data, cand + 1, n);
                                if (IsValidPartName(candStr)) ok = true;
                            }
                        }
                    }
                    if (!ok)
                    {
                        int nextOff = FindNextEntry(data, dataStart);
                        if (nextOff != -1) guessed = nextOff - dataStart;
                    }
                    blockSize = guessed;
                }
                else
                {
                    blockSize = GuessBlockSize(name);
                    int nextOff = FindNextEntry(data, dataStart + 1);
                    if (nextOff != -1)
                    {
                        int candidate = nextOff - dataStart;
                        if (candidate >= 40 && candidate <= 149) blockSize = candidate;
                    }
                }

                if (dataStart + blockSize > data.Length) blockSize = data.Length - dataStart;
                var block = new byte[blockSize];
                Array.Copy(data, dataStart, block, 0, blockSize);
                section.Parts.Add(DecodeBlock(name, block));
                pos = dataStart + blockSize;
            }

            return Tuple.Create(section, pos);
        }

        static byte[] EncodePartsSection(PartsSection section)
        {
            var chunks = new List<byte[]>();
            chunks.Add(WriteStr8(section.SectionName));
            chunks.Add(BitConverter.GetBytes((uint)section.Parts.Count));
            foreach (var p in section.Parts)
            {
                chunks.Add(WriteStr8(p.Name));
                chunks.Add(EncodeBlock(p));
            }
            return ConcatBytes(chunks);
        }

        // ── Header ─────────────────────────────────────────────────────────────

        static Tuple<SaveHeader, int> ParseHeader(byte[] data)
        {
            string magic = Encoding.ASCII.GetString(data, 0, 8);
            if (magic != Magic)
                throw new InvalidOperationException("Not a valid CMS21 save file (bad magic: " + magic + ")");

            int year = BitConverter.ToUInt16(data, 8);
            int month = data[10];
            int nl = data[11];
            string name = Encoding.UTF8.GetString(data, 12, nl);
            int pos = 12 + nl;

            int rawStart = pos;
            while (pos < data.Length && data[pos] != 0xff) pos++;
            while (pos < data.Length && data[pos] == 0xff) pos++;

            var headerRaw = new byte[pos - rawStart];
            Array.Copy(data, rawStart, headerRaw, 0, headerRaw.Length);

            var hdr = new SaveHeader
            {
                Magic = magic,
                SaveYear = year,
                SaveMonth = month,
                ProfileName = name,
                Version = ExtractVersion(headerRaw),
                HeaderRaw = BytesToHex(headerRaw, 0, headerRaw.Length),
            };
            return Tuple.Create(hdr, pos);
        }

        static string ExtractVersion(byte[] raw)
        {
            for (int i = 0; i < raw.Length - 8; i++)
            {
                int n = raw[i];
                if (n >= 3 && n <= 16)
                {
                    var b = new byte[n];
                    Array.Copy(raw, i + 1, b, 0, n);
                    if (!AllPrintable(b, 0, n)) continue;
                    string s = Encoding.ASCII.GetString(b);
                    string stripped = s.TrimEnd('w', 'W', 'b', 'B');
                    var parts = stripped.Split('.');
                    if (parts.Length >= 2)
                    {
                        bool allDigits = true;
                        foreach (var p in parts)
                        {
                            if (p.Length > 0 && !IsAllDigits(p)) { allDigits = false; break; }
                        }
                        if (allDigits) return s;
                    }
                }
            }
            return "";
        }

        static bool IsAllDigits(string s)
        {
            foreach (char c in s) if (!char.IsDigit(c)) return false;
            return true;
        }

        static byte[] EncodeHeader(SaveHeader hdr)
        {
            var magicBytes = Encoding.ASCII.GetBytes(Magic);
            var chunks = new List<byte[]>
            {
                magicBytes,
                BitConverter.GetBytes((ushort)hdr.SaveYear),
                new byte[] { (byte)hdr.SaveMonth },
                WriteStr8(hdr.ProfileName),
                HexToBytes(hdr.HeaderRaw),
            };
            return ConcatBytes(chunks);
        }

        // ── Tail-section dynamic-offset helpers ────────────────────────────────

        static int TailStatsOffset(byte[] tail)
        {
            int idx = BytesLastIndexOf(tail, _STATS_ANCHOR);
            if (idx == -1) throw new InvalidOperationException("Stats anchor not found — unexpected save format");
            return idx + 17;
        }

        static int TailGarageStateOffset(byte[] tail)
        {
            int pidx = BytesIndexOf(tail, _PAINTSHOP);
            if (pidx == -1) throw new InvalidOperationException("Garage section not found (no paintshop)");
            int goff = pidx - 1 - 4;
            int count = (int)BitConverter.ToUInt32(tail, goff);
            int pos = goff + 4;
            for (int i = 0; i < count; i++)
            {
                int n = tail[pos];
                pos += 1 + n;
            }
            pos += 4;
            return pos;
        }

        static int TailSkillPointsOffset(byte[] tail)
        {
            int goff = BytesIndexOf(tail, _GARAGE_NEEDLE);
            if (goff == -1) throw new InvalidOperationException("Garage section not found in save");
            return goff - 4;
        }

        static int TailSkillsOffset(byte[] tail)
        {
            int idx = BytesIndexOf(tail, _SKILL_NEEDLE);
            if (idx == -1) throw new InvalidOperationException("Skills section not found in save");
            return idx - 1 - 4;
        }

        static List<Skill> ParseTailSkills(byte[] tail)
        {
            int pos = TailSkillsOffset(tail);
            int count = (int)BitConverter.ToUInt32(tail, pos);
            pos += 4;

            var names = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var sr = ReadStr8(tail, pos);
                names.Add(sr.Item1);
                pos = sr.Item2;
            }
            pos += 4;

            var skills = new List<Skill>();
            foreach (var name in names)
            {
                int dataLen = (int)BitConverter.ToUInt32(tail, pos);
                int dataStart = pos + 4;
                var skill = new Skill { Name = name };
                if (dataLen > 0) skill.Purchased = tail[dataStart] != 0;
                for (int i = 1; i < dataLen; i++)
                    skill.Tiers.Add(tail[dataStart + i] != 0);
                skills.Add(skill);
                pos += 4 + dataLen;
            }
            return skills;
        }

        static byte[] EncodeTailSkills(byte[] tail, List<Skill> skills)
        {
            var buf = (byte[])tail.Clone();
            int pos = TailSkillsOffset(buf);
            int count = (int)BitConverter.ToUInt32(buf, pos);
            pos += 4;

            var names = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var sr = ReadStr8(buf, pos);
                names.Add(sr.Item1);
                pos = sr.Item2;
            }
            pos += 4;

            for (int i = 0; i < names.Count; i++)
            {
                int dataLen = (int)BitConverter.ToUInt32(buf, pos);
                if (i < skills.Count)
                {
                    var s = skills[i];
                    var newData = new List<byte>();
                    newData.Add(s.Purchased ? (byte)1 : (byte)0);
                    foreach (var t in s.Tiers) newData.Add(t ? (byte)1 : (byte)0);
                    for (int k = 0; k < dataLen; k++)
                        buf[pos + 4 + k] = k < newData.Count ? newData[k] : (byte)0;
                }
                pos += 4 + dataLen;
            }
            return buf;
        }

        // ── Top-level decode / encode ──────────────────────────────────────────

        /// <summary>Decode a raw .cms21b byte array into a SaveData object.</summary>
        public static SaveData Decode(byte[] data)
        {
            var hdrRes = ParseHeader(data);
            var header = hdrRes.Item1;
            int hStart = hdrRes.Item2;

            var save = new SaveData { Header = header };

            int rawStart = 12 + data[11];
            var partsSections = new List<PartsSection>();
            int pos = hStart;
            int scanPos = rawStart;

            while (scanPos < data.Length - 5)
            {
                if (data[scanPos] == 0xff)
                {
                    while (scanPos < data.Length && data[scanPos] == 0xff) scanPos++;
                    var candidate = TrySectionAt(data, scanPos);
                    if (candidate != null)
                    {
                        var secRes = ParsePartsSection(data, scanPos);
                        if (secRes != null)
                        {
                            partsSections.Add(secRes.Item1);
                            pos = secRes.Item2;
                        }
                        break;
                    }
                }
                else
                {
                    scanPos++;
                }
            }

            if (partsSections.Count == 0) pos = hStart;

            save.PartsSections = partsSections;
            var tail = new byte[data.Length - pos];
            Array.Copy(data, pos, tail, 0, tail.Length);
            save.TailRaw = BytesToHex(tail, 0, tail.Length);
            return save;
        }

        static PartsSection TrySectionAt(byte[] data, int pos)
        {
            if (pos + 5 >= data.Length) return null;
            int namelen = data[pos];
            if (namelen < 3 || namelen > 20) return null;
            int nameEnd = pos + 1 + namelen;
            if (nameEnd + 4 > data.Length) return null;
            for (int i = pos + 1; i < nameEnd; i++)
                if (data[i] < 32 || data[i] >= 127) return null;
            int count = (int)BitConverter.ToUInt32(data, nameEnd);
            if (count > 5000) return null;
            return new PartsSection { SectionName = Encoding.UTF8.GetString(data, pos + 1, namelen) };
        }

        /// <summary>Re-encode a (possibly modified) SaveData into a byte array.</summary>
        public static byte[] Encode(SaveData save)
        {
            var chunks = new List<byte[]>();
            chunks.Add(EncodeHeader(save.Header));
            foreach (var sec in save.PartsSections)
                chunks.Add(EncodePartsSection(sec));
            chunks.Add(HexToBytes(save.TailRaw));
            return ConcatBytes(chunks);
        }

        // ── Derived data ───────────────────────────────────────────────────────

        public class Stats
        {
            public uint Money;
            public uint Level;   // display value (stored as level - 1)
            public uint Xp;
            public uint SkillPoints;
        }

        static byte[] TailBytes(SaveData save) => HexToBytes(save.TailRaw);

        public static Stats ParseStats(SaveData save)
        {
            var tail = TailBytes(save);
            int off = TailStatsOffset(tail);
            var st = new Stats
            {
                Money = BitConverter.ToUInt32(tail, off),
                Level = BitConverter.ToUInt32(tail, off + 4) + 1,
                Xp = BitConverter.ToUInt32(tail, off + 8),
                SkillPoints = BitConverter.ToUInt32(tail, TailSkillPointsOffset(tail)),
            };
            return st;
        }

        public static List<GarageItem> ParseGarage(SaveData save)
        {
            var tail = TailBytes(save);
            int baseOff = TailGarageStateOffset(tail);
            var list = new List<GarageItem>();
            for (int i = 0; i < GarageNames.Length; i++)
            {
                int off = baseOff + i * 8;
                list.Add(new GarageItem { Name = GarageNames[i], State = tail[off] });
            }
            return list;
        }

        public static List<Skill> ParseSkills(SaveData save)
        {
            return ParseTailSkills(TailBytes(save));
        }

        public static List<PartBlock> FlattenParts(SaveData save)
        {
            var outL = new List<PartBlock>();
            foreach (var sec in save.PartsSections)
                foreach (var p in sec.Parts)
                    outL.Add(p);
            return outL;
        }

        static readonly string[] _CAR_PART_PREFIXES = {
            "car_","window","mirror","door","trunk","hood","taillight","bumper","fender",
            "rim_","tire_","wheel_","suspension","piasta","tuleja","amortyzator",
            "tarcza","zacisk","klocki","wahacz","drazek","stabilizator","engine"
        };

        /// <summary>
        /// Scan the tail blob for car records and group their components.
        /// CMS21 saves are sequential: a car record followed by its components.
        /// </summary>
        public static List<Car> ParseCars(SaveData save)
        {
            var tail = TailBytes(save);
            var cars = new List<Car>();
            Car currentCar = null;

            for (int pos = 0; pos < tail.Length - 60; pos++)
            {
                int len = tail[pos];
                if (len >= 3 && len <= 50)
                {
                    bool isString = AllPrintable(tail, pos + 1, len);
                    if (!isString) continue;
                    string name = Encoding.UTF8.GetString(tail, pos + 1, len);
                    string lower = name.ToLowerInvariant();
                    bool isPart = false;
                    foreach (var p in _CAR_PART_PREFIXES)
                        if (lower.StartsWith(p)) { isPart = true; break; }

                    if (isPart || name.Contains("/") || name.Contains("("))
                    {
                        bool isMainCar = lower.StartsWith("car_") && !name.Contains("-") &&
                                         !lower.Contains("_wash") && name.Split('_').Length <= 2;
                        bool isWash = lower == "car_wash";

                        if (isMainCar || isWash)
                        {
                            currentCar = new Car { Name = name, RawName = name };
                            cars.Add(currentCar);
                        }
                        else if (currentCar != null)
                        {
                            int dataStart = pos + 1 + len;
                            int blockSize = GuessBlockSize(name);
                            if (dataStart + blockSize <= tail.Length)
                            {
                                var block = new byte[blockSize];
                                Array.Copy(tail, dataStart, block, 0, blockSize);
                                var decoded = DecodeBlock(name, block);
                                if (decoded.HasCondition && decoded.Condition >= 0 && decoded.Condition <= 1.0f)
                                {
                                    currentCar.Parts.Add(new CarPart
                                    {
                                        Name = name,
                                        Condition = decoded.Condition,
                                        Offset = dataStart,
                                    });
                                }
                            }
                        }
                        pos += len;
                    }
                }
            }

            var result = new List<Car>();
            foreach (var c in cars)
            {
                result.Add(new Car
                {
                    Name = c.Name.Replace("^car_", "").Replace("_", " "),
                    RawName = c.RawName,
                    Parts = c.Parts,
                });
            }
            return result;
        }

        /// <summary>
        /// Apply edits to a save object and return the resulting binary.
        /// </summary>
        public static byte[] ApplyEdits(SaveData save, Stats stats, List<PartEdit> partEdits,
            List<GarageEdit> garageEdits, List<Skill> skillEdits, List<CarPartEdit> carPartEdits)
        {
            save = Clone(save);

            // Player stats
            if (stats != null)
            {
                var tail = TailBytes(save);
                int off = TailStatsOffset(tail);
                WriteUInt32(tail, off, stats.Money);
                WriteUInt32(tail, off + 4, Math.Max(0u, stats.Level - 1));
                WriteUInt32(tail, off + 8, stats.Xp);
                WriteUInt32(tail, TailSkillPointsOffset(tail), stats.SkillPoints);
                save.TailRaw = BytesToHex(tail, 0, tail.Length);
            }

            // Part conditions
            if (partEdits != null)
            {
                foreach (var edit in partEdits)
                {
                    var block = save.PartsSections[edit.SecIdx].Parts[edit.PartIdx];
                    block.Condition = Clamp01(edit.Condition);
                }
            }

            // Garage state
            if (garageEdits != null && garageEdits.Count > 0)
            {
                var tail = TailBytes(save);
                int baseOff = TailGarageStateOffset(tail);
                foreach (var edit in garageEdits)
                    tail[baseOff + edit.Idx * 8] = (byte)Math.Max(0, Math.Min(255, edit.State));
                save.TailRaw = BytesToHex(tail, 0, tail.Length);
            }

            // Skills
            if (skillEdits != null && skillEdits.Count > 0)
            {
                var tail = TailBytes(save);
                var patched = EncodeTailSkills(tail, skillEdits);
                save.TailRaw = BytesToHex(patched, 0, patched.Length);
            }

            // Car parts
            if (carPartEdits != null && carPartEdits.Count > 0)
            {
                var tail = TailBytes(save);
                foreach (var edit in carPartEdits)
                    WriteSingle(tail, edit.Offset + 9, Clamp01(edit.Condition));
                save.TailRaw = BytesToHex(tail, 0, tail.Length);
            }

            return Encode(save);
        }

        public static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));

        static SaveData Clone(SaveData save)
        {
            var s = new SaveData
            {
                Header = new SaveHeader
                {
                    Magic = save.Header.Magic,
                    SaveYear = save.Header.SaveYear,
                    SaveMonth = save.Header.SaveMonth,
                    ProfileName = save.Header.ProfileName,
                    Version = save.Header.Version,
                    HeaderRaw = save.Header.HeaderRaw,
                },
                TailRaw = save.TailRaw,
            };
            foreach (var sec in save.PartsSections)
            {
                var ns = new PartsSection { SectionName = sec.SectionName };
                foreach (var p in sec.Parts)
                {
                    ns.Parts.Add(new PartBlock
                    {
                        Name = p.Name,
                        Raw = p.Raw,
                        PartId = p.PartId,
                        Condition = p.Condition,
                        Quality = p.Quality,
                        Flag = p.Flag,
                        Extra = p.Extra,
                        HasCondition = p.HasCondition,
                        PaintR = p.PaintR, PaintG = p.PaintG, PaintB = p.PaintB, PaintA = p.PaintA,
                        HasPaint = p.HasPaint,
                    });
                }
                s.PartsSections.Add(ns);
            }
            return s;
        }
    }

    public class PartEdit
    {
        public int SecIdx;
        public int PartIdx;
        public float Condition;
    }

    public class GarageEdit
    {
        public int Idx;
        public int State;
    }

    public class CarPartEdit
    {
        public int Offset;
        public float Condition;
    }
}
