using System;
using System.IO;
using Cms21SaveEditor.Core;

static class HeaderCheck
{
    static int Main(string[] args)
    {
        var bytes = File.ReadAllBytes("saves/profile0.cms21b");
        var save = SaveFile.Decode(bytes);
        Console.WriteLine("Magic       : [" + save.Header.Magic + "]");
        Console.WriteLine("SaveYear    : " + save.Header.SaveYear);
        Console.WriteLine("SaveMonth   : " + save.Header.SaveMonth);
        Console.WriteLine("ProfileName : [" + save.Header.ProfileName + "]");
        Console.WriteLine("Version     : [" + save.Header.Version + "]");
        Console.WriteLine("HeaderRawLen: " + (save.Header.HeaderRaw.Length / 2) + " bytes");
        // ParseStats too
        try
        {
            var st = SaveFile.ParseStats(save);
            Console.WriteLine("Stats: money=" + st.Money + " level=" + st.Level + " xp=" + st.Xp + " sp=" + st.SkillPoints);
        }
        catch (Exception ex) { Console.WriteLine("Stats err: " + ex.Message); }
        return 0;
    }
}
