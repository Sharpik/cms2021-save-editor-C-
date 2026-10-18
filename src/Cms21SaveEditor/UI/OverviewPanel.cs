using System;
using System.Drawing;
using System.Windows.Forms;
using Cms21SaveEditor.Core;

namespace Cms21SaveEditor.UI
{
    /// <summary>Overview tab: player stats (money / level / XP / skill points) and profile info.</summary>
    public class OverviewPanel : UserControl
    {
        private SaveFile.Stats _stats;
        private readonly SaveFile.Stats _orig;
        private readonly SaveHeader _header;
        private readonly NumericUpDown _money;
        private readonly NumericUpDown _level;
        private readonly NumericUpDown _xp;
        private readonly NumericUpDown _skillPoints;

        public event EventHandler Changed;

        public SaveFile.Stats Stats => _stats;

        public OverviewPanel(SaveFile.Stats stats, SaveHeader header)
        {
            _stats = stats;
            _orig = new SaveFile.Stats { Money = stats.Money, Level = stats.Level, Xp = stats.Xp, SkillPoints = stats.SkillPoints };
            _header = header;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 2 };
            var grid = new TableLayoutPanel { ColumnCount = 4, AutoSize = true, Dock = DockStyle.Top };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            _money = MakeNumeric(stats.Money, 0, uint.MaxValue);
            _level = MakeNumeric(stats.Level, 1, uint.MaxValue);
            _xp = MakeNumeric(stats.Xp, 0, uint.MaxValue);
            _skillPoints = MakeNumeric(stats.SkillPoints, 0, uint.MaxValue);

            _money.ValueChanged += (s, e) => { _stats.Money = (uint)_money.Value; Changed?.Invoke(this, EventArgs.Empty); };
            _level.ValueChanged += (s, e) => { _stats.Level = (uint)_level.Value; Changed?.Invoke(this, EventArgs.Empty); };
            _xp.ValueChanged += (s, e) => { _stats.Xp = (uint)_xp.Value; Changed?.Invoke(this, EventArgs.Empty); };
            _skillPoints.ValueChanged += (s, e) => { _stats.SkillPoints = (uint)_skillPoints.Value; Changed?.Invoke(this, EventArgs.Empty); };

            grid.Controls.Add(MakeStatCard("Money", _money), 0, 0);
            grid.Controls.Add(MakeStatCard("Level", _level), 1, 0);
            grid.Controls.Add(MakeStatCard("XP", _xp), 2, 0);
            grid.Controls.Add(MakeStatCard("Skill Points", _skillPoints), 3, 0);

            var profile = new GroupBox { Text = "Profile Info", Dock = DockStyle.Fill, Padding = new Padding(10) };
            var pl = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
            };
            pl.Controls.Add(MakeRow("Profile name", _header.ProfileName));
            pl.Controls.Add(MakeRow("Save date", _header.SaveYear + "-" + _header.SaveMonth.ToString("00")));
            pl.Controls.Add(MakeRow("Game version", string.IsNullOrEmpty(_header.Version) ? "—" : _header.Version));
            pl.Controls.Add(MakeRow("Magic", _header.Magic));
            profile.Controls.Add(pl);

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(grid, 0, 0);
            root.Controls.Add(profile, 0, 1);
            Controls.Add(root);
        }

        public void UpdateStats(SaveFile.Stats stats) { _stats = stats; }

        private NumericUpDown MakeNumeric(uint value, uint min, uint max)
        {
            return new NumericUpDown
            {
                Maximum = max,
                Minimum = min,
                Value = value,
                Dock = DockStyle.Fill,
                ThousandsSeparator = true,
            };
        }

        private static Control MakeStatCard(string label, Control input)
        {
            var card = new GroupBox { Text = label, Dock = DockStyle.Fill, Padding = new Padding(8) };
            var inner = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            inner.Controls.Add(new Label { Text = label, Font = new Font(SystemFonts.DefaultFont.FontFamily, 13f, FontStyle.Bold), Dock = DockStyle.Fill }, 0, 0);
            inner.Controls.Add(input, 0, 1);
            card.Controls.Add(inner);
            return card;
        }

        private static Control MakeRow(string label, string value)
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 4, 0, 4),
            };
            panel.Controls.Add(new Label
            {
                Text = label + ":",
                AutoSize = true,
                MinimumSize = new Size(120, 0),
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9.5f, FontStyle.Regular),
            });
            panel.Controls.Add(new Label
            {
                Text = value,
                AutoSize = true,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9.5f, FontStyle.Bold),
            });
            return panel;
        }
    }
}
