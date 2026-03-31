namespace TrackerApp;

public sealed class SearchFilterControl : UserControl
{
    private readonly TextBox _searchTextBox = new();
    private readonly TextBox _bookTextBox = new();
    private readonly TextBox _chapterTextBox = new();
    private readonly TextBox _verseTextBox = new();
    private readonly CheckBox _limitToSelectedNodeCheckBox = new();
    private readonly CheckBox _failedRecentlyCheckBox = new();
    private readonly ComboBox _tagComboBox = new();
    private readonly ComboBox _difficultyComboBox = new();
    private readonly Label _selectedNodeLabel = new();
    private readonly Label _resultSummaryLabel = new();

    public event EventHandler? SearchRequested;
    public event EventHandler? ClearRequested;

    public SearchFilterControl()
    {
        BackColor = ClassicPalette.CardBackground;
        BorderStyle = BorderStyle.FixedSingle;
        Dock = DockStyle.Top;
        Height = 196;
        RightToLeft = RightToLeft.Yes;
        BuildLayout();
        UiLayoutHelper.ApplyRecursive(this);
    }

    public string SearchText => _searchTextBox.Text.Trim();
    public string Book => _bookTextBox.Text.Trim();
    public string Chapter => _chapterTextBox.Text.Trim();
    public string Verse => _verseTextBox.Text.Trim();
    public bool LimitToSelectedNode => _limitToSelectedNodeCheckBox.Checked;
    public bool FailedRecentlyOnly => _failedRecentlyCheckBox.Checked;
    public int? SelectedTagId => _tagComboBox.SelectedItem is TagChoice choice && choice.Id > 0 ? choice.Id : null;
    public StudyDifficulty SelectedDifficulty => _difficultyComboBox.SelectedItem is DifficultyChoice choice ? choice.Difficulty : StudyDifficulty.Any;

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
        _selectedNodeLabel.Text = string.IsNullOrWhiteSpace(path) ? "ללא צומת נבחר" : path;
    }

    public void SetResultSummary(string summary)
    {
        _resultSummaryLabel.Text = summary;
    }

    public void SetSearchState(string searchText, bool limitToSelectedNode, int? tagId)
    {
        _searchTextBox.Text = searchText;
        _limitToSelectedNodeCheckBox.Checked = limitToSelectedNode;
        SelectTag(tagId);
    }

    public void SetAdvancedState(string searchText, string book, string chapter, string verse, int? tagId, StudyDifficulty difficulty, bool failedRecentlyOnly, bool limitToSelectedNode)
    {
        _searchTextBox.Text = searchText;
        _bookTextBox.Text = book;
        _chapterTextBox.Text = chapter;
        _verseTextBox.Text = verse;
        _failedRecentlyCheckBox.Checked = failedRecentlyOnly;
        _limitToSelectedNodeCheckBox.Checked = limitToSelectedNode;
        SelectTag(tagId);
        for (var index = 0; index < _difficultyComboBox.Items.Count; index++)
        {
            if (_difficultyComboBox.Items[index] is DifficultyChoice choice && choice.Difficulty == difficulty)
            {
                _difficultyComboBox.SelectedIndex = index;
                break;
            }
        }
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 3,
            Padding = new Padding(8)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));

        ConfigureBox(_searchTextBox);
        ConfigureBox(_bookTextBox);
        ConfigureBox(_chapterTextBox);
        ConfigureBox(_verseTextBox);

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

        _limitToSelectedNodeCheckBox.Text = "רק בצומת הנבחר";
        _limitToSelectedNodeCheckBox.Dock = DockStyle.Fill;
        _limitToSelectedNodeCheckBox.TextAlign = ContentAlignment.MiddleRight;
        _limitToSelectedNodeCheckBox.RightToLeft = RightToLeft.Yes;

        _failedRecentlyCheckBox.Text = "נכשלו לאחרונה";
        _failedRecentlyCheckBox.Dock = DockStyle.Fill;
        _failedRecentlyCheckBox.TextAlign = ContentAlignment.MiddleRight;

        _selectedNodeLabel.Dock = DockStyle.Fill;
        _selectedNodeLabel.TextAlign = ContentAlignment.MiddleRight;
        _selectedNodeLabel.BorderStyle = BorderStyle.FixedSingle;
        _selectedNodeLabel.BackColor = Color.White;

        _resultSummaryLabel.Dock = DockStyle.Fill;
        _resultSummaryLabel.TextAlign = ContentAlignment.MiddleRight;
        _resultSummaryLabel.Font = new Font(Font, FontStyle.Bold);
        _resultSummaryLabel.Padding = new Padding(6, 0, 6, 0);

        var searchButton = CreateButton("חפש");
        searchButton.Click += (_, _) => SearchRequested?.Invoke(this, EventArgs.Empty);
        var clearButton = CreateButton("נקה");
        clearButton.Click += (_, _) =>
        {
            _searchTextBox.Clear();
            _bookTextBox.Clear();
            _chapterTextBox.Clear();
            _verseTextBox.Clear();
            _tagComboBox.SelectedIndex = 0;
            _difficultyComboBox.SelectedIndex = 0;
            _limitToSelectedNodeCheckBox.Checked = false;
            _failedRecentlyCheckBox.Checked = false;
            ClearRequested?.Invoke(this, EventArgs.Empty);
        };

        layout.Controls.Add(CreateLabel("טקסט"), 0, 0);
        layout.Controls.Add(_searchTextBox, 1, 0);
        layout.Controls.Add(CreateLabel("ספר"), 2, 0);
        layout.Controls.Add(_bookTextBox, 3, 0);
        layout.Controls.Add(CreateLabel("פרק"), 4, 0);
        layout.Controls.Add(_chapterTextBox, 5, 0);
        layout.Controls.Add(searchButton, 6, 0);
        layout.Controls.Add(clearButton, 7, 0);

        layout.Controls.Add(CreateLabel("פסוק"), 0, 1);
        layout.Controls.Add(_verseTextBox, 1, 1);
        layout.Controls.Add(CreateLabel("תגית"), 2, 1);
        layout.Controls.Add(_tagComboBox, 3, 1);
        layout.Controls.Add(CreateLabel("קושי"), 4, 1);
        layout.Controls.Add(_difficultyComboBox, 5, 1);
        layout.Controls.Add(_limitToSelectedNodeCheckBox, 6, 1);
        layout.Controls.Add(_failedRecentlyCheckBox, 7, 1);

        layout.Controls.Add(CreateLabel("צומת"), 0, 2);
        layout.Controls.Add(_selectedNodeLabel, 1, 2);
        layout.SetColumnSpan(_selectedNodeLabel, 5);
        layout.Controls.Add(_resultSummaryLabel, 6, 2);
        layout.SetColumnSpan(_resultSummaryLabel, 2);

        Controls.Add(layout);
    }

    private void SelectTag(int? tagId)
    {
        _tagComboBox.SelectedIndex = 0;
        if (!tagId.HasValue)
        {
            return;
        }

        for (var index = 0; index < _tagComboBox.Items.Count; index++)
        {
            if (_tagComboBox.Items[index] is TagChoice choice && choice.Id == tagId)
            {
                _tagComboBox.SelectedIndex = index;
                break;
            }
        }
    }

    private static void ConfigureBox(TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.RightToLeft = RightToLeft.Yes;
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
        UiLayoutHelper.StyleActionButton(button, 108, 38);
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
}
