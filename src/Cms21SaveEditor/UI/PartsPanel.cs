using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Cms21SaveEditor.Core;

namespace Cms21SaveEditor.UI
{
    /// <summary>Parts tab: per-part condition editing, search, filters, bulk repair.</summary>
    public class PartsPanel : UserControl
    {
        private readonly List<PartRow> _rows = new List<PartRow>();
        private readonly TextBox _search;
        private readonly Button _btnAll, _btnWorn, _btnCritical;
        private readonly Label _count;
        private string _filter = "all";

        public event EventHandler Changed;

        public PartsPanel(SaveData save)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 2, ColumnCount = 1 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill };
            _search = new TextBox { Width = 180, Anchor = AnchorStyles.Left };
            _search.TextChanged += (s, e) => ApplyFilter();

            _btnAll = MakeFilterBtn("All", "all");
            _btnWorn = MakeFilterBtn("Worn  <100%", "worn");
            _btnCritical = MakeFilterBtn("Critical  <50%", "critical");

            var repairAll = new Button { Text = "Repair All", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 0, 0, 0) };
            repairAll.Click += (s, e) =>
            {
                int count = 0;
                foreach (var r in _rows)
                {
                    if (r.Part.HasCondition && r.Part.Condition < 1.0f)
                    {
                        r.Part.Condition = 1.0f;
                        r.Refresh();
                        count++;
                    }
                }
                ApplyFilter();
                Changed?.Invoke(this, EventArgs.Empty);
            };

            _count = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 0, 0, 0) };

            toolbar.Controls.Add(_search);
            toolbar.Controls.Add(_btnAll);
            toolbar.Controls.Add(_btnWorn);
            toolbar.Controls.Add(_btnCritical);
            toolbar.Controls.Add(repairAll);
            toolbar.Controls.Add(_count);
            root.Controls.Add(toolbar, 0, 0);

            var listPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var list = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, AutoSize = true };
            list.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            list.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            list.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            list.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));

            var header = MakeRowHeader("#", "Part Name", "Condition", "");
            list.Controls.Add(header, 0, 0);

            int totalWithCondition = 0;
            foreach (var sec in save.PartsSections)
            {
                for (int i = 0; i < sec.Parts.Count; i++)
                {
                    var p = sec.Parts[i];
                    var row = new PartRow(p, totalWithCondition);
                    row.Changed += (s, e) => Changed?.Invoke(this, EventArgs.Empty);
                    _rows.Add(row);
                    list.Controls.Add(row, 0, _rows.Count);
                    if (p.HasCondition) totalWithCondition++;
                }
            }

            listPanel.Controls.Add(list);
            root.Controls.Add(listPanel, 0, 1);
            Controls.Add(root);
            UpdateCount();
        }

        private Button MakeFilterBtn(string label, string key)
        {
            var b = new Button { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Tag = key, Margin = new Padding(4, 0, 0, 0) };
            b.Click += (s, e) =>
            {
                _filter = key;
                ApplyFilter();
            };
            return b;
        }

        private void ApplyFilter()
        {
            string q = _search.Text.Trim().ToLowerInvariant();
            foreach (var r in _rows)
            {
                bool visible = true;
                if (!r.Part.HasCondition) visible = false;
                if (visible && _filter == "worn" && r.Part.Condition >= 1.0f) visible = false;
                if (visible && _filter == "critical" && r.Part.Condition >= 0.5f) visible = false;
                if (visible && q.Length > 0 && !r.Part.Name.ToLowerInvariant().Contains(q)) visible = false;
                r.Visible = visible;
            }
            UpdateCount();
        }

        private void UpdateCount()
        {
            int shown = _rows.Count(r => r.Visible);
            int total = _rows.Count(r => r.Part.HasCondition);
            _count.Text = shown + " shown / " + total + " total";
        }

        private static Control MakeRowHeader(string c1, string c2, string c3, string c4)
        {
            var panel = new TableLayoutPanel { ColumnCount = 4, Dock = DockStyle.Top, AutoSize = true };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            var l1 = new Label { Text = c1, Dock = DockStyle.Fill, Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Bold) };
            var l2 = new Label { Text = c2, Dock = DockStyle.Fill, Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Bold) };
            var l3 = new Label { Text = c3, Dock = DockStyle.Fill, Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Bold) };
            var l4 = new Label { Text = c4, Dock = DockStyle.Fill };
            panel.Controls.Add(l1, 0, 0);
            panel.Controls.Add(l2, 1, 0);
            panel.Controls.Add(l3, 2, 0);
            panel.Controls.Add(l4, 3, 0);
            return panel;
        }
    }

    /// <summary>A single part row: name, condition bar, percent, repair button.</summary>
    public class PartRow : UserControl
    {
        public PartBlock Part { get; }
        private readonly float _orig;
        private readonly ProgressBar _bar;
        private readonly Label _pct;
        private readonly Button _repair;

        public event EventHandler Changed;

        public PartRow(PartBlock part, int rowIndex)
        {
            Part = part;
            _orig = part.HasCondition ? part.Condition : 0f;
            Height = 32;
            Dock = DockStyle.Top;
            Margin = new Padding(0);
            Width = 600;

            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Margin = new Padding(0) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));

            var idx = new Label { Text = (rowIndex + 1).ToString(), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(0, 0, 6, 0) };
            var name = new Label { Text = part.Name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            _bar = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Height = 14, Margin = new Padding(0, 9, 6, 9) };
            _pct = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false };
            _repair = new Button { Text = "Fix", Dock = DockStyle.Fill, Margin = new Padding(2) };
            _repair.Click += (s, e) =>
            {
                Part.Condition = 1.0f;
                Refresh();
                Changed?.Invoke(this, EventArgs.Empty);
            };

            panel.Controls.Add(idx, 0, 0);
            panel.Controls.Add(name, 1, 0);
            panel.Controls.Add(_bar, 2, 0);
            panel.Controls.Add(_pct, 3, 0);
            panel.Controls.Add(_repair, 4, 0);
            Controls.Add(panel);
            Refresh();
        }

        public new void Refresh()
        {
            float cond = Part.HasCondition ? Part.Condition : 0f;
            int pct = (int)Math.Round(cond * 100);
            _bar.Value = Math.Max(0, Math.Min(100, pct));
            _pct.Text = pct + "%";
            Color c;
            if (cond >= 0.8f) c = Color.FromArgb(46, 139, 87);
            else if (cond >= 0.5f) c = Color.FromArgb(200, 160, 30);
            else c = Color.FromArgb(180, 40, 40);
            _bar.ForeColor = c;
            _pct.ForeColor = c;
            _repair.Enabled = cond < 1.0f;
            if (Part.HasCondition && Math.Abs(cond - _orig) > 0.001f)
                BackColor = Color.FromArgb(255, 250, 225);
            else
                BackColor = SystemColors.Control;
        }
    }
}
