using System.Collections.Generic;

namespace Cms21SaveEditor.Core
{
    /// <summary>Parsed save file header.</summary>
    public class SaveHeader
    {
        public string Magic;
        public int SaveYear;
        public int SaveMonth;
        public string ProfileName;
        public string Version;
        public string HeaderRaw; // hex of raw header bytes
    }

    /// <summary>A single decoded part block.</summary>
    public class PartBlock
    {
        public string Name;
        public string Raw;                 // hex of the original binary block
        public ushort PartId;
        public float Condition;
        public float Quality;
        public uint Flag;
        public float Extra;
        public bool HasCondition;
        public float PaintR, PaintG, PaintB, PaintA;
        public bool HasPaint;
    }

    /// <summary>One inventory parts section (section_name + parts).</summary>
    public class PartsSection
    {
        public string SectionName;
        public List<PartBlock> Parts = new List<PartBlock>();
    }

    /// <summary>A parsed skill record.</summary>
    public class Skill
    {
        public string Name;
        public bool Purchased;
        public List<bool> Tiers = new List<bool>();
    }

    /// <summary>A garage upgrade item.</summary>
    public class GarageItem
    {
        public string Name;
        public int State;
    }

    /// <summary>A car sub-component discovered in the tail.</summary>
    public class CarPart
    {
        public string Name;
        public float Condition;
        public int Offset;   // absolute offset within tail raw
    }

    /// <summary>A car / major assembly.</summary>
    public class Car
    {
        public string Name;
        public string RawName;
        public List<CarPart> Parts = new List<CarPart>();
    }

    /// <summary>The fully decoded save object (mirrors the JS save object).</summary>
    public class SaveData
    {
        public SaveHeader Header;
        public List<PartsSection> PartsSections = new List<PartsSection>();
        public string TailRaw; // hex of the tail blob (after the parts section)
    }
}
