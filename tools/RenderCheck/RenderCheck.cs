using System;
using System.Drawing;
using System.Windows.Forms;
using Cms21SaveEditor.Core;
using Cms21SaveEditor.UI;

static class RenderCheck
{
    [STAThread]
    static int Main()
    {
        int failures = 0;

        var header = new SaveHeader
        {
            Magic = "PJOOOTER",
            SaveYear = 2021,
            SaveMonth = 5,
            ProfileName = "SharpNet Cars",
            Version = "1.0.40",
            HeaderRaw = "00",
        };
        var stats = new SaveFile.Stats { Money = 11131, Level = 1, Xp = 140, SkillPoints = 0 };

        using (var form = new Form { Width = 900, Height = 500 })
        {
            var panel = new OverviewPanel(stats, header) { Dock = DockStyle.Fill };
            form.Controls.Add(panel);
            form.Show();
            form.CreateControl();
            panel.PerformLayout();
            Application.DoEvents();
            panel.Refresh();

            // Collect all labels and their text/size
            Console.WriteLine("Labels in OverviewPanel:");
            foreach (var lbl in AllLabels(panel))
            {
                Console.WriteLine("  '" + lbl.Text + "' size=" + lbl.Size.Width + "x" + lbl.Size.Height +
                    " visible=" + lbl.Visible);
                if (lbl.Text == "SharpNet Cars" || lbl.Text == "1.0.40" || lbl.Text == "PJOOOTER" ||
                    lbl.Text == "2021-05")
                {
                    if (!lbl.Visible || lbl.Size.Width == 0 || lbl.Size.Height == 0)
                    {
                        Console.WriteLine("    FAIL: value label not rendered");
                        failures++;
                    }
                    else
                    {
                        Console.WriteLine("    OK: value label rendered");
                    }
                }
            }

            // Render to bitmap to force a real draw pass
            using (var bmp = new Bitmap(panel.Width, panel.Height))
            {
                panel.DrawToBitmap(bmp, new Rectangle(0, 0, panel.Width, panel.Height));
            }
        }

        Console.WriteLine(failures == 0 ? "RENDER CHECK PASSED" : failures + " RENDER FAILURES");
        return failures == 0 ? 0 : 1;
    }

    static System.Collections.Generic.IEnumerable<Label> AllLabels(Control root)
    {
        foreach (Control c in root.Controls)
        {
            if (c is Label l) yield return l;
            foreach (var sub in AllLabels(c)) yield return sub;
        }
    }
}
