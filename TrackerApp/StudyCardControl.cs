namespace TrackerApp;

public sealed class StudyCardControl : UserControl
{
    private readonly StudyItemModel _item;
    private readonly CheckBox _selectionCheckBox = new();

    public event EventHandler<ReviewRating>? RatingClicked;
    public event EventHandler<int>? EditClicked;
    public event EventHandler<int>? DeleteClicked;
    public event EventHandler<int>? TagsClicked;

    public StudyCardControl(StudyItemModel item)
    {
        _item = item;
        BackColor = ClassicPalette.CardBackground;
        Margin = new Padding(8, 6, 8, 6);
        Padding = new Padding(10);
        Height = 620;
        MinimumSize = new Size(660, 620);
        RightToLeft = RightToLeft.Yes;
        BuildLayout();
        UiLayoutHelper.ApplyRecursive(this);
    }

    public int ItemId => _item.Id;

    public bool IsSelected
    {
        get => _selectionCheckBox.Checked;
        set => _selectionCheckBox.Checked = value;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var outerPen = new Pen(ClassicPalette.Teal, 2F);
        using var innerPen = new Pen(ClassicPalette.TealLight, 1F);
        e.Graphics.DrawRectangle(outerPen, 0, 0, Width - 1, Height - 1);
        e.Graphics.DrawRectangle(innerPen, 3, 3, Width - 7, Height - 7);
    }

    private void BuildLayout()
    {
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            RightToLeft = RightToLeft.Yes,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 136F));

        var contentSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            FixedPanel = FixedPanel.None,
            IsSplitterFixed = false,
            BorderStyle = BorderStyle.None,
            Panel1MinSize = 80,
            Panel2MinSize = 80,
            RightToLeft = RightToLeft.Yes
        };
        contentSplit.Resize += (_, _) =>
        {
            var availableHeight = contentSplit.Height - contentSplit.SplitterWidth;
            if (availableHeight <= contentSplit.Panel1MinSize + contentSplit.Panel2MinSize)
            {
                return;
            }

            var target = Math.Max(contentSplit.Panel1MinSize, availableHeight / 2);
            var maxTarget = availableHeight - contentSplit.Panel2MinSize;
            contentSplit.SplitterDistance = Math.Min(target, maxTarget);
        };
        contentSplit.Panel1.Controls.Add(CreateSection("שאלה", _item.Question, 84));
        contentSplit.Panel2.Controls.Add(CreateSection("תשובה", _item.Answer, 84));

        rootLayout.Controls.Add(CreateHeaderPanel(), 0, 0);
        rootLayout.Controls.Add(CreateTopicLabel(), 0, 1);
        rootLayout.Controls.Add(CreateTagsPanel(), 0, 2);
        rootLayout.Controls.Add(contentSplit, 0, 3);
        rootLayout.Controls.Add(CreateMetaLayout(), 0, 4);
        rootLayout.Controls.Add(CreateActionsPanel(), 0, 5);

        Controls.Add(rootLayout);
    }

    private Control CreateHeaderPanel()
    {
        var headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ClassicPalette.Teal
        };

        _selectionCheckBox.Dock = DockStyle.Left;
        _selectionCheckBox.Width = 32;
        _selectionCheckBox.BackColor = Color.Transparent;

        var dateLabel = new Label
        {
            Dock = DockStyle.Left,
            Width = 120,
            Text = _item.DueDate.ToString("dd/MM/yyyy"),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            RightToLeft = RightToLeft.Yes
        };

        var pathLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = _item.SubjectPath.Replace(" > ", " / ", StringComparison.Ordinal),
            ForeColor = Color.White,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes
        };

        headerPanel.Controls.Add(pathLabel);
        headerPanel.Controls.Add(dateLabel);
        headerPanel.Controls.Add(_selectionCheckBox);
        return headerPanel;
    }

    private Control CreateTopicLabel()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = _item.Topic,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes
        };
    }

    private Control CreateTagsPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes
        };

        var caption = new Label
        {
            Dock = DockStyle.Right,
            Width = 64,
            Text = "תגיות:",
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font(Font, FontStyle.Bold),
            RightToLeft = RightToLeft.Yes
        };

        var tagsText = _item.Tags.Count == 0
            ? "ללא תגיות"
            : string.Join(" | ", _item.Tags.Select(tag => tag.Name));

        var valueLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = tagsText,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes
        };

        panel.Controls.Add(valueLabel);
        panel.Controls.Add(caption);
        return panel;
    }

    private Control CreateMetaLayout()
    {
        var metaLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            RightToLeft = RightToLeft.Yes
        };
        metaLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        metaLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        metaLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        metaLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

        metaLayout.Controls.Add(CreateMetaLabel($"תאריך יצירה: {_item.CreatedAt:dd/MM/yyyy}"), 0, 0);
        metaLayout.Controls.Add(CreateMetaLabel($"חזרה: {(_item.LastRating.Length == 0 ? "-" : TranslateRating(_item.LastRating))}"), 1, 0);
        metaLayout.Controls.Add(CreateMetaLabel($"מרווח: {_item.IntervalDays:0.#} ימים"), 2, 0);
        metaLayout.Controls.Add(CreateMetaLabel($"EF: {_item.EaseFactor:0.00}"), 3, 0);
        metaLayout.Controls.Add(CreateMetaLabel($"שלב: {_item.Level}"), 0, 1);
        metaLayout.Controls.Add(CreateMetaLabel($"חזרות: {_item.TotalReviews}"), 1, 1);
        metaLayout.Controls.Add(CreateMetaLabel($"קושי: {TranslateDifficulty(_item.Difficulty)}"), 2, 1);
        metaLayout.Controls.Add(CreateMetaLabel(_item.IsMastered ? "נלמד היטב" : "בתהליך"), 3, 1);

        return metaLayout;
    }

    private Control CreateActionsPanel()
    {
        var actionsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            RightToLeft = RightToLeft.Yes,
            BackColor = ClassicPalette.CardBackground,
            Padding = new Padding(0, 8, 0, 8)
        };
        actionsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        actionsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));

        var manageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        manageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        manageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        manageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));

        var deleteButton = new Button { Text = "מחק", Width = 84, Height = 38 };
        deleteButton.Click += (_, _) => DeleteClicked?.Invoke(this, _item.Id);
        var editButton = new Button { Text = "ערוך", Width = 84, Height = 38 };
        editButton.Click += (_, _) => EditClicked?.Invoke(this, _item.Id);
        var tagsButton = new Button { Text = "תגיות", Width = 92, Height = 38 };
        tagsButton.Click += (_, _) => TagsClicked?.Invoke(this, _item.Id);
        UiLayoutHelper.StyleActionButton(deleteButton, 84, 38);
        UiLayoutHelper.StyleActionButton(editButton, 84, 38);
        UiLayoutHelper.StyleActionButton(tagsButton, 92, 38);

        deleteButton.Dock = DockStyle.Fill;
        editButton.Dock = DockStyle.Fill;
        tagsButton.Dock = DockStyle.Fill;
        manageLayout.Controls.Add(deleteButton, 0, 0);
        manageLayout.Controls.Add(editButton, 1, 0);
        manageLayout.Controls.Add(tagsButton, 2, 0);

        var ratingsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        for (var index = 0; index < 5; index++)
        {
            ratingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        }

        var perfectButton = CreateRatingButton("מצוין", ReviewRating.Perfect, Color.FromArgb(202, 222, 240));
        var easyButton = CreateRatingButton("טוב", ReviewRating.Easy, Color.FromArgb(206, 232, 234));
        var goodButton = CreateRatingButton("בסדר", ReviewRating.Good, Color.FromArgb(221, 235, 210));
        var hardButton = CreateRatingButton("חלש", ReviewRating.Hard, Color.FromArgb(240, 228, 204));
        var againButton = CreateRatingButton("גרוע", ReviewRating.Again, Color.FromArgb(241, 216, 208));
        perfectButton.Dock = DockStyle.Fill;
        easyButton.Dock = DockStyle.Fill;
        goodButton.Dock = DockStyle.Fill;
        hardButton.Dock = DockStyle.Fill;
        againButton.Dock = DockStyle.Fill;
        ratingsLayout.Controls.Add(perfectButton, 0, 0);
        ratingsLayout.Controls.Add(easyButton, 1, 0);
        ratingsLayout.Controls.Add(goodButton, 2, 0);
        ratingsLayout.Controls.Add(hardButton, 3, 0);
        ratingsLayout.Controls.Add(againButton, 4, 0);

        actionsPanel.Controls.Add(manageLayout, 0, 0);
        actionsPanel.Controls.Add(ratingsLayout, 0, 1);
        return actionsPanel;
    }

    private Button CreateRatingButton(string text, ReviewRating rating, Color backColor)
    {
        var button = new Button
        {
            Text = text,
            Width = 92,
            Height = 34,
            BackColor = backColor
        };
        UiLayoutHelper.StyleActionButton(button, 92, 40);
        button.Click += (_, _) => RatingClicked?.Invoke(this, rating);
        return button;
    }

    private static Control CreateSection(string caption, string text, int textHeight)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes
        };

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = caption
        };
        UiLayoutHelper.StyleSectionHeader(header, 28);

        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            Text = text,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes
        };

        panel.Controls.Add(textBox);
        panel.Controls.Add(header);
        return panel;
    }

    private static Label CreateMetaLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes
        };
    }

    private static string TranslateRating(string value)
    {
        return value switch
        {
            "Again" => "גרוע",
            "Hard" => "חלש",
            "Good" => "בסדר",
            "Easy" => "טוב",
            "Perfect" => "מצוין",
            _ => value
        };
    }

    private static string TranslateDifficulty(StudyDifficulty difficulty)
    {
        return difficulty switch
        {
            StudyDifficulty.Easy => "קלה",
            StudyDifficulty.Medium => "בינונית",
            StudyDifficulty.Hard => "קשה",
            _ => "-"
        };
    }
}
