namespace TrackerApp;

public sealed class ReviewPresetForm : Form
{
    private readonly TextBox _nameTextBox = new();
    private readonly CheckBox _restrictToSelectedNodeCheckBox = new();
    private readonly Label _selectedNodeLabel = new();
    private readonly ComboBox _tagComboBox = new();
    private readonly ComboBox _difficultyComboBox = new();
    private readonly ComboBox _orderModeComboBox = new();
    private readonly CheckBox _dueOnlyCheckBox = new();
    private readonly CheckBox _failedRecentlyCheckBox = new();

    public ReviewPresetForm(string title, string selectedNodePath, IReadOnlyList<TagModel> tags, ReviewPresetModel? existingPreset = null)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(640, 456);
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        UiLayoutHelper.ApplyFormDefaults(this);
        BuildLayout(tags, selectedNodePath, existingPreset);
        UiLayoutHelper.ApplyRecursive(this);
    }

    public string PresetName => _nameTextBox.Text.Trim();
    public bool RestrictToSelectedNode => _restrictToSelectedNodeCheckBox.Checked;
    public int? SelectedTagId => _tagComboBox.SelectedItem is TagChoice choice && choice.Id > 0 ? choice.Id : null;
    public StudyDifficulty SelectedDifficulty => _difficultyComboBox.SelectedItem is DifficultyChoice difficulty ? difficulty.Difficulty : StudyDifficulty.Any;
    public ReviewSessionOrderMode SelectedOrderMode => _orderModeComboBox.SelectedItem is OrderChoice order ? order.OrderMode : ReviewSessionOrderMode.Default;
    public bool DueOnly => _dueOnlyCheckBox.Checked;
    public bool FailedRecentlyOnly => _failedRecentlyCheckBox.Checked;

    private void BuildLayout(IReadOnlyList<TagModel> tags, string selectedNodePath, ReviewPresetModel? existingPreset)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(10),
            RightToLeft = RightToLeft.Yes
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        for (var i = 0; i < 6; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));

        _selectedNodeLabel.Dock = DockStyle.Fill;
        _selectedNodeLabel.TextAlign = ContentAlignment.MiddleRight;
        _selectedNodeLabel.BorderStyle = BorderStyle.FixedSingle;
        _selectedNodeLabel.BackColor = Color.White;
        _selectedNodeLabel.Text = string.IsNullOrWhiteSpace(selectedNodePath) ? "ללא צומת נבחר" : selectedNodePath;

        _nameTextBox.Dock = DockStyle.Fill;

        _restrictToSelectedNodeCheckBox.Text = "שמור גם את הצומת הנבחר";
        _restrictToSelectedNodeCheckBox.Checked = existingPreset?.RestrictToSubject ?? !string.IsNullOrWhiteSpace(selectedNodePath);
        _restrictToSelectedNodeCheckBox.Dock = DockStyle.Fill;
        _restrictToSelectedNodeCheckBox.TextAlign = ContentAlignment.MiddleRight;

        _tagComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _tagComboBox.Dock = DockStyle.Fill;
        _tagComboBox.Items.Add(new TagChoice(null, "ללא תגית"));
        foreach (var tag in tags)
        {
            _tagComboBox.Items.Add(new TagChoice(tag.Id, tag.Name));
        }
        _tagComboBox.SelectedIndex = 0;

        _difficultyComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _difficultyComboBox.Dock = DockStyle.Fill;
        _difficultyComboBox.Items.AddRange(
        [
            new DifficultyChoice(StudyDifficulty.Any, "כל הרמות"),
            new DifficultyChoice(StudyDifficulty.Hard, "קשה"),
            new DifficultyChoice(StudyDifficulty.Medium, "בינונית"),
            new DifficultyChoice(StudyDifficulty.Easy, "קלה")
        ]);
        _difficultyComboBox.SelectedIndex = 0;

        _orderModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _orderModeComboBox.Dock = DockStyle.Fill;
        _orderModeComboBox.Items.AddRange(
        [
            new OrderChoice(ReviewSessionOrderMode.Default, "לפי הסדר הרגיל"),
            new OrderChoice(ReviewSessionOrderMode.Random, "אקראי"),
            new OrderChoice(ReviewSessionOrderMode.HardFirst, "מהקשים לקלים"),
            new OrderChoice(ReviewSessionOrderMode.FailedFirst, "מהנכשלים תחילה"),
            new OrderChoice(ReviewSessionOrderMode.NewFirst, "מהחדשים תחילה")
        ]);
        _orderModeComboBox.SelectedIndex = 0;

        _dueOnlyCheckBox.Text = "רק לביצוע היום";
        _dueOnlyCheckBox.Checked = existingPreset?.DueOnly ?? true;
        _dueOnlyCheckBox.Dock = DockStyle.Fill;
        _dueOnlyCheckBox.TextAlign = ContentAlignment.MiddleRight;

        _failedRecentlyCheckBox.Text = "נכשלו לאחרונה";
        _failedRecentlyCheckBox.Checked = existingPreset?.FailedRecentlyOnly ?? false;
        _failedRecentlyCheckBox.Dock = DockStyle.Fill;
        _failedRecentlyCheckBox.TextAlign = ContentAlignment.MiddleRight;

        if (existingPreset is not null)
        {
            _nameTextBox.Text = existingPreset.Name;
            SelectDifficulty(existingPreset.Difficulty);
            SelectOrderMode(existingPreset.OrderMode);
            SelectTag(existingPreset.TagId);
        }

        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes
        };
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));

        var saveButton = new Button { Text = "שמור", Width = 96, Height = 36 };
        UiLayoutHelper.StyleActionButton(saveButton, 96, 36);
        saveButton.Click += (_, _) =>
        {
            if (PresetName.Length == 0)
            {
                MessageBox.Show(this, "יש להזין שם לפריסט.", "פריסט חזרה", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        var cancelButton = new Button { Text = "ביטול", Width = 96, Height = 36 };
        UiLayoutHelper.StyleActionButton(cancelButton, 96, 36);
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        saveButton.Dock = DockStyle.Fill;
        cancelButton.Dock = DockStyle.Fill;
        buttonPanel.Controls.Add(saveButton, 0, 0);
        buttonPanel.Controls.Add(cancelButton, 1, 0);

        layout.Controls.Add(CreateLabel("שם פריסט"), 0, 0);
        layout.Controls.Add(_nameTextBox, 1, 0);
        layout.Controls.Add(CreateLabel("צומת"), 0, 1);
        layout.Controls.Add(_selectedNodeLabel, 1, 1);
        layout.Controls.Add(CreateLabel(""), 0, 2);
        layout.Controls.Add(_restrictToSelectedNodeCheckBox, 1, 2);
        layout.Controls.Add(CreateLabel("תגית"), 0, 3);
        layout.Controls.Add(_tagComboBox, 1, 3);
        layout.Controls.Add(CreateLabel("קושי"), 0, 4);
        layout.Controls.Add(_difficultyComboBox, 1, 4);
        layout.Controls.Add(CreateLabel("סדר"), 0, 5);
        layout.Controls.Add(_orderModeComboBox, 1, 5);

        var optionsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes
        };
        optionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        optionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        optionsPanel.Controls.Add(_failedRecentlyCheckBox, 0, 0);
        optionsPanel.Controls.Add(_dueOnlyCheckBox, 1, 0);
        layout.Controls.Add(CreateLabel("אפשרויות"), 0, 6);
        layout.Controls.Add(optionsPanel, 1, 6);
        layout.Controls.Add(buttonPanel, 1, 7);

        Controls.Add(layout);
    }

    private void SelectTag(int? tagId)
    {
        if (!tagId.HasValue)
        {
            return;
        }

        for (var i = 0; i < _tagComboBox.Items.Count; i++)
        {
            if (_tagComboBox.Items[i] is TagChoice choice && choice.Id == tagId)
            {
                _tagComboBox.SelectedIndex = i;
                return;
            }
        }
    }

    private void SelectDifficulty(StudyDifficulty difficulty)
    {
        for (var i = 0; i < _difficultyComboBox.Items.Count; i++)
        {
            if (_difficultyComboBox.Items[i] is DifficultyChoice choice && choice.Difficulty == difficulty)
            {
                _difficultyComboBox.SelectedIndex = i;
                return;
            }
        }
    }

    private void SelectOrderMode(ReviewSessionOrderMode orderMode)
    {
        for (var i = 0; i < _orderModeComboBox.Items.Count; i++)
        {
            if (_orderModeComboBox.Items[i] is OrderChoice choice && choice.OrderMode == orderMode)
            {
                _orderModeComboBox.SelectedIndex = i;
                return;
            }
        }
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
    }

    private sealed record TagChoice(int? Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record DifficultyChoice(StudyDifficulty Difficulty, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record OrderChoice(ReviewSessionOrderMode OrderMode, string Name)
    {
        public override string ToString() => Name;
    }
}
