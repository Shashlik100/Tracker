namespace TrackerApp;

public sealed class ReviewFilterControl : UserControl
{
    private readonly CheckBox _restrictToSelectedNodeCheckBox = new();
    private readonly CheckBox _dueOnlyCheckBox = new();
    private readonly CheckBox _failedRecentlyCheckBox = new();
    private readonly ComboBox _difficultyComboBox = new();
    private readonly ComboBox _tagComboBox = new();
    private readonly ComboBox _orderModeComboBox = new();
    private readonly Label _selectedNodeLabel = new();
    private readonly Label _resultSummaryLabel = new();
    private readonly Label _pausedSessionLabel = new();
    private readonly Button _resumeSessionButton = new();

    public event EventHandler? ReviewRequested;
    public event EventHandler? ResetRequested;
    public event EventHandler? ResumeRequested;

    public ReviewFilterControl()
    {
        BackColor = ClassicPalette.CardBackground;
        BorderStyle = BorderStyle.FixedSingle;
        Dock = DockStyle.Top;
        Height = 214;
        RightToLeft = RightToLeft.Yes;
        BuildLayout();
        UiLayoutHelper.ApplyRecursive(this);
    }

    public bool RestrictToSelectedNode => _restrictToSelectedNodeCheckBox.Checked;
    public bool DueOnly => _dueOnlyCheckBox.Checked;
    public bool FailedRecentlyOnly => _failedRecentlyCheckBox.Checked;
    public int? SelectedTagId => _tagComboBox.SelectedItem is TagChoice choice && choice.Id > 0 ? choice.Id : null;
    public StudyDifficulty SelectedDifficulty => _difficultyComboBox.SelectedItem is DifficultyChoice choice ? choice.Difficulty : StudyDifficulty.Any;
    public ReviewSessionOrderMode SelectedOrderMode => _orderModeComboBox.SelectedItem is OrderChoice choice ? choice.OrderMode : ReviewSessionOrderMode.Default;

    public void BindTags(IReadOnlyList<TagModel> tags)
    {
        var selectedId = SelectedTagId;
        _tagComboBox.Items.Clear();
        _tagComboBox.Items.Add(new TagChoice(null, "כל התגיות"));
        foreach (var tag in tags)
        {
            _tagComboBox.Items.Add(new TagChoice(tag.Id, tag.Name));
        }

        _tagComboBox.SelectedIndex = 0;
        if (selectedId.HasValue)
        {
            for (var index = 0; index < _tagComboBox.Items.Count; index++)
            {
                if (_tagComboBox.Items[index] is TagChoice choice && choice.Id == selectedId)
                {
                    _tagComboBox.SelectedIndex = index;
                    break;
                }
            }
        }
    }

    public void SetSelectedNodePath(string path)
    {
        _selectedNodeLabel.Text = string.IsNullOrWhiteSpace(path) ? "כל הספרייה" : path;
    }

    public void SetResultSummary(string summary)
    {
        _resultSummaryLabel.Text = summary;
    }

    public void SetPausedSessionSummary(string summary, bool hasPausedSession)
    {
        _pausedSessionLabel.Text = hasPausedSession ? summary : "אין סשן מושהה.";
        _resumeSessionButton.Enabled = hasPausedSession;
    }

    public void SetFilterState(bool restrictToSelectedNode, bool dueOnly, bool failedRecentlyOnly, StudyDifficulty difficulty, int? tagId, ReviewSessionOrderMode orderMode = ReviewSessionOrderMode.Default)
    {
        _restrictToSelectedNodeCheckBox.Checked = restrictToSelectedNode;
        _dueOnlyCheckBox.Checked = dueOnly;
        _failedRecentlyCheckBox.Checked = failedRecentlyOnly;

        for (var index = 0; index < _difficultyComboBox.Items.Count; index++)
        {
            if (_difficultyComboBox.Items[index] is DifficultyChoice choice && choice.Difficulty == difficulty)
            {
                _difficultyComboBox.SelectedIndex = index;
                break;
            }
        }

        for (var index = 0; index < _orderModeComboBox.Items.Count; index++)
        {
            if (_orderModeComboBox.Items[index] is OrderChoice choice && choice.OrderMode == orderMode)
            {
                _orderModeComboBox.SelectedIndex = index;
                break;
            }
        }

        _tagComboBox.SelectedIndex = 0;
        if (tagId.HasValue)
        {
            for (var index = 0; index < _tagComboBox.Items.Count; index++)
            {
                if (_tagComboBox.Items[index] is TagChoice choice && choice.Id == tagId)
                {
                    _tagComboBox.SelectedIndex = index;
                    break;
                }
            }
        }
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 3,
            Padding = new Padding(8),
            RightToLeft = RightToLeft.Yes
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152F));

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));

        _selectedNodeLabel.Dock = DockStyle.Fill;
        _selectedNodeLabel.TextAlign = ContentAlignment.MiddleRight;
        _selectedNodeLabel.BorderStyle = BorderStyle.FixedSingle;
        _selectedNodeLabel.BackColor = Color.White;

        _tagComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _tagComboBox.Dock = DockStyle.Fill;

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

        _restrictToSelectedNodeCheckBox.Text = "רק בצומת הנבחר";
        _restrictToSelectedNodeCheckBox.Checked = true;
        _restrictToSelectedNodeCheckBox.TextAlign = ContentAlignment.MiddleRight;
        _restrictToSelectedNodeCheckBox.Dock = DockStyle.Fill;

        _dueOnlyCheckBox.Text = "רק לביצוע היום";
        _dueOnlyCheckBox.Checked = true;
        _dueOnlyCheckBox.TextAlign = ContentAlignment.MiddleRight;
        _dueOnlyCheckBox.Dock = DockStyle.Fill;

        _failedRecentlyCheckBox.Text = "נכשלו לאחרונה";
        _failedRecentlyCheckBox.TextAlign = ContentAlignment.MiddleRight;
        _failedRecentlyCheckBox.Dock = DockStyle.Fill;

        _resultSummaryLabel.Dock = DockStyle.Fill;
        _resultSummaryLabel.TextAlign = ContentAlignment.MiddleRight;
        _resultSummaryLabel.Font = new Font(Font, FontStyle.Bold);
        _resultSummaryLabel.Padding = new Padding(6, 0, 6, 0);

        _pausedSessionLabel.Dock = DockStyle.Fill;
        _pausedSessionLabel.TextAlign = ContentAlignment.MiddleRight;
        _pausedSessionLabel.BorderStyle = BorderStyle.FixedSingle;
        _pausedSessionLabel.BackColor = Color.White;

        var reviewButton = CreateButton("התחל חזרה");
        reviewButton.Click += (_, _) => ReviewRequested?.Invoke(this, EventArgs.Empty);

        _resumeSessionButton.Text = "המשך סשן";
        _resumeSessionButton.Dock = DockStyle.Fill;
        _resumeSessionButton.Enabled = false;
        UiLayoutHelper.StyleActionButton(_resumeSessionButton, 142, 42);
        _resumeSessionButton.Click += (_, _) => ResumeRequested?.Invoke(this, EventArgs.Empty);

        var resetButton = CreateButton("איפוס");
        resetButton.Click += (_, _) =>
        {
            _tagComboBox.SelectedIndex = 0;
            _difficultyComboBox.SelectedIndex = 0;
            _orderModeComboBox.SelectedIndex = 0;
            _restrictToSelectedNodeCheckBox.Checked = true;
            _dueOnlyCheckBox.Checked = true;
            _failedRecentlyCheckBox.Checked = false;
            ResetRequested?.Invoke(this, EventArgs.Empty);
        };

        layout.Controls.Add(CreateLabel("צומת"), 0, 0);
        layout.Controls.Add(_selectedNodeLabel, 1, 0);
        layout.SetColumnSpan(_selectedNodeLabel, 2);
        layout.Controls.Add(CreateLabel("תגית"), 3, 0);
        layout.Controls.Add(_tagComboBox, 4, 0);
        layout.Controls.Add(reviewButton, 5, 0);
        layout.Controls.Add(_resumeSessionButton, 6, 0);

        layout.Controls.Add(CreateLabel("קושי"), 0, 1);
        layout.Controls.Add(_difficultyComboBox, 1, 1);
        layout.Controls.Add(CreateLabel("סדר"), 2, 1);
        layout.Controls.Add(_orderModeComboBox, 3, 1);
        layout.Controls.Add(_restrictToSelectedNodeCheckBox, 4, 1);
        layout.Controls.Add(_dueOnlyCheckBox, 5, 1);
        layout.Controls.Add(_failedRecentlyCheckBox, 6, 1);

        layout.Controls.Add(CreateLabel("מושהה"), 0, 2);
        layout.Controls.Add(_pausedSessionLabel, 1, 2);
        layout.SetColumnSpan(_pausedSessionLabel, 4);
        layout.Controls.Add(_resultSummaryLabel, 5, 2);
        layout.Controls.Add(resetButton, 6, 2);

        Controls.Add(layout);
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes
        };
    }

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill
        };
        UiLayoutHelper.StyleActionButton(button, 142, 42);
        return button;
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
