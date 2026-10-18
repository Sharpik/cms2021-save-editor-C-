using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Cms21SaveEditor.Core;

namespace Cms21SaveEditor.UI
{
    /// <summary>
    /// Main editor window. Handles opening / saving .cms21b files and hosts the
    /// Overview, Garage, Parts, Skills and Cars tabs.
    /// </summary>
    public class MainForm : Form
    {
        private SaveData _save;
        private SaveFile.Stats _stats;
        private SaveFile.Stats _origStats;
        private List<GarageItem> _garage;
        private List<Skill> _skills;
        private List<Car> _cars;
        private string _filename = "";

        private int[] _origGarageState;
        private List<KeyValuePair<bool, bool[]>> _origSkills;
        private List<KeyValuePair<string, float[]>> _origCars;
        private float[] _origPartConditions;

        private TabControl _tabs;
        private OverviewPanel _overview;
        private GaragePanel _garagePanel;
        private PartsPanel _partsPanel;
        private SkillsPanel _skillsPanel;
        private CarsPanel _carsPanel;
        private Button _btnSave;
        private Button _btnOpen;
        private Label _dirtyBadge;
        private Label _profileLabel;

        public MainForm()
        {
            Text = "CMS21 Save Editor";
            Width = 980;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;

            var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 48, ColumnCount = 2 };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            var left = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 8), WrapContents = false };
            var title = new Label { Text = "CMS21 Save Editor", AutoSize = true, Font = new Font(Font.FontFamily, 13f, FontStyle.Bold), Anchor = AnchorStyles.Left };
            _profileLabel = new Label { Text = "", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.Gray, Margin = new Padding(14, 0, 0, 0) };
            _dirtyBadge = new Label { Text = "", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.DarkOrange, Margin = new Padding(14, 0, 0, 0) };
            left.Controls.Add(title);
            left.Controls.Add(_profileLabel);
            left.Controls.Add(_dirtyBadge);

            var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 8, 10, 8), WrapContents = false };
            _btnSave = new Button { Text = "Save .cms21b", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(6, 0, 0, 0), Enabled = false };
            _btnSave.Click += (s, e) => SaveFileDialog();
            _btnOpen = new Button { Text = "Open another file", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(6, 0, 0, 0) };
            _btnOpen.Click += (s, e) => OpenFileDialog();
            right.Controls.Add(_btnSave);
            right.Controls.Add(_btnOpen);

            top.Controls.Add(left, 0, 0);
            top.Controls.Add(right, 1, 0);

            _tabs = new TabControl { Dock = DockStyle.Fill };
            Controls.Add(_tabs);
            Controls.Add(top);

            var footer = new Label
            {
                Text = "Always back up your save files before editing. Default save location: %USERPROFILE%\\AppData\\LocalLow\\Red Dot Games\\Car Mechanic Simulator 2021\\Save\\",
                Dock = DockStyle.Bottom,
                AutoSize = false,
                Height = 26,
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
            };
            Controls.Add(footer);
            footer.BringToFront();

            OpenFileDialog();
        }

        private void OpenFileDialog()
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "CMS21 Save (*.cms21b)|*.cms21b|All files (*.*)|*.*",
                Title = "Open CMS21 save file",
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    LoadFile(dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Failed to load save", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadFile(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var save = SaveFile.Decode(bytes);
            var stats = SaveFile.ParseStats(save);
            var garage = SaveFile.ParseGarage(save);
            var skills = SaveFile.ParseSkills(save);
            var cars = SaveFile.ParseCars(save);

            _save = save;
            _stats = stats;
            _origStats = new SaveFile.Stats { Money = stats.Money, Level = stats.Level, Xp = stats.Xp, SkillPoints = stats.SkillPoints };
            _garage = garage;
            _skills = skills;
            _cars = cars;
            _filename = Path.GetFileName(path);

            _origGarageState = new int[garage.Count];
            for (int i = 0; i < garage.Count; i++) _origGarageState[i] = garage[i].State;

            _origSkills = new List<KeyValuePair<bool, bool[]>>();
            foreach (var s in skills)
            {
                var tiers = new bool[s.Tiers.Count];
                for (int i = 0; i < s.Tiers.Count; i++) tiers[i] = s.Tiers[i];
                _origSkills.Add(new KeyValuePair<bool, bool[]>(s.Purchased, tiers));
            }

            _origCars = new List<KeyValuePair<string, float[]>>();
            foreach (var c in cars)
            {
                var conds = new float[c.Parts.Count];
                for (int i = 0; i < c.Parts.Count; i++) conds[i] = c.Parts[i].Condition;
                _origCars.Add(new KeyValuePair<string, float[]>(c.RawName, conds));
            }

            var flatParts = SaveFile.FlattenParts(save);
            _origPartConditions = new float[flatParts.Count];
            for (int i = 0; i < flatParts.Count; i++)
                _origPartConditions[i] = flatParts[i].HasCondition ? flatParts[i].Condition : -1f;

            _profileLabel.Text = _save.Header.ProfileName + "   " +
                _save.Header.SaveYear + "-" + _save.Header.SaveMonth.ToString("00") +
                (string.IsNullOrEmpty(_save.Header.Version) ? "" : "   v" + _save.Header.Version);

            _tabs.TabPages.Clear();

            _overview = new OverviewPanel(stats, save.Header);
            _overview.Changed += (s, e) => { _stats = _overview.Stats; UpdateDirty(); };

            _garagePanel = new GaragePanel(garage);
            _garagePanel.Changed += (s, e) => UpdateDirty();

            _partsPanel = new PartsPanel(save);
            _partsPanel.Changed += (s, e) => UpdateDirty();

            _skillsPanel = new SkillsPanel(skills);
            _skillsPanel.Changed += (s, e) => UpdateDirty();

            _carsPanel = new CarsPanel(cars);
            _carsPanel.Changed += (s, e) => UpdateDirty();

            AddTab("Overview", _overview);
            AddTab("Garage", _garagePanel);
            AddTab("Parts", _partsPanel);
            AddTab("Skills", _skillsPanel);
            AddTab("Cars", _carsPanel);

            _btnSave.Enabled = true;
            UpdateDirty();
        }

        private void AddTab(string title, Control content)
        {
            var page = new TabPage(title) { Padding = new Padding(0) };
            content.Dock = DockStyle.Fill;
            page.Controls.Add(content);
            _tabs.TabPages.Add(page);
        }

        private void UpdateDirty()
        {
            bool dirty = IsDirty();
            _dirtyBadge.Text = dirty ? "● Unsaved changes" : "";
        }

        private bool IsDirty()
        {
            if (_save == null) return false;
            if (_origStats != null && _stats != null &&
                (_stats.Money != _origStats.Money || _stats.Level != _origStats.Level ||
                 _stats.Xp != _origStats.Xp || _stats.SkillPoints != _origStats.SkillPoints))
                return true;

            for (int i = 0; i < _garage.Count; i++)
                if (_garage[i].State != _origGarageState[i]) return true;

            for (int i = 0; i < _skills.Count; i++)
            {
                var orig = _origSkills[i];
                if (_skills[i].Purchased != orig.Key) return true;
                for (int t = 0; t < _skills[i].Tiers.Count; t++)
                    if (t >= orig.Value.Length || _skills[i].Tiers[t] != orig.Value[t]) return true;
            }

            for (int i = 0; i < _cars.Count; i++)
            {
                var orig = _origCars[i];
                for (int p = 0; p < _cars[i].Parts.Count; p++)
                    if (Math.Abs(_cars[i].Parts[p].Condition - orig.Value[p]) > 0.001f) return true;
            }

            var flat = SaveFile.FlattenParts(_save);
            for (int i = 0; i < flat.Count; i++)
                if (flat[i].HasCondition && Math.Abs(flat[i].Condition - _origPartConditions[i]) > 0.001f) return true;

            return false;
        }

        private void SaveFileDialog()
        {
            if (_save == null) return;
            using (var dlg = new SaveFileDialog
            {
                Filter = "CMS21 Save (*.cms21b)|*.cms21b",
                Title = "Save modified .cms21b",
                FileName = string.IsNullOrEmpty(_filename) ? "profile0.cms21b" : _filename,
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var result = BuildBinary();
                    File.WriteAllBytes(dlg.FileName, result);
                    _origStats = new SaveFile.Stats { Money = _stats.Money, Level = _stats.Level, Xp = _stats.Xp, SkillPoints = _stats.SkillPoints };
                    for (int i = 0; i < _garage.Count; i++) _origGarageState[i] = _garage[i].State;
                    for (int i = 0; i < _skills.Count; i++)
                    {
                        var tiers = new bool[_skills[i].Tiers.Count];
                        for (int t = 0; t < _skills[i].Tiers.Count; t++) tiers[t] = _skills[i].Tiers[t];
                        _origSkills[i] = new KeyValuePair<bool, bool[]>(_skills[i].Purchased, tiers);
                    }
                    for (int i = 0; i < _cars.Count; i++)
                    {
                        var conds = new float[_cars[i].Parts.Count];
                        for (int p = 0; p < _cars[i].Parts.Count; p++) conds[p] = _cars[i].Parts[p].Condition;
                        _origCars[i] = new KeyValuePair<string, float[]>(_cars[i].RawName, conds);
                    }
                    var flat = SaveFile.FlattenParts(_save);
                    for (int i = 0; i < flat.Count; i++)
                        _origPartConditions[i] = flat[i].HasCondition ? flat[i].Condition : -1f;
                    UpdateDirty();
                    MessageBox.Show(this, "Saved to " + dlg.FileName, "Save complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private byte[] BuildBinary()
        {
            var partEdits = new List<PartEdit>();
            for (int si = 0; si < _save.PartsSections.Count; si++)
                for (int pi = 0; pi < _save.PartsSections[si].Parts.Count; pi++)
                {
                    var p = _save.PartsSections[si].Parts[pi];
                    if (p.HasCondition)
                        partEdits.Add(new PartEdit { SecIdx = si, PartIdx = pi, Condition = p.Condition });
                }

            var garageEdits = new List<GarageEdit>();
            for (int i = 0; i < _garage.Count; i++)
                garageEdits.Add(new GarageEdit { Idx = i, State = _garage[i].State });

            var skillEdits = _skills;

            var carPartEdits = new List<CarPartEdit>();
            foreach (var c in _cars)
                foreach (var p in c.Parts)
                    carPartEdits.Add(new CarPartEdit { Offset = p.Offset, Condition = p.Condition });

            return SaveFile.ApplyEdits(_save, _stats, partEdits, garageEdits, skillEdits, carPartEdits);
        }
    }
}
