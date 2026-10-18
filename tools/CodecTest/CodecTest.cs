using System;
using System.Collections.Generic;
using System.Text;
using Cms21SaveEditor.Core;

static class CodecTest
{
    static byte[] Magic = Encoding.ASCII.GetBytes("PJOOOTER");

    static byte[] WriteStr8(string s)
    {
        var b = Encoding.UTF8.GetBytes(s);
        var r = new byte[b.Length + 1];
        r[0] = (byte)b.Length;
        Array.Copy(b, 0, r, 1, b.Length);
        return r;
    }

    static byte[] Concat(params byte[][] arr)
    {
        int total = 0; foreach (var a in arr) total += a.Length;
        var o = new byte[total]; int off = 0;
        foreach (var a in arr) { Array.Copy(a, 0, o, off, a.Length); off += a.Length; }
        return o;
    }

    static byte[] BuildBlock(string name, float cond, int size)
    {
        var b = new byte[size];
        byte[] id = BitConverter.GetBytes((ushort)123);
        Array.Copy(id, 0, b, 0, 2);
        byte[] c = BitConverter.GetBytes(cond);
        Array.Copy(c, 0, b, 9, 4);
        byte[] q = BitConverter.GetBytes(1.0f);
        Array.Copy(q, 0, b, 13, 4);
        byte[] f = BitConverter.GetBytes((uint)0);
        Array.Copy(f, 0, b, 17, 4);
        byte[] e = BitConverter.GetBytes(0.5f);
        Array.Copy(e, 0, b, 39, 4);
        if (size >= 63)
        {
            foreach (var col in new[] { 47, 51, 55, 59 })
            {
                byte[] ch = BitConverter.GetBytes(1.0f);
                Array.Copy(ch, 0, b, col, 4);
            }
        }
        return b;
    }

    static byte[] BuildPartsSection()
    {
        var chunks = new List<byte[]>();
        chunks.Add(WriteStr8("garage"));
        chunks.Add(BitConverter.GetBytes((uint)3));
        chunks.Add(WriteStr8("car_engine"));
        chunks.Add(BuildBlock("car_engine", 0.4f, 63));
        chunks.Add(WriteStr8("rim_front"));
        chunks.Add(BuildBlock("rim_front", 0.8f, 64));
        chunks.Add(WriteStr8("piston"));
        chunks.Add(BuildBlock("piston", 1.0f, 47));
        return Concat(chunks.ToArray());
    }

    static byte[] BuildTail()
    {
        // A realistic gap of non-name car-data bytes so the parts-section's
        // last-block heuristic does not extend into the anchors.
        var gap = new byte[160];
        var rnd = new Random(42);
        rnd.NextBytes(gap);

        // Car region: a main car record followed by component blocks.
        var carChunks = new List<byte[]>();
        carChunks.Add(WriteStr8("car_bmw"));
        carChunks.Add(BuildBlock("car_bmw", 0.9f, 63));
        carChunks.Add(WriteStr8("car_door_l"));
        carChunks.Add(BuildBlock("car_door_l", 0.5f, 63));
        carChunks.Add(WriteStr8("rim_front_l"));
        carChunks.Add(BuildBlock("rim_front_l", 0.3f, 64));
        carChunks.Add(WriteStr8("piston"));
        carChunks.Add(BuildBlock("piston", 1.0f, 47));
        var carRegion = Concat(carChunks.ToArray());

        // Stats anchor + float + float + pad + money + level-1 + xp
        var stats = Concat(
            new byte[] { 0x5c, 0xfe, 0xff, 0xff, 0x00, 0x00, 0xf0, 0x41 },
            BitConverter.GetBytes(30.0f),
            BitConverter.GetBytes(15.0f),
            new byte[] { 0x00 },
            BitConverter.GetBytes((uint)12345),
            BitConverter.GetBytes((uint)4),   // level 5 stored as 4
            BitConverter.GetBytes((uint)999));

        // Skill points + garage section
        var names = new[] { "paintshop","scraps","dyno","warehouse","path_test","car_wash",
            "unlock_tablet","unlock_obd","unlock_fuel","unlock_electronic","garage_upgrade",
            "garage_customization","lifter","unlock_cylinder","unlock_tires","brake_lathe",
            "repair_parts","welder","battery","crane","repair_body","bus_upgrade","windowtint" };
        var gchunks = new List<byte[]>();
        gchunks.Add(BitConverter.GetBytes((uint)77)); // skill points
        gchunks.Add(BitConverter.GetBytes((uint)23));
        foreach (var n in names) gchunks.Add(WriteStr8(n));
        gchunks.Add(BitConverter.GetBytes((uint)23));
        for (int i = 0; i < 23; i++)
        {
            var state = new byte[8];
            state[0] = (byte)(i % 3); // tier states
            gchunks.Add(state);
        }
        var garage = Concat(gchunks.ToArray());

        // Skills section
        var skillNames = new[] { "fast_movement", "fix", "shop_discount", "strong_man" };
        var schunks = new List<byte[]>();
        schunks.Add(BitConverter.GetBytes((uint)skillNames.Length));
        foreach (var n in skillNames) schunks.Add(WriteStr8(n));
        schunks.Add(BitConverter.GetBytes((uint)skillNames.Length));
        foreach (var n in skillNames)
        {
            var data = n == "fast_movement"
                ? new byte[] { 1, 1, 1 }          // purchased + 2 tiers
                : new byte[] { 0, 0, 0, 0 };      // not purchased + 3 tiers
            schunks.Add(BitConverter.GetBytes((uint)data.Length));
            schunks.Add(data);
        }
        var skills = Concat(schunks.ToArray());

        return Concat(gap, carRegion, stats, garage, skills);
    }

    static byte[] BuildFullSave()
    {
        var header = Concat(
            Magic,
            BitConverter.GetBytes((ushort)2024),
            new byte[] { 5 },
            WriteStr8("TestPlayer"),
            new byte[] { 0x01, 0x02, 0x03, 0x04 }, // header raw
            new byte[] { 0xff, 0xff });            // padding
        return Concat(header, BuildPartsSection(), BuildTail());
    }

    static int Main()
    {
        int failures = 0;

        var bytes = BuildFullSave();
        var save = SaveFile.Decode(bytes);

        // Header
        Assert(save.Header.Magic == "PJOOOTER", "magic", ref failures);
        Assert(save.Header.SaveYear == 2024, "year", ref failures);
        Assert(save.Header.SaveMonth == 5, "month", ref failures);
        Assert(save.Header.ProfileName == "TestPlayer", "profile", ref failures);

        // Parts
        Assert(save.PartsSections.Count == 1, "one section", ref failures);
        Assert(save.PartsSections[0].Parts.Count == 3, "3 parts", ref failures);
        Assert(Math.Abs(save.PartsSections[0].Parts[0].Condition - 0.4f) < 0.001f, "car_engine cond", ref failures);
        Assert(save.PartsSections[0].Parts[0].HasPaint, "engine paint", ref failures);
        Assert(save.PartsSections[0].Parts[2].Name == "piston", "piston name", ref failures);

        // Stats
        var stats = SaveFile.ParseStats(save);
        Assert(stats.Money == 12345, "money", ref failures);
        Assert(stats.Level == 5, "level", ref failures);
        Assert(stats.Xp == 999, "xp", ref failures);
        Assert(stats.SkillPoints == 77, "skill points", ref failures);

        // Garage
        var garage = SaveFile.ParseGarage(save);
        Assert(garage.Count == 23, "garage count", ref failures);
        Assert(garage[0].Name == "paintshop" && garage[0].State == 0, "garage0", ref failures);
        Assert(garage[1].State == 1, "garage1", ref failures);

        // Skills
        var skills = SaveFile.ParseSkills(save);
        Assert(skills.Count == 4, "skills count", ref failures);
        Assert(skills[0].Name == "fast_movement" && skills[0].Purchased, "skill0 purchased", ref failures);
        Assert(skills[0].Tiers.Count == 2 && skills[0].Tiers[0] && skills[0].Tiers[1], "skill0 tiers", ref failures);

        // Cars: at least the car_bmw main record should be detected with components
        var cars = SaveFile.ParseCars(save);
        bool foundBmw = false;
        foreach (var c in cars)
            if (c.RawName == "car_bmw" && c.Parts.Count > 0) foundBmw = true;
        Assert(foundBmw, "car_bmw with components", ref failures);

        // Round-trip byte-exact
        var reencoded = SaveFile.Encode(save);
        bool identical = true;
        if (reencoded.Length != bytes.Length) identical = false;
        else for (int i = 0; i < bytes.Length; i++) if (reencoded[i] != bytes[i]) { identical = false; break; }
        Assert(identical, "round-trip byte-exact", ref failures);

        // ApplyEdits: change stats + garage + skills + part, then round-trip
        var edits = SaveFile.ApplyEdits(save,
            new SaveFile.Stats { Money = 1, Level = 2, Xp = 3, SkillPoints = 4 },
            new List<PartEdit> { new PartEdit { SecIdx = 0, PartIdx = 0, Condition = 1.0f } },
            new List<GarageEdit> { new GarageEdit { Idx = 0, State = 3 } },
            new List<Skill> { new Skill { Name = "fast_movement", Purchased = true, Tiers = new List<bool> { true, false } } },
            null);
        var save2 = SaveFile.Decode(edits);
        var stats2 = SaveFile.ParseStats(save2);
        Assert(stats2.Money == 1 && stats2.Level == 2 && stats2.Xp == 3 && stats2.SkillPoints == 4, "edited stats", ref failures);
        var gar2 = SaveFile.ParseGarage(save2);
        Assert(gar2[0].State == 3, "edited garage", ref failures);
        Assert(Math.Abs(save2.PartsSections[0].Parts[0].Condition - 1.0f) < 0.001f, "edited part", ref failures);
        var sk2 = SaveFile.ParseSkills(save2);
        Assert(sk2[0].Tiers[1] == false, "edited skill tier", ref failures);

        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : failures + " FAILURES");
        return failures == 0 ? 0 : 1;
    }

    static void Assert(bool cond, string label, ref int failures)
    {
        if (cond) Console.WriteLine("  OK   " + label);
        else { Console.WriteLine("  FAIL " + label); failures++; }
    }
}
