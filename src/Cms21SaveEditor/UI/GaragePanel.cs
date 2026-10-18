using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Cms21SaveEditor.Core;

namespace Cms21SaveEditor.UI
{
    /// <summary>Garage tab: lock / unlock any of the 23 garage upgrades with tier states.</summary>
    public class GaragePanel : UserControl
    {
        static readonly Dictionary<string, int> Tiers = new Dictionary<string, int>
        {
            { "paintshop", 3 }, { "garage_upgrade", 3 }, { "lifter", 3 },
            { "repair_parts", 3 }, { "repair_body", 3 },
            { "scraps", 1 }, { "dyno", 1 }, { "warehouse", 1 }, { "path_test", 1 },
            { "car_wash", 1 }, { "unlock_tablet", 1 }, { "unlock_obd", 1 },
            { "unlock_fuel", 1 }, { "unlock_electronic", 1 }, { "garage_customization", 1 },
            { "unlock_cylinder", 1 }, { "unlock_tires", 1 }, { "brake_lathe", 1 },
            { "welder", 1 }, { "battery", 1 }, { "crane", 1 }, { "bus_upgrade", 1 },
            { "windowtint", 1 },
        };

        internal static readonly Dictionary<string, string> Display = new Dictionary<string, string>
        {
            { "paintshop", "Paint Shop" }, { "scraps", "Scrap Parts Bin" }, { "dyno", "Dynamometer" },
            { "warehouse", "Parts Warehouse" }, { "path_test", "Test Track" }, { "car_wash", "Car Wash" },
            { "unlock_tablet", "Diagnostic Tablet" }, { "unlock_obd", "OBD Scanner" },
            { "unlock_fuel", "Fuel System Kit" }, { "unlock_electronic", "Electronics Bench" },
            { "garage_upgrade", "Garage Upgrade" }, { "garage_customization", "Garage Customization" },
            { "lifter", "Car Lifter" }, { "unlock_cylinder", "Cylinder Head Kit" },
            { "unlock_tires", "Tire Equipment" }, { "brake_lathe", "Brake Lathe" },
            { "repair_parts", "Parts Repair Bench" }, { "welder", "Welding Station" },
            { "battery", "Battery Charger" }, { "crane", "Engine Crane" },
            { "repair_body", "Body Repair Set" }, { "bus_upgrade", "Bus Bay Upgrade" },
            { "windowtint", "Window Tinting" },
        };

        private readonly List<GarageItem> _garage;
        private readonly int[] _origState;
        private readonly List<GarageItemCard> _cards = new List<GarageItemCard>();

        public event EventHandler Changed;

        public GaragePanel(List<GarageItem> garage)
        {
            _garage = garage;
            _origState = new int[garage.Count];
            for (int i = 0; i < garage.Count; i++) _origState[i] = garage[i].State;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 2, ColumnCount = 1 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            var unlocked = CountUnlocked();
            var badge = new Label { Text = unlocked + " / " + garage.Count + " upgrades unlocked", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(4) };
            var maxAll = new Button { Text = "Max All Upgrades", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 0, 0, 0) };
            maxAll.Click += (s, e) =>
            {
                for (int i = 0; i < _garage.Count; i++)
                {
                    _garage[i].State = Tiers.ContainsKey(_garage[i].Name) ? Tiers[_garage[i].Name] : 1;
                    _cards[i].SetState(_garage[i].State);
                }
                Changed?.Invoke(this, EventArgs.Empty);
            };
            toolbar.Controls.Add(badge);
            toolbar.Controls.Add(maxAll);
            root.Controls.Add(toolbar, 0, 0);

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true };
            for (int i = 0; i < garage.Count; i++)
            {
                var item = garage[i];
                var card = new GarageItemCard(item, Tiers.ContainsKey(item.Name) ? Tiers[item.Name] : 1, _origState[i]);
                int idx = i;
                card.StateChanged += s => { _garage[idx].State = s; Changed?.Invoke(this, EventArgs.Empty); };
                _cards.Add(card);
                flow.Controls.Add(card);
            }
            root.Controls.Add(flow, 0, 1);
            Controls.Add(root);
        }

        private int CountUnlocked()
        {
            int c = 0;
            foreach (var g in _garage) if (g.State > 0) c++;
            return c;
        }
    }

    /// <summary>A single garage upgrade card with checkbox + optional tier buttons.</summary>
    public class GarageItemCard : UserControl
    {
        private readonly int _maxTier;
        private readonly int _orig;
        private int _state;
        private readonly CheckBox _check;
        private readonly FlowLayoutPanel _tierFlow;
        private readonly Label _was;

        public event Action<int> StateChanged;

        public GarageItemCard(GarageItem item, int maxTier, int orig)
        {
            _maxTier = maxTier;
            _orig = orig;
            _state = item.State;
            Width = 240;
            Height = 96;
            Margin = new Padding(4);
            BorderStyle = BorderStyle.FixedSingle;

            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));

            var header = new FlowLayoutPanel { Dock = DockStyle.Fill };
            _check = new CheckBox { AutoSize = true, Text = DisplayName(item.Name) + "  (" + item.Name + ")", Anchor = AnchorStyles.Left, Checked = _state > 0 };
            _check.CheckedChanged += (s, e) =>
            {
                int newState = _check.Checked ? 1 : 0;
                if (_maxTier > 1 && _check.Checked && _state > 0) newState = _state; // keep existing tier
                _state = newState;
                UpdateTiers();
                StateChanged?.Invoke(_state);
            };
            header.Controls.Add(_check);
            panel.Controls.Add(header, 0, 0);

            _tierFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            if (_maxTier > 1)
            {
                for (int t = 1; t <= _maxTier; t++)
                {
                    int tier = t;
                    var btn = new Button
                    {
                        Text = tier.ToString(),
                        Width = 30,
                        Height = 24,
                        Margin = new Padding(2, 0, 2, 0),
                        Tag = tier,
                    };
                    btn.Click += (s, e) =>
                    {
                        _state = tier;
                        _check.Checked = true;
                        UpdateTiers();
                        StateChanged?.Invoke(_state);
                    };
                    _tierFlow.Controls.Add(btn);
                }
            }
            panel.Controls.Add(_tierFlow, 0, 1);

            _was = new Label { Dock = DockStyle.Fill, AutoSize = false, Font = new Font(Font.FontFamily, 8f) };
            panel.Controls.Add(_was, 0, 2);

            Controls.Add(panel);
            UpdateTiers();
        }

        public void SetState(int state)
        {
            _state = state;
            _check.Checked = state > 0;
            UpdateTiers();
        }

        private void UpdateTiers()
        {
            bool changed = _state != _orig;
            _was.Text = changed ? "Modified (was " + _orig + ")" : "";
            _was.ForeColor = changed ? Color.DarkOrange : Color.Gray;
            if (_maxTier > 1)
            {
                int i = 0;
                foreach (Control c in _tierFlow.Controls)
                {
                    if (c is Button b)
                    {
                        int tier = (int)b.Tag;
                        bool active = _state >= tier;
                        b.BackColor = active ? Color.FromArgb(46, 139, 87) : SystemColors.Control;
                        b.ForeColor = active ? Color.White : SystemColors.ControlText;
                        b.Enabled = _check.Checked;
                        b.Text = tier.ToString();
                        i++;
                    }
                }
            }
        }

        private static string DisplayName(string name)
        {
            return GaragePanel.Display.ContainsKey(name) ? GaragePanel.Display[name] : name;
        }
    }
}
