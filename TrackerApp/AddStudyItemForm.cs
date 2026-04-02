namespace TrackerApp;

public sealed class AddStudyItemForm : Form
{
    private readonly ComboBox _subjectComboBox = new();
    private readonly ComboBox _difficultyComboBox = new();
    private readonly TextBox _topicTextBox = new();
    private readonly TextBox _sourceTextBox = new();
    private readonly TextBox _pshatTextBox = new();
    private readonly TextBox _kushyaTextBox = new();
    private readonly TextBox _terutzTextBox = new();
    private readonly TextBox _chidushTextBox = new();
    private readonly TextBox _personalSummaryTextBox = new();
    private readonly TextBox _reviewNotesTextBox = new();
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

        Text = _isEditMode ? "עריכת יחידת לימוד" : "יחידת לימוד חדשה";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(920, 860);
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        UiLayoutHelper.ApplyFormDefaults(this);

        BuildLayout(subjects, tags, initialSubjectId, existingItem);
        UiLayoutHelper.ApplyRecursive(this);
    }

    public int SelectedSubjectId => ((SubjectChoice)_subjectComboBox.SelectedItem!).Id;
    public string Topic => _topicTextBox.Text.Trim();
    public string SourceText => _sourceTextBox.Text.Trim();
    public string PshatText => _pshatTextBox.Text.Trim();
    public string KushyaText => _kushyaTextBox.Text.Trim();
    public string TerutzText => _terutzTextBox.Text.Trim();
    public string ChidushText => _chidushTextBox.Text.Trim();
    public string PersonalSummary => _personalSummaryTextBox.Text.Trim();
    public string ReviewNotes => _reviewNotesTextBox.Text.Trim();
    public string Question => SourceText;
    public string Answer => PersonalSummary;
    public StudyDifficulty? SelectedDifficulty =>
        _difficultyComboBox.SelectedItem is DifficultyChoice choice && choice.Difficulty != StudyDifficulty.Any
            ? choice.Difficulty
            : null;

    public StudyItemDraftModel Draft => new()
    {
        SubjectId = SelectedSubjectId,
        Topic = Topic,
        SourceText = SourceText,
        PshatText = PshatText,
        KushyaText = KushyaText,
        TerutzText = TerutzText,
        ChidushText = ChidushText,
        PersonalSummary = PersonalSummary,
        ReviewNotes = ReviewNotes,
        ManualDifficulty = SelectedDifficulty
    };

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
            var hasCoreContent =
                !string.IsNullOrWhiteSpace(SourceText) ||
                !string.IsNullOrWhiteSpace(PshatText) ||
                !string.IsNullOrWhiteSpace(KushyaText) ||
                !string.IsNullOrWhiteSpace(TerutzText) ||
                !string.IsNullOrWhiteSpace(ChidushText) ||
                !string.IsNullOrWhiteSpace(PersonalSummary);

            if (_subjectComboBox.SelectedItem is null ||
                string.IsNullOrWhiteSpace(Topic) ||
                !hasCoreContent)
            {
                MessageBox.Show(
                    this,
                    "יש למלא מיקום בספרייה, נושא, ולפחות אחד מאזורי הלימוד המרכזיים.",
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
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12),
            RightToLeft = RightToLeft.Yes
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));

        var scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = ClassicPalette.PanelBackground
        };

        var formLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 11,
            RightToLeft = RightToLeft.Yes,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        for (var index = 0; index < 8; index++)
        {
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, index == 7 ? 148F : 110F));
        }

        ConfigureSubjectChoices(subjects, initialSubjectId, existingItem);
        ConfigureDifficultyChoices(existingItem);

        _topicTextBox.Dock = DockStyle.Fill;
        _topicTextBox.Text = existingItem?.Topic ?? string.Empty;

        ConfigureMultilineBox(_sourceTextBox, existingItem?.SourceText);
        ConfigureMultilineBox(_pshatTextBox, existingItem?.PshatText);
        ConfigureMultilineBox(_kushyaTextBox, existingItem?.KushyaText);
        ConfigureMultilineBox(_terutzTextBox, existingItem?.TerutzText);
        ConfigureMultilineBox(_chidushTextBox, existingItem?.ChidushText);
        ConfigureMultilineBox(_personalSummaryTextBox, existingItem?.PersonalSummary);
        ConfigureMultilineBox(_reviewNotesTextBox, existingItem?.ReviewNotes);

        formLayout.Controls.Add(CreatePromptLabel("מיקום בספרייה"), 0, 0);
        formLayout.Controls.Add(_subjectComboBox, 1, 0);
        formLayout.Controls.Add(CreatePromptLabel("נושא"), 0, 1);
        formLayout.Controls.Add(_topicTextBox, 1, 1);
        formLayout.Controls.Add(CreatePromptLabel("קושי ידני"), 0, 2);
        formLayout.Controls.Add(_difficultyComboBox, 1, 2);
        formLayout.Controls.Add(CreatePromptLabel("מקור"), 0, 3);
        formLayout.Controls.Add(_sourceTextBox, 1, 3);
        formLayout.Controls.Add(CreatePromptLabel("פשט"), 0, 4);
        formLayout.Controls.Add(_pshatTextBox, 1, 4);
        formLayout.Controls.Add(CreatePromptLabel("קושיה"), 0, 5);
        formLayout.Controls.Add(_kushyaTextBox, 1, 5);
        formLayout.Controls.Add(CreatePromptLabel("תירוץ"), 0, 6);
        formLayout.Controls.Add(_terutzTextBox, 1, 6);
        formLayout.Controls.Add(CreatePromptLabel("חידוש"), 0, 7);
        formLayout.Controls.Add(_chidushTextBox, 1, 7);
        formLayout.Controls.Add(CreatePromptLabel("סיכום אישי"), 0, 8);
        formLayout.Controls.Add(_personalSummaryTextBox, 1, 8);
        formLayout.Controls.Add(CreatePromptLabel("הערות חזרה"), 0, 9);
        formLayout.Controls.Add(_reviewNotesTextBox, 1, 9);

        var tagsGroup = BuildTagsGroup(tags, existingItem?.Tags.Select(tag => tag.Id).ToHashSet() ?? []);
        tagsGroup.Height = 148;
        formLayout.Controls.Add(CreatePromptLabel("תגיות"), 0, 10);
        formLayout.Controls.Add(tagsGroup, 1, 10);

        scrollHost.Controls.Add(formLayout);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var okButton = new Button
        {
            Text = _isEditMode ? "שמור שינויים" : "שמור יחידת לימוד",
            DialogResult = DialogResult.OK
        };
        UiLayoutHelper.StyleActionButton(okButton, 148, 40);
        okButton.Dock = DockStyle.Fill;

        var cancelButton = new Button
        {
            Text = "ביטול",
            DialogResult = DialogResult.Cancel
        };
        UiLayoutHelper.StyleActionButton(cancelButton, 132, 40);
        cancelButton.Dock = DockStyle.Fill;

        var hintLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "מלאו את אזורי הלימוד הדרושים לכם. אזורים ריקים יוסתרו בתצוגת היחידה.",
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes,
            ForeColor = Color.FromArgb(54, 77, 77)
        };

        footer.Controls.Add(okButton, 0, 0);
        footer.Controls.Add(cancelButton, 1, 0);
        footer.Controls.Add(hintLabel, 2, 0);

        root.Controls.Add(scrollHost, 0, 0);
        root.Controls.Add(footer, 0, 1);

        Controls.Add(root);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void ConfigureSubjectChoices(IReadOnlyList<SubjectNodeModel> subjects, int? initialSubjectId, StudyItemModel? existingItem)
    {
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
        if (!targetSubjectId.HasValue)
        {
            return;
        }

        for (var index = 0; index < _subjectComboBox.Items.Count; index++)
        {
            if (_subjectComboBox.Items[index] is SubjectChoice choice && choice.Id == targetSubjectId.Value)
            {
                _subjectComboBox.SelectedIndex = index;
                break;
            }
        }
    }

    private void ConfigureDifficultyChoices(StudyItemModel? existingItem)
    {
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

        if (existingItem is null || !Enum.TryParse<StudyDifficulty>(existingItem.ManualDifficulty, true, out var parsed))
        {
            return;
        }

        for (var index = 0; index < _difficultyComboBox.Items.Count; index++)
        {
            if (_difficultyComboBox.Items[index] is DifficultyChoice choice && choice.Difficulty == parsed)
            {
                _difficultyComboBox.SelectedIndex = index;
                break;
            }
        }
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

        _tagsSummaryLabel.Dock = DockStyle.Bottom;
        _tagsSummaryLabel.Height = 22;
        _tagsSummaryLabel.TextAlign = ContentAlignment.MiddleRight;

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
            TextAlign = ContentAlignment.MiddleRight
        };
    }

    private static void ConfigureMultilineBox(TextBox textBox, string? value)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Multiline = true;
        textBox.ScrollBars = ScrollBars.Vertical;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Text = value ?? string.Empty;
        textBox.RightToLeft = RightToLeft.Yes;
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
