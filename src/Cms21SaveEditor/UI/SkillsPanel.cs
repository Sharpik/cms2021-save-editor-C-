using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Cms21SaveEditor.Core;

namespace Cms21SaveEditor.UI
{
    /// <summary>Skills tab: per-skill purchased state and per-tier flags.</summary>
    public class SkillsPanel : UserControl
    {
        internal static readonly Dictionary<string, string> Display = new Dictionary<string, string>
        {
            { "fast_movement", "Fast Movement" },
            { "fix", "Repair Skills" },
            { "shop_discount", "Shop Discount" },
            { "strong_man", "Strong Man (Carry)" },
            { "inspector", "Inspector (Diagnostic)" },
            { "eagle_eye", "Eagle Eye (Spotting)" },
            { "negotiator", "Negotiator (Selling)" },
            { "lucky_bastard", "Lucky Bastard (Rare parts)" },
            { "renovation", "Renovation (Restoration)" },
        };

        private readonly List<Skill> _skills;
        private readonly List<SkillCard> _cards = new List<SkillCard>();
        private readonly Label _summary;

        public event EventHandler Changed;

        public SkillsPanel(List<Skill> skills)
        {
            _skills = skills;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 2, ColumnCount = 1 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill };
            _summary = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(4) };
            var maxAll = new Button { Text = "Max All Skills", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 0, 0, 0) };
            maxAll.Click += (s, e) =>
            {
                for (int i = 0; i < _skills.Count; i++)
                {
                    _skills[i].Purchased = true;
                    for (int t = 0; t < _skills[i].Tiers.Count; t++) _skills[i].Tiers[t] = true;
                    _cards[i].RefreshData();
                }
                UpdateSummary();
                Changed?.Invoke(this, EventArgs.Empty);
            };
            toolbar.Controls.Add(_summary);
            toolbar.Controls.Add(maxAll);
            root.Controls.Add(toolbar, 0, 0);

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true };
            for (int i = 0; i < skills.Count; i++)
            {
                var card = new SkillCard(skills[i]);
                card.Changed += (s, e) => { UpdateSummary(); Changed?.Invoke(this, EventArgs.Empty); };
                _cards.Add(card);
                flow.Controls.Add(card);
            }
            root.Controls.Add(flow, 0, 1);
            Controls.Add(root);
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int purchased = 0, tiers = 0, purchasedTiers = 0;
            foreach (var s in _skills)
            {
                if (s.Purchased) purchased++;
                tiers += s.Tiers.Count;
                foreach (var t in s.Tiers) if (t) purchasedTiers++;
            }
            _summary.Text = purchased + " / " + _skills.Count + " skills    " + purchasedTiers + " / " + tiers + " tiers";
        }
    }

    /// <summary>A single skill card with purchased checkbox and tier buttons.</summary>
    public class SkillCard : UserControl
    {
        private readonly Skill _skill;
        private readonly bool _origPurchased;
        private readonly List<bool> _origTiers = new List<bool>();
        private readonly CheckBox _check;
        private readonly FlowLayoutPanel _tierFlow;
        private readonly Label _was;

        public event EventHandler Changed;

        public SkillCard(Skill skill)
        {
            _skill = skill;
            _origPurchased = skill.Purchased;
            foreach (var t in skill.Tiers) _origTiers.Add(t);

            Width = 260;
            Height = 110;
            Margin = new Padding(4);
            BorderStyle = BorderStyle.FixedSingle;

            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));

            var name = SkillsPanel.Display.ContainsKey(skill.Name) ? SkillsPanel.Display[skill.Name] : skill.Name;
            _check = new CheckBox { Text = name + "  (" + skill.Name + ")", AutoSize = true, Dock = DockStyle.Top, Checked = skill.Purchased };
            _check.CheckedChanged += (s, e) =>
            {
                _skill.Purchased = _check.Checked;
                UpdateTierButtons();
                UpdateWas();
                Changed?.Invoke(this, EventArgs.Empty);
            };
            panel.Controls.Add(_check, 0, 0);

            _tierFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            for (int i = 0; i < skill.Tiers.Count; i++)
            {
                int tierIdx = i;
                var btn = new Button { Text = (i + 1).ToString(), Width = 30, Height = 24, Margin = new Padding(2, 0, 2, 0) };
                btn.Click += (s, e) =>
                {
                    _skill.Tiers[tierIdx] = !_skill.Tiers[tierIdx];
                    _check.Checked = true;
                    UpdateTierButtons();
                    UpdateWas();
                    Changed?.Invoke(this, EventArgs.Empty);
                };
                _tierFlow.Controls.Add(btn);
            }
            panel.Controls.Add(_tierFlow, 0, 1);

            _was = new Label { Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 8f) };
            panel.Controls.Add(_was, 0, 2);

            Controls.Add(panel);
            UpdateTierButtons();
            UpdateWas();
        }

        public void RefreshData()
        {
            _check.Checked = _skill.Purchased;
            UpdateTierButtons();
            UpdateWas();
        }

        private void UpdateTierButtons()
        {
            int i = 0;
            foreach (Control c in _tierFlow.Controls)
            {
                if (c is Button b)
                {
                    bool active = i < _skill.Tiers.Count && _skill.Tiers[i];
                    b.BackColor = active ? Color.FromArgb(46, 139, 87) : SystemColors.Control;
                    b.ForeColor = active ? Color.White : SystemColors.ControlText;
                    b.Enabled = _check.Checked;
                    i++;
                }
            }
        }

        private void UpdateWas()
        {
            bool changed = _skill.Purchased != _origPurchased;
            if (!changed)
            {
                for (int i = 0; i < _skill.Tiers.Count; i++)
                {
                    bool orig = i < _origTiers.Count && _origTiers[i];
                    if (orig != _skill.Tiers[i]) { changed = true; break; }
                }
            }
            _was.Text = changed ? "Modified" : "";
            _was.ForeColor = changed ? Color.DarkOrange : Color.Gray;
        }
    }
}
