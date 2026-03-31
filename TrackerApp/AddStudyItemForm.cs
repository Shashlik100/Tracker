namespace TrackerApp;

public sealed class AddStudyItemForm : Form
{
    private readonly ComboBox _subjectComboBox = new();
    private readonly ComboBox _difficultyComboBox = new();
    private readonly TextBox _topicTextBox = new();
    private readonly TextBox _questionTextBox = new();
    private readonly TextBox _answerTextBox = new();
    private readonly CheckedListBox _tagsListBox = new();
    private readonly Label _emptyTagsLabel = new();
    private readonly Label _tagsSummaryLabel = new();
    private readonly Func<IReadOnlyList<TagModel>> _tagLoader;
    private readonly Func<IWin32Window, string?, TagModel?>? _tagCreator;
    private readonly bool _isEditMode;

    public AddStudyItemForm(
        IReadOnlyList<SubjectNodeModel> subjects,
        IReadOnlyList<TagModel> tags,
        int? initialSubjectId,
        StudyItemModel? existingItem = null,
        Func<IReadOnlyList<TagModel>>? tagLoader = null,
        Func<IWin32Window, string?, TagModel?>? tagCreator = null)
    {
        _isEditMode = existingItem is not null;
        _tagLoader = tagLoader ?? (() => tags);
        _tagCreator = tagCreator;

        Text = _isEditMode ? "עריכת כרטיס" : "כרטיס חדש";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(820, 712);
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = false;
        UiLayoutHelper.ApplyFormDefaults(this);

        BuildLayout(subjects, tags, initialSubjectId, existingItem);
        UiLayoutHelper.ApplyRecursive(this);
    }

    public int SelectedSubjectId => ((SubjectChoice)_subjectComboBox.SelectedItem!).Id;
    public string Topic => _topicTextBox.Text.Trim();
    public string Question => _questionTextBox.Text.Trim();
    public string Answer => _answerTextBox.Text.Trim();
    public StudyDifficulty? SelectedDifficulty =>
        _difficultyComboBox.SelectedItem is DifficultyChoice choice && choice.Difficulty != StudyDifficulty.Any
            ? choice.Difficulty
            : null;

    public IReadOnlyList<int> SelectedTagIds =>
        _tagsListBox.CheckedItems.OfType<TagModel>().Select(tag => tag.Id).OrderBy(id => id).ToArray();

    public bool HasVisibleTags => _tagsListBox.Items.Count > 0;

    public bool HasTagNamed(string tagName) =>
        _tagsListBox.Items.OfType<TagModel>().Any(tag => string.Equals(tag.Name, tagName, StringComparison.OrdinalIgnoreCase));

    public bool IsTagChecked(int tagId) =>
        _tagsListBox.CheckedItems.OfType<TagModel>().Any(tag => tag.Id == tagId);

    public TagModel? AutomationCreateTag(string tagName)
    {
        if (_tagCreator is null)
        {
            return null;
        }

        var selectedIds = SelectedTagIds.ToHashSet();
        var createdTag = _tagCreator(this, tagName);
        if (createdTag is null)
        {
            return null;
        }

        selectedIds.Add(createdTag.Id);
        ReloadTags(selectedIds);
        return createdTag;
    }

    public bool AutomationSelectTag(string tagName)
    {
        for (var index = 0; index < _tagsListBox.Items.Count; index++)
        {
            if (_tagsListBox.Items[index] is TagModel tag &&
                string.Equals(tag.Name, tagName, StringComparison.OrdinalIgnoreCase))
            {
                _tagsListBox.SetItemChecked(index, true);
                UpdateTagsState();
                return true;
            }
        }

        return false;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            if (_subjectComboBox.SelectedItem is null ||
                string.IsNullOrWhiteSpace(_topicTextBox.Text) ||
                string.IsNullOrWhiteSpace(_questionTextBox.Text) ||
                string.IsNullOrWhiteSpace(_answerTextBox.Text))
            {
                MessageBox.Show(
                    this,
                    "יש למלא מיקום בספרייה, נושא, שאלה ותשובה.",
                    "שדות חסרים",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                e.Cancel = true;
            }
        }

        base.OnFormClosing(e);
    }

    private void BuildLayout(IReadOnlyList<SubjectNodeModel> subjects, IReadOnlyList<TagModel> tags, int? initialSubjectId, StudyItemModel? existingItem)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(12)
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 164F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 206F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _subjectComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _subjectComboBox.Dock = DockStyle.Fill;
        foreach (var subject in subjects)
        {
            var display = string.IsNullOrWhiteSpace(subject.DisplayPath) ? subject.Name : subject.DisplayPath;
            _subjectComboBox.Items.Add(new SubjectChoice(subject.Id, display.Replace(" > ", " / ", StringComparison.Ordinal)));
        }

        if (_subjectComboBox.Items.Count > 0)
        {
            _subjectComboBox.SelectedIndex = 0;
        }

        var targetSubjectId = existingItem?.SubjectId ?? initialSubjectId;
        if (targetSubjectId.HasValue)
        {
            for (var index = 0; index < _subjectComboBox.Items.Count; index++)
            {
                if (_subjectComboBox.Items[index] is SubjectChoice choice && choice.Id == targetSubjectId.Value)
                {
                    _subjectComboBox.SelectedIndex = index;
                    break;
                }
            }
        }

        _difficultyComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _difficultyComboBox.Dock = DockStyle.Fill;
        _difficultyComboBox.Items.AddRange(
        [
            new DifficultyChoice(StudyDifficulty.Any, "ללא קושי ידני"),
            new DifficultyChoice(StudyDifficulty.Hard, "קשה"),
            new DifficultyChoice(StudyDifficulty.Medium, "בינונית"),
            new DifficultyChoice(StudyDifficulty.Easy, "קלה")
        ]);
        _difficultyComboBox.SelectedIndex = 0;
        if (existingItem is not null && Enum.TryParse<StudyDifficulty>(existingItem.ManualDifficulty, true, out var parsed))
        {
            for (var index = 0; index < _difficultyComboBox.Items.Count; index++)
            {
                if (_difficultyComboBox.Items[index] is DifficultyChoice choice && choice.Difficulty == parsed)
                {
                    _difficultyComboBox.SelectedIndex = index;
                    break;
                }
            }
        }

        _topicTextBox.Dock = DockStyle.Fill;
        _topicTextBox.RightToLeft = RightToLeft.Yes;
        _topicTextBox.Text = existingItem?.Topic ?? string.Empty;

        ConfigureMultilineBox(_questionTextBox, existingItem?.Question);
        ConfigureMultilineBox(_answerTextBox, existingItem?.Answer);

        root.Controls.Add(CreatePromptLabel("מיקום"), 0, 0);
        root.Controls.Add(_subjectComboBox, 1, 0);
        root.Controls.Add(CreatePromptLabel("נושא"), 0, 1);
        root.Controls.Add(_topicTextBox, 1, 1);
        root.Controls.Add(CreatePromptLabel("קושי"), 0, 2);
        root.Controls.Add(_difficultyComboBox, 1, 2);
        root.Controls.Add(CreatePromptLabel("שאלה"), 0, 3);
        root.Controls.Add(_questionTextBox, 1, 3);
        root.Controls.Add(CreatePromptLabel("תשובה"), 0, 4);
        root.Controls.Add(_answerTextBox, 1, 4);

        var bottomLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));

        var tagsGroup = BuildTagsGroup(tags, existingItem?.Tags.Select(tag => tag.Id).ToHashSet() ?? []);

        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 8, 0, 0),
            RightToLeft = RightToLeft.Yes
        };
        buttonPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        buttonPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));

        var okButton = new Button
        {
            Text = _isEditMode ? "שמור שינויים" : "שמור",
            DialogResult = DialogResult.OK,
            Width = 124,
            Height = 38
        };
        UiLayoutHelper.StyleActionButton(okButton, 124, 38);

        var cancelButton = new Button
        {
            Text = "ביטול",
            DialogResult = DialogResult.Cancel,
            Width = 108,
            Height = 38
        };
        UiLayoutHelper.StyleActionButton(cancelButton, 108, 38);

        okButton.Dock = DockStyle.Fill;
        cancelButton.Dock = DockStyle.Fill;
        buttonPanel.Controls.Add(okButton, 0, 0);
        buttonPanel.Controls.Add(cancelButton, 0, 1);
        bottomLayout.Controls.Add(tagsGroup, 0, 0);
        bottomLayout.Controls.Add(buttonPanel, 1, 0);
        root.Controls.Add(bottomLayout, 1, 5);

        Controls.Add(root);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private GroupBox BuildTagsGroup(IReadOnlyList<TagModel> tags, IReadOnlyCollection<int> selectedTagIds)
    {
        var tagsGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "תגיות",
            RightToLeft = RightToLeft.Yes
        };

        var tagsToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 2)
        };

        var addTagButton = new Button
        {
            Text = "הוסף תגית",
            Width = 120,
            Height = 36
        };
        UiLayoutHelper.StyleActionButton(addTagButton, 120, 36);
        addTagButton.Click += (_, _) => HandleAddTag();

        var refreshTagsButton = new Button
        {
            Text = "רענן תגיות",
            Width = 120,
            Height = 36
        };
        UiLayoutHelper.StyleActionButton(refreshTagsButton, 120, 36);
        refreshTagsButton.Click += (_, _) => ReloadTags(SelectedTagIds);

        tagsToolbar.Controls.Add(addTagButton);
        tagsToolbar.Controls.Add(refreshTagsButton);

        var tagsContent = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        _tagsListBox.Dock = DockStyle.Fill;
        _tagsListBox.CheckOnClick = true;
        _tagsListBox.BorderStyle = BorderStyle.None;
        _tagsListBox.ItemCheck += (_, _) =>
        {
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(UpdateTagsState));
            }
            else
            {
                UpdateTagsState();
            }
        };

        _emptyTagsLabel.Dock = DockStyle.Fill;
        _emptyTagsLabel.TextAlign = ContentAlignment.MiddleCenter;
        _emptyTagsLabel.Text =
            "אין תגיות עדיין." + Environment.NewLine +
            "לחצו על \"הוסף תגית\" כדי ליצור תגית חדשה.";
        _emptyTagsLabel.RightToLeft = RightToLeft.Yes;

        _tagsSummaryLabel.Dock = DockStyle.Fill;
        _tagsSummaryLabel.TextAlign = ContentAlignment.MiddleRight;
        _tagsSummaryLabel.RightToLeft = RightToLeft.Yes;

        _tagsSummaryLabel.Dock = DockStyle.Bottom;
        _tagsSummaryLabel.Height = 22;

        tagsContent.Controls.Add(_tagsListBox);
        tagsContent.Controls.Add(_emptyTagsLabel);
        tagsGroup.Controls.Add(tagsContent);
        tagsGroup.Controls.Add(_tagsSummaryLabel);
        tagsGroup.Controls.Add(tagsToolbar);

        PopulateTags(tags, selectedTagIds);
        return tagsGroup;
    }

    private void HandleAddTag()
    {
        if (_tagCreator is null)
        {
            MessageBox.Show(
                this,
                "לא הוגדר מסלול יצירת תגיות מתוך חלון זה.",
                "יצירת תגית אינה זמינה",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return;
        }

        var selectedIds = SelectedTagIds.ToHashSet();
        var createdTag = _tagCreator(this, null);
        if (createdTag is null)
        {
            return;
        }

        selectedIds.Add(createdTag.Id);
        ReloadTags(selectedIds);
    }

    private void ReloadTags(IReadOnlyCollection<int> selectedTagIds)
    {
        PopulateTags(_tagLoader(), selectedTagIds);
    }

    private void PopulateTags(IReadOnlyList<TagModel> tags, IReadOnlyCollection<int> selectedTagIds)
    {
        _tagsListBox.BeginUpdate();
        _tagsListBox.Items.Clear();
        foreach (var tag in tags)
        {
            var index = _tagsListBox.Items.Add(tag);
            if (selectedTagIds.Contains(tag.Id))
            {
                _tagsListBox.SetItemChecked(index, true);
            }
        }

        _tagsListBox.EndUpdate();
        UpdateTagsState();
    }

    private void UpdateTagsState()
    {
        var totalTags = _tagsListBox.Items.Count;
        var selectedCount = _tagsListBox.CheckedItems.Count;
        var hasTags = totalTags > 0;

        _tagsListBox.Visible = hasTags;
        _emptyTagsLabel.Visible = !hasTags;
        _tagsSummaryLabel.Text = hasTags
            ? $"זמינות {totalTags} תגיות | נבחרו {selectedCount}"
            : "אין תגיות זמינות כרגע.";
    }

    private static Label CreatePromptLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes
        };
    }

    private static void ConfigureMultilineBox(TextBox textBox, string? value)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Multiline = true;
        textBox.ScrollBars = ScrollBars.Vertical;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.RightToLeft = RightToLeft.Yes;
        textBox.Text = value ?? string.Empty;
    }

    private sealed record SubjectChoice(int Id, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record DifficultyChoice(StudyDifficulty Difficulty, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
