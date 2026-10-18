using System;
using System.IO;

static class DebugA
{
    static int Main(string[] args)
    {
        var bytes = File.ReadAllBytes("saves/profile0.cms21b");
        // Byte-level dump from 17090 to end
        for (int i = 17090; i < bytes.Length; i++)
        {
            Console.WriteLine(i.ToString("00000") + ": " + bytes[i].ToString("X2") + "  '" + (bytes[i] >= 32 && bytes[i] < 127 ? ((char)bytes[i]).ToString() : ".") + "'");
        }
        return 0;
    }
}
