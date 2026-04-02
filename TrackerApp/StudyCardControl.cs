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
        Height = 760;
        MinimumSize = new Size(700, 760);
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

        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 136F));

        rootLayout.Controls.Add(CreateHeaderPanel(), 0, 0);
        rootLayout.Controls.Add(CreateTopicLabel(), 0, 1);
        rootLayout.Controls.Add(CreateTagsPanel(), 0, 2);
        rootLayout.Controls.Add(CreateContentPanel(), 0, 3);
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
            Width = 156,
            Text = $"חזרה הבאה: {_item.DueDate:dd/MM/yyyy}",
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleRight,
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
            Width = 82,
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

    private Control CreateContentPanel()
    {
        var sections = _item.GetDisplaySections();
        if (sections.Count == 0)
        {
            return CreateEmptyContentPanel();
        }

        var scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = ClassicPalette.CardBackground,
            RightToLeft = RightToLeft.Yes
        };

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 0,
            RightToLeft = RightToLeft.Yes,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        for (var index = 0; index < sections.Count; index++)
        {
            var (title, value) = sections[index];
            stack.RowCount++;
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.Controls.Add(CreateSection(title, value), 0, index);
        }

        scrollHost.Controls.Add(stack);
        return scrollHost;
    }

    private Control CreateEmptyContentPanel()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Text = "אין עדיין תוכן לימודי להצגה ביחידה זו.",
            TextAlign = ContentAlignment.MiddleCenter,
            RightToLeft = RightToLeft.Yes
        };
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

        metaLayout.Controls.Add(CreateMetaLabel($"נוצרה: {_item.CreatedAt:dd/MM/yyyy}"), 0, 0);
        metaLayout.Controls.Add(CreateMetaLabel($"דירוג אחרון: {(_item.LastRating.Length == 0 ? "-" : TranslateRating(_item.LastRating))}"), 1, 0);
        metaLayout.Controls.Add(CreateMetaLabel($"מרווח: {_item.IntervalDays:0.#} ימים"), 2, 0);
        metaLayout.Controls.Add(CreateMetaLabel($"EF: {_item.EaseFactor:0.00}"), 3, 0);
        metaLayout.Controls.Add(CreateMetaLabel($"שלב: {_item.Level}"), 0, 1);
        metaLayout.Controls.Add(CreateMetaLabel($"חזרות: {_item.TotalReviews}"), 1, 1);
        metaLayout.Controls.Add(CreateMetaLabel($"קושי: {TranslateDifficulty(_item.Difficulty)}"), 2, 1);
        metaLayout.Controls.Add(CreateMetaLabel(_item.IsMastered ? "מחזור יציב" : "עדיין בתהליך"), 3, 1);

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

        var deleteButton = new Button { Text = "מחק יחידה", Width = 106, Height = 38 };
        deleteButton.Click += (_, _) => DeleteClicked?.Invoke(this, _item.Id);
        var editButton = new Button { Text = "ערוך יחידה", Width = 114, Height = 38 };
        editButton.Click += (_, _) => EditClicked?.Invoke(this, _item.Id);
        var tagsButton = new Button { Text = "תגיות", Width = 92, Height = 38 };
        tagsButton.Click += (_, _) => TagsClicked?.Invoke(this, _item.Id);
        UiLayoutHelper.StyleActionButton(deleteButton, 106, 38);
        UiLayoutHelper.StyleActionButton(editButton, 114, 38);
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
        var againButton = CreateRatingButton("צריך חיזוק", ReviewRating.Again, Color.FromArgb(241, 216, 208));
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
            Width = 104,
            Height = 34,
            BackColor = backColor
        };
        UiLayoutHelper.StyleActionButton(button, 104, 40);
        button.Click += (_, _) => RatingClicked?.Invoke(this, rating);
        return button;
    }

    private static Control CreateSection(string caption, string text)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 156,
            Margin = new Padding(0, 0, 0, 8),
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

    private static string TranslateRating(string rating)
    {
        return rating switch
        {
            nameof(ReviewRating.Again) => "צריך חיזוק",
            nameof(ReviewRating.Hard) => "חלש",
            nameof(ReviewRating.Good) => "בסדר",
            nameof(ReviewRating.Easy) => "טוב",
            nameof(ReviewRating.Perfect) => "מצוין",
            _ => rating
        };
    }

    private static string TranslateDifficulty(StudyDifficulty difficulty)
    {
        return difficulty switch
        {
            StudyDifficulty.Hard => "קשה",
            StudyDifficulty.Medium => "בינונית",
            StudyDifficulty.Easy => "קלה",
            _ => "ללא"
        };
    }
}
