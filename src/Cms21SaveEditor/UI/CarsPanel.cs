using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Cms21SaveEditor.Core;

namespace Cms21SaveEditor.UI
{
    /// <summary>Cars tab: vehicle components with per-part condition sliders.</summary>
    public class CarsPanel : UserControl
    {
        private readonly List<Car> _cars;
        private readonly List<CarCard> _cards = new List<CarCard>();
        private readonly Label _info;

        public event EventHandler Changed;

        public CarsPanel(List<Car> cars)
        {
            _cars = cars;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 2, ColumnCount = 1 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _info = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, Text = "" };
            root.Controls.Add(_info, 0, 0);

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.TopDown };
            if (cars.Count == 0)
            {
                _info.Text = "No vehicles detected. We couldn't find any vehicle records in the tail of this save file.";
            }
            else
            {
                _info.Text = "Found " + cars.Count + " vehicles / major assemblies. You can now repair individual components.";
                for (int i = 0; i < cars.Count; i++)
                {
                    var card = new CarCard(cars[i]);
                    card.Changed += (s, e) => Changed?.Invoke(this, EventArgs.Empty);
                    _cards.Add(card);
                    flow.Controls.Add(card);
                }
            }
            root.Controls.Add(flow, 0, 1);
            Controls.Add(root);
        }
    }

    /// <summary>A single car card with expandable component rows.</summary>
    public class CarCard : UserControl
    {
        private readonly Car _car;
        private readonly List<CarPartRow> _rows = new List<CarPartRow>();
        private readonly Panel _expanded;
        private readonly Label _editCount;
        private bool _isExpanded = false;

        public event EventHandler Changed;

        public CarCard(Car car)
        {
            _car = car;
            Width = 760;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Margin = new Padding(4);
            BorderStyle = BorderStyle.FixedSingle;

            var panel = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new TableLayoutPanel { ColumnCount = 3, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 6, 8, 6) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            var titleFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            var name = new Label { Text = car.Name, AutoSize = true, Font = new Font(Font.FontFamily, 11f, FontStyle.Bold), Anchor = AnchorStyles.Left };
            var meta = new Label { Text = "Internal ID: " + car.RawName, AutoSize = true, ForeColor = Color.Gray, Anchor = AnchorStyles.Left, Margin = new Padding(8, 0, 0, 0) };
            titleFlow.Controls.Add(name);
            titleFlow.Controls.Add(meta);

            var rightFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.RightToLeft };
            _editCount = new Label { Text = "", AutoSize = true, Anchor = AnchorStyles.Right, ForeColor = Color.DarkOrange, Margin = new Padding(0, 0, 12, 0) };
            var compCount = new Label { Text = car.Parts.Count + " components", AutoSize = true, Anchor = AnchorStyles.Right, ForeColor = Color.Gray, Margin = new Padding(0, 0, 12, 0) };
            var arrow = new Label { Text = "▼", AutoSize = true, Anchor = AnchorStyles.Right };
            rightFlow.Controls.Add(arrow);
            rightFlow.Controls.Add(compCount);
            rightFlow.Controls.Add(_editCount);

            header.Controls.Add(titleFlow, 0, 0);
            header.Controls.Add(rightFlow, 1, 0);
            header.Click += (s, e) => Toggle();

            _expanded = new Panel { Dock = DockStyle.Top, AutoSize = true, Visible = false };
            var expandedFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12, 0, 12, 12) };

            var fixAll = new Button { Text = "Repair All Components (100%)", AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
            fixAll.Click += (s, e) =>
            {
                foreach (var r in _rows) r.SetCondition(1.0f);
                UpdateCount();
                Changed?.Invoke(this, EventArgs.Empty);
            };
            expandedFlow.Controls.Add(fixAll);

            foreach (var part in car.Parts)
            {
                var row = new CarPartRow(part);
                row.Changed += (s, e) => { UpdateCount(); Changed?.Invoke(this, EventArgs.Empty); };
                _rows.Add(row);
                expandedFlow.Controls.Add(row);
            }
            if (car.Parts.Count == 0)
                expandedFlow.Controls.Add(new Label { Text = "No sub-components detected for this vehicle.", AutoSize = true, ForeColor = Color.Gray });

            _expanded.Controls.Add(expandedFlow);
            panel.Controls.Add(header, 0, 0);
            panel.Controls.Add(_expanded, 0, 1);
            Controls.Add(panel);
            UpdateCount();
        }

        private void Toggle()
        {
            _isExpanded = !_isExpanded;
            _expanded.Visible = _isExpanded;
        }

        private void UpdateCount()
        {
            int changed = 0;
            foreach (var r in _rows) if (r.IsChanged()) changed++;
            _editCount.Text = changed > 0 ? changed + " edits" : "";
        }
    }

    /// <summary>A car component row with a condition slider and Fix button.</summary>
    public class CarPartRow : UserControl
    {
        private readonly CarPart _part;
        private readonly float _orig;
        private readonly TrackBar _slider;
        private readonly Label _pct;

        public event EventHandler Changed;

        public CarPartRow(CarPart part)
        {
            _part = part;
            _orig = part.Condition;
            Width = 720;
            Height = 34;
            Margin = new Padding(0, 2, 0, 2);
            Dock = DockStyle.Top;

            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));

            var name = new Label { Text = part.Name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            _slider = new TrackBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Value = (int)Math.Round(part.Condition * 100), TickStyle = TickStyle.None };
            _slider.ValueChanged += (s, e) =>
            {
                _part.Condition = _slider.Value / 100f;
                _pct.Text = _slider.Value + "%";
                UpdateColor();
                Changed?.Invoke(this, EventArgs.Empty);
            };
            _pct = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false };
            var fix = new Button { Text = "Fix", Dock = DockStyle.Fill, Margin = new Padding(2) };
            fix.Click += (s, e) => { SetCondition(1.0f); Changed?.Invoke(this, EventArgs.Empty); };

            panel.Controls.Add(name, 0, 0);
            panel.Controls.Add(_slider, 1, 0);
            panel.Controls.Add(_pct, 2, 0);
            panel.Controls.Add(fix, 3, 0);
            Controls.Add(panel);
            UpdateColor();
        }

        public bool IsChanged() => Math.Abs(_part.Condition - _orig) > 0.001f;

        public void SetCondition(float v)
        {
            _slider.Value = (int)Math.Round(v * 100);
        }

        private void UpdateColor()
        {
            int pct = _slider.Value;
            Color c;
            if (pct < 15) c = Color.FromArgb(180, 40, 40);
            else if (pct < 50) c = Color.FromArgb(200, 160, 30);
            else c = Color.FromArgb(46, 139, 87);
            _pct.ForeColor = c;
            if (IsChanged()) BackColor = Color.FromArgb(255, 250, 225);
            else BackColor = SystemColors.Control;
        }
    }
}
