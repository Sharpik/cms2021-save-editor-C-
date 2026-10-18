using System;
using System.Collections.Generic;
using System.IO;
using Cms21SaveEditor.Core;

static class RealFileTest
{
    static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "saves/profile0.cms21b";
        int failures = 0;

        Console.WriteLine("Loading: " + Path.GetFullPath(path));
        var bytes = File.ReadAllBytes(path);
        Console.WriteLine("File size: " + bytes.Length + " bytes");

        // Decode
        var save = SaveFile.Decode(bytes);
        Console.WriteLine();
        Console.WriteLine("=== HEADER ===");
        Console.WriteLine("  Profile     : " + save.Header.ProfileName);
        Console.WriteLine("  Save date   : " + save.Header.SaveYear + "-" + save.Header.SaveMonth.ToString("00"));
        Console.WriteLine("  Version     : " + (string.IsNullOrEmpty(save.Header.Version) ? "(none)" : save.Header.Version));
        Console.WriteLine("  Magic       : " + save.Header.Magic);

        Console.WriteLine();
        Console.WriteLine("=== PARTS SECTIONS ===");
        foreach (var sec in save.PartsSections)
        {
            int worn = 0;
            foreach (var p in sec.Parts) if (p.HasCondition && p.Condition < 0.5f) worn++;
            Console.WriteLine("  Section '" + sec.SectionName + "': " + sec.Parts.Count + " parts, " + worn + " worn (<50%)");
        }
        Console.WriteLine("  Tail raw length (bytes): " + (save.TailRaw.Length / 2));

        // Stats
        try
        {
            var stats = SaveFile.ParseStats(save);
            Console.WriteLine();
            Console.WriteLine("=== PLAYER STATS ===");
            Console.WriteLine("  Money        : " + stats.Money.ToString("N0"));
            Console.WriteLine("  Level        : " + stats.Level);
            Console.WriteLine("  XP           : " + stats.Xp);
            Console.WriteLine("  Skill points : " + stats.SkillPoints);
            Assert(stats.Money > 0, "money parsed", ref failures);
        }
        catch (Exception ex)
        {
            Console.WriteLine("  FAIL stats: " + ex.Message);
            failures++;
        }

        // Garage
        try
        {
            var garage = SaveFile.ParseGarage(save);
            int unlocked = 0;
            foreach (var g in garage) if (g.State > 0) unlocked++;
            Console.WriteLine();
            Console.WriteLine("=== GARAGE (" + unlocked + " / " + garage.Count + " unlocked) ===");
            foreach (var g in garage)
                Console.WriteLine("  " + g.Name.PadRight(24) + " state=" + g.State);
            Assert(garage.Count == 23, "garage count == 23", ref failures);
        }
        catch (Exception ex)
        {
            Console.WriteLine("  FAIL garage: " + ex.Message);
            failures++;
        }

        // Skills
        try
        {
            var skills = SaveFile.ParseSkills(save);
            int purchased = 0, tiers = 0, purchasedTiers = 0;
            foreach (var s in skills)
            {
                if (s.Purchased) purchased++;
                tiers += s.Tiers.Count;
                foreach (var t in s.Tiers) if (t) purchasedTiers++;
            }
            Console.WriteLine();
            Console.WriteLine("=== SKILLS (" + purchased + " / " + skills.Count + " purchased, " + purchasedTiers + " / " + tiers + " tiers) ===");
            foreach (var s in skills)
                Console.WriteLine("  " + s.Name.PadRight(20) + " purchased=" + s.Purchased + " tiers=[" + string.Join(",", s.Tiers.Select2()) + "]");
            Assert(skills.Count > 0, "skills parsed", ref failures);
        }
        catch (Exception ex)
        {
            Console.WriteLine("  FAIL skills: " + ex.Message);
            failures++;
        }

        // Cars
        try
        {
            var cars = SaveFile.ParseCars(save);
            Console.WriteLine();
            Console.WriteLine("=== CARS (" + cars.Count + " detected) ===");
            foreach (var c in cars)
                Console.WriteLine("  " + c.Name.PadRight(28) + " parts=" + c.Parts.Count);
        }
        catch (Exception ex)
        {
            Console.WriteLine("  FAIL cars: " + ex.Message);
            failures++;
        }

        // Round-trip byte-exact
        var reencoded = SaveFile.Encode(save);
        Console.WriteLine("  header+parts+tail encoded size: " + reencoded.Length);
        bool identical = reencoded.Length == bytes.Length;
        if (identical)
            for (int i = 0; i < bytes.Length; i++)
                if (reencoded[i] != bytes[i]) { identical = false; break; }
        Console.WriteLine();
        Console.WriteLine("=== ROUND-TRIP ===");
        Console.WriteLine("  original size: " + bytes.Length + ", reencoded size: " + reencoded.Length);
        Assert(identical, "re-encode is byte-exact with original", ref failures);

        // Apply edits: change stats + garage + skills, verify values round-trip and
        // that only the edited bytes differ from the original.
        Console.WriteLine();
        Console.WriteLine("=== APPLY EDITS ===");
        var edits = SaveFile.ApplyEdits(save,
            new SaveFile.Stats { Money = 50000, Level = 10, Xp = 2000, SkillPoints = 5 },
            null, null, null, null);
        var edited = SaveFile.Decode(edits);
        var estats = SaveFile.ParseStats(edited);
        Console.WriteLine("  edited -> money=" + estats.Money + " level=" + estats.Level + " xp=" + estats.Xp + " sp=" + estats.SkillPoints);
        Assert(estats.Money == 50000, "edited money", ref failures);
        Assert(estats.Level == 10, "edited level", ref failures);
        Assert(estats.Xp == 2000, "edited xp", ref failures);
        Assert(estats.SkillPoints == 5, "edited skill points", ref failures);

        // Ensure the rest of the file is unchanged (diff count is small)
        int diffs = 0;
        for (int i = 0; i < edits.Length; i++)
            if (edits[i] != bytes[i]) diffs++;
        Console.WriteLine("  bytes changed by stats edit: " + diffs);
        Assert(diffs <= 20, "only stats bytes changed", ref failures);

        // Garage edit
        var gEdits = SaveFile.ApplyEdits(save, null, null,
            new List<GarageEdit> { new GarageEdit { Idx = 0, State = 2 } }, null, null);
        var gEdited = SaveFile.Decode(gEdits);
        var gGarage = SaveFile.ParseGarage(gEdited);
        Assert(gGarage[0].State == 2, "edited garage paintshop to 2", ref failures);

        // Skills edit
        var sEdits = SaveFile.ApplyEdits(save, null, null, null,
            new List<Skill> { new Skill { Name = "fast_movement", Purchased = true, Tiers = new List<bool> { true, false, false, false, false } } }, null);
        var sEdited = SaveFile.Decode(sEdits);
        var sSkills = SaveFile.ParseSkills(sEdited);
        Assert(sSkills[0].Purchased, "edited skill purchased", ref failures);

        // Simulate the GUI "Save" path: re-apply every part/garage/skill/car with its
        // CURRENT values (no-op edits). This must reproduce the file byte-for-byte,
        // otherwise the editor would corrupt a save even when nothing was changed.
        Console.WriteLine();
        Console.WriteLine("=== GUI SAVE (no-op edits) ===");
        var noopPartEdits = new List<PartEdit>();
        for (int si = 0; si < save.PartsSections.Count; si++)
            for (int pi = 0; pi < save.PartsSections[si].Parts.Count; pi++)
            {
                var p = save.PartsSections[si].Parts[pi];
                if (p.HasCondition)
                    noopPartEdits.Add(new PartEdit { SecIdx = si, PartIdx = pi, Condition = p.Condition });
            }
        var noopGarage = new List<GarageEdit>();
        var ng = SaveFile.ParseGarage(save);
        for (int i = 0; i < ng.Count; i++)
            noopGarage.Add(new GarageEdit { Idx = i, State = ng[i].State });
        var noopCars = new List<CarPartEdit>();
        var nc = SaveFile.ParseCars(save);
        foreach (var c in nc)
            foreach (var cp in c.Parts)
                noopCars.Add(new CarPartEdit { Offset = cp.Offset, Condition = cp.Condition });
        var noopSave = SaveFile.ApplyEdits(save, SaveFile.ParseStats(save), noopPartEdits, noopGarage, SaveFile.ParseSkills(save), noopCars);
        bool noopIdentical = noopSave.Length == bytes.Length;
        if (noopIdentical)
            for (int i = 0; i < bytes.Length; i++)
                if (noopSave[i] != bytes[i]) { noopIdentical = false; break; }
        Assert(noopIdentical, "no-op GUI save is byte-exact", ref failures);

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : failures + " FAILURES");
        return failures == 0 ? 0 : 1;
    }

    static void Assert(bool cond, string label, ref int failures)
    {
        if (cond) Console.WriteLine("  OK   " + label);
        else { Console.WriteLine("  FAIL " + label); failures++; }
    }
}

static class Ext
{
    public static IEnumerable<string> Select2(this IEnumerable<bool> src)
    {
        foreach (var b in src) yield return b ? "1" : "0";
    }
}
