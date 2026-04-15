namespace TrackerApp;

public sealed class ReviewSessionControl : UserControl
{
    private readonly TableLayoutPanel _contentRoot = new();
    private readonly Label _titleLabel = new();
    private readonly Label _progressLabel = new();
    private readonly Label _topicLabel = new();
    private readonly Label _pathLabel = new();
    private readonly Label _metaLabel = new();
    private readonly Label _promptTextLabel = new();
    private readonly TextBox _responseTextBox = new();
    private readonly Panel _responsePanel = new();
    private readonly Button _showComparisonButton = new();
    private readonly FlowLayoutPanel _ratingButtonsPanel = new();
    private readonly Panel _summaryPanel = new();
    private readonly Label _summaryTitleLabel = new();
    private readonly Label _summaryStatsLabel = new();
    private readonly Label _summaryHintLabel = new();
    private readonly Button _retryFailedButton = new();
    private readonly Button _closeSummaryButton = new();
    private readonly Button _pauseSessionButton = new();
    private readonly Button _skipButton = new();
    private readonly Button _reviewLaterButton = new();
    private readonly Label _orderModeLabel = new();

    private bool _comparisonVisible;

    public event EventHandler? RevealAnswerRequested;
    public event EventHandler<ReviewRating>? RatingRequested;
    public event EventHandler? RetryFailedRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler? PauseRequested;
    public event EventHandler? SkipRequested;
    public event EventHandler? ReviewLaterRequested;

    public ReviewSessionControl()
    {
        Dock = DockStyle.Fill;
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        TabStop = true;
        BuildLayout();
        UiLayoutHelper.ApplyRecursive(this);
        ShowIdleState();
    }

    public bool IsSummaryVisible => _summaryPanel.Visible;
    public bool IsAnswerVisible => _comparisonVisible;

    public void ShowReviewItem(StudyItemModel item, ReviewSessionProgress progress, string sessionTitle, ReviewSessionOrderMode orderMode)
    {
        _contentRoot.Visible = true;
        _summaryPanel.Visible = false;
        _titleLabel.Text = sessionTitle;
        _progressLabel.Text = $"סה\"כ: {progress.TotalCount} | הושלמו: {progress.CompletedCount} | נכשלו: {progress.FailedCount} | נשארו: {progress.RemainingCount}";
        _topicLabel.Text = item.Topic;
        _pathLabel.Text = item.SubjectPath.Replace(" > ", " / ", StringComparison.Ordinal);
        _metaLabel.Text = $"שלב: {item.Level} | קושי: {TranslateDifficulty(item.Difficulty)} | לביצוע: {item.DueDate:dd/MM/yyyy}";
        _orderModeLabel.Text = $"סדר סשן: {TranslateOrderMode(orderMode)}";
        _promptTextLabel.Text = item.ReviewPromptText;
        _responseTextBox.Text = item.ReviewResponseText;
        SetAnswerVisible(false);
        Visible = true;
        FocusForReview();
    }

    public void ShowSummary(ReviewSessionSummary summary)
    {
        _contentRoot.Visible = false;
        _summaryPanel.Visible = true;
        _summaryTitleLabel.Text = summary.Title;
        _summaryStatsLabel.Text =
            $"סה\"כ יחידות: {summary.TotalCount}{Environment.NewLine}" +
            $"הושלמו: {summary.CompletedCount}{Environment.NewLine}" +
            $"נכשלו: {summary.FailedCount}{Environment.NewLine}" +
            $"דורגו גבוה: {summary.HighRatingCount}{Environment.NewLine}" +
            $"דורגו נמוך: {summary.LowRatingCount}{Environment.NewLine}" +
            $"מסומנות לעיון חוזר: {summary.ReviewLaterCount}{Environment.NewLine}" +
            $"דולגו: {summary.SkippedCount}{Environment.NewLine}" +
            $"נשארו: {summary.RemainingCount}" +
            (string.IsNullOrWhiteSpace(summary.AdditionalSummaryText)
                ? string.Empty
                : $"{Environment.NewLine}{summary.AdditionalSummaryText}");
        _summaryHintLabel.Text = summary.FailedCount > 0
            ? "אפשר להפעיל עכשיו סשן נוסף רק על יחידות הלימוד שדורגו חלש."
            : "אין יחידות לימוד שדורגו חלש בסשן הזה.";
        _retryFailedButton.Visible = summary.FailedCount > 0;
        FocusForReview();
    }

    public void RevealAnswer()
    {
        SetAnswerVisible(true);
        FocusForReview();
    }

    public void ShowIdleState()
    {
        _contentRoot.Visible = true;
        _summaryPanel.Visible = false;
        _titleLabel.Text = "מסך חזרה";
        _progressLabel.Text = "בחרו סשן חזרה רגיל או סשן על יחידות לימוד נבחרות.";
        _topicLabel.Text = "אין סשן פעיל";
        _pathLabel.Text = string.Empty;
        _metaLabel.Text = "רווח או Enter להצגת חומר ההשוואה, מספרים 1-5 לדירוג.";
        _orderModeLabel.Text = "סדר סשן: לפי הסדר הרגיל";
        _promptTextLabel.Text = "פתחו חזרה יומית/מסוננת או הפעילו חזרה על יחידות הלימוד שנבחרו.";
        _responseTextBox.Text = string.Empty;
        SetAnswerVisible(false);
    }

    public bool HandleShortcut(Keys keyData)
    {
        if (!Visible)
        {
            return false;
        }

        if (_summaryPanel.Visible)
        {
            if ((keyData == Keys.Enter || keyData == Keys.Space) && _retryFailedButton.Visible)
            {
                RetryFailedRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }

            if (keyData == Keys.Escape)
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }

            return false;
        }

        if (!_comparisonVisible && (keyData == Keys.Space || keyData == Keys.Enter))
        {
            RevealAnswerRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (_comparisonVisible && TryMapRatingShortcut(keyData, out var rating))
        {
            RatingRequested?.Invoke(this, rating);
            return true;
        }

        if (keyData == Keys.S)
        {
            SkipRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.R)
        {
            ReviewLaterRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.P)
        {
            PauseRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    public void AutomationRevealAnswer()
    {
        if (!_comparisonVisible)
        {
            RevealAnswerRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void AutomationRate(ReviewRating rating)
    {
        if (_comparisonVisible)
        {
            RatingRequested?.Invoke(this, rating);
        }
    }

    public void AutomationSkip()
    {
        SkipRequested?.Invoke(this, EventArgs.Empty);
    }

    public void AutomationReviewLater()
    {
        ReviewLaterRequested?.Invoke(this, EventArgs.Empty);
    }

    public void AutomationPause()
    {
        PauseRequested?.Invoke(this, EventArgs.Empty);
    }

    public void FocusForReview()
    {
        Select();
        Focus();
    }

    private void BuildLayout()
    {
        var scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = ClassicPalette.PanelBackground,
            RightToLeft = RightToLeft.Yes
        };

        _contentRoot.Dock = DockStyle.Top;
        _contentRoot.ColumnCount = 1;
        _contentRoot.RowCount = 4;
        _contentRoot.Padding = new Padding(12);
        _contentRoot.BackColor = ClassicPalette.PanelBackground;
        _contentRoot.RightToLeft = RightToLeft.Yes;
        _contentRoot.Height = 880;
        _contentRoot.MinimumSize = new Size(0, 880);

        _contentRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        _contentRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
        _contentRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _contentRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 240F));

        var headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ClassicPalette.Teal,
            Padding = new Padding(12),
            RightToLeft = RightToLeft.Yes
        };

        _pauseSessionButton.Text = "השהה";
        _pauseSessionButton.Dock = DockStyle.Left;
        _pauseSessionButton.Width = 120;
        UiLayoutHelper.StyleActionButton(_pauseSessionButton, 120, 40);
        _pauseSessionButton.Click += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);

        var titleStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes
        };
        titleStack.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        titleStack.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        _titleLabel.Dock = DockStyle.Fill;
        _titleLabel.ForeColor = Color.White;
        _titleLabel.Font = new Font("Microsoft Sans Serif", 14F, FontStyle.Bold);
        _titleLabel.TextAlign = ContentAlignment.MiddleRight;

        _progressLabel.Dock = DockStyle.Fill;
        _progressLabel.ForeColor = Color.White;
        _progressLabel.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
        _progressLabel.TextAlign = ContentAlignment.MiddleRight;

        titleStack.Controls.Add(_titleLabel, 0, 0);
        titleStack.Controls.Add(_progressLabel, 0, 1);

        headerPanel.Controls.Add(titleStack);
        headerPanel.Controls.Add(_pauseSessionButton);

        var detailsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = ClassicPalette.CardBackground,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            RightToLeft = RightToLeft.Yes
        };
        detailsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        detailsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        detailsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        detailsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));

        ConfigureDetailLabel(_topicLabel, new Font("Microsoft Sans Serif", 12F, FontStyle.Bold));
        ConfigureDetailLabel(_pathLabel, new Font("Microsoft Sans Serif", 9.5F, FontStyle.Regular));
        ConfigureDetailLabel(_metaLabel, new Font("Microsoft Sans Serif", 9F, FontStyle.Regular));
        ConfigureDetailLabel(_orderModeLabel, new Font("Microsoft Sans Serif", 9F, FontStyle.Bold));

        detailsPanel.Controls.Add(_topicLabel, 0, 0);
        detailsPanel.Controls.Add(_pathLabel, 0, 1);
        detailsPanel.Controls.Add(_metaLabel, 0, 2);
        detailsPanel.Controls.Add(_orderModeLabel, 0, 3);

        var promptPanel = CreateTextPanel("עוגן לימוד", out var promptBodyHost);
        _promptTextLabel.Dock = DockStyle.Fill;
        _promptTextLabel.BackColor = Color.White;
        _promptTextLabel.BorderStyle = BorderStyle.FixedSingle;
        _promptTextLabel.Font = new Font("Microsoft Sans Serif", 16F, FontStyle.Bold);
        _promptTextLabel.TextAlign = ContentAlignment.TopRight;
        _promptTextLabel.Padding = new Padding(12);
        _promptTextLabel.RightToLeft = RightToLeft.Yes;
        promptBodyHost.Controls.Add(_promptTextLabel);

        var responseOuterPanel = CreateTextPanel("חומר להשוואה", out var responseBodyHost);
        _responsePanel.Dock = DockStyle.Fill;
        _responsePanel.RightToLeft = RightToLeft.Yes;

        _responseTextBox.Dock = DockStyle.Fill;
        _responseTextBox.Multiline = true;
        _responseTextBox.ReadOnly = true;
        _responseTextBox.ScrollBars = ScrollBars.Vertical;
        _responseTextBox.BorderStyle = BorderStyle.FixedSingle;
        _responseTextBox.BackColor = Color.White;
        _responseTextBox.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular);
        _responseTextBox.RightToLeft = RightToLeft.Yes;

        _responsePanel.Controls.Add(_responseTextBox);
        responseBodyHost.Controls.Add(_responsePanel);

        var contentSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            FixedPanel = FixedPanel.None,
            IsSplitterFixed = false,
            BorderStyle = BorderStyle.None,
            Panel1MinSize = 120,
            Panel2MinSize = 120,
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
        contentSplit.Panel1.Controls.Add(promptPanel);
        contentSplit.Panel2.Controls.Add(responseOuterPanel);

        var actionsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ClassicPalette.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10),
            RightToLeft = RightToLeft.Yes
        };

        var keyboardHintLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            Text = "קיצורים: רווח/Enter לחומר השוואה | 1-5 דירוג | S דלג | R עיון חוזר | P השהה | Esc סגירה",
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes
        };

        _showComparisonButton.Text = "הצג חומר להשוואה";
        _showComparisonButton.Width = 164;
        _showComparisonButton.Height = 40;
        UiLayoutHelper.StyleActionButton(_showComparisonButton, 164, 40);
        _showComparisonButton.Click += (_, _) => RevealAnswerRequested?.Invoke(this, EventArgs.Empty);

        _skipButton.Text = "דלג";
        _skipButton.Width = 96;
        _skipButton.Height = 40;
        UiLayoutHelper.StyleActionButton(_skipButton, 96, 40);
        _skipButton.Click += (_, _) => SkipRequested?.Invoke(this, EventArgs.Empty);

        _reviewLaterButton.Text = "עיון חוזר בסוף";
        _reviewLaterButton.Width = 152;
        _reviewLaterButton.Height = 40;
        UiLayoutHelper.StyleActionButton(_reviewLaterButton, 152, 40);
        _reviewLaterButton.Click += (_, _) => ReviewLaterRequested?.Invoke(this, EventArgs.Empty);

        _ratingButtonsPanel.Dock = DockStyle.Fill;
        _ratingButtonsPanel.FlowDirection = FlowDirection.RightToLeft;
        _ratingButtonsPanel.WrapContents = true;
        _ratingButtonsPanel.RightToLeft = RightToLeft.Yes;
        _ratingButtonsPanel.Padding = new Padding(0, 4, 0, 0);
        _ratingButtonsPanel.Controls.Add(CreateRatingButton("5 מצוין", ReviewRating.Perfect, Color.FromArgb(202, 222, 240)));
        _ratingButtonsPanel.Controls.Add(CreateRatingButton("4 טוב", ReviewRating.Easy, Color.FromArgb(206, 232, 234)));
        _ratingButtonsPanel.Controls.Add(CreateRatingButton("3 בסדר", ReviewRating.Good, Color.FromArgb(221, 235, 210)));
        _ratingButtonsPanel.Controls.Add(CreateRatingButton("2 חלש", ReviewRating.Hard, Color.FromArgb(240, 228, 204)));
        _ratingButtonsPanel.Controls.Add(CreateRatingButton("1 צריך חיזוק", ReviewRating.Again, Color.FromArgb(241, 216, 208)));

        var primaryButtonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            RightToLeft = RightToLeft.Yes,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        primaryButtonsPanel.Controls.Add(_reviewLaterButton);
        primaryButtonsPanel.Controls.Add(_skipButton);
        primaryButtonsPanel.Controls.Add(_showComparisonButton);

        var ratingHostPanel = new Panel
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(0, 8, 0, 0)
        };
        ratingHostPanel.Controls.Add(_ratingButtonsPanel);

        var actionsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            RightToLeft = RightToLeft.Yes
        };
        actionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        actionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        actionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        actionsLayout.Controls.Add(keyboardHintLabel, 0, 0);
        actionsLayout.Controls.Add(primaryButtonsPanel, 0, 1);
        actionsLayout.Controls.Add(ratingHostPanel, 0, 2);
        actionsPanel.Controls.Add(actionsLayout);

        BuildSummaryPanel();

        _contentRoot.Controls.Add(headerPanel, 0, 0);
        _contentRoot.Controls.Add(detailsPanel, 0, 1);
        _contentRoot.Controls.Add(contentSplit, 0, 2);
        _contentRoot.Controls.Add(actionsPanel, 0, 3);

        scrollHost.Controls.Add(_contentRoot);
        Controls.Add(_summaryPanel);
        Controls.Add(scrollHost);
    }

    private void BuildSummaryPanel()
    {
        _summaryPanel.Dock = DockStyle.Fill;
        _summaryPanel.BackColor = ClassicPalette.PanelBackground;
        _summaryPanel.Visible = false;
        _summaryPanel.Padding = new Padding(18);
        _summaryPanel.RightToLeft = RightToLeft.Yes;

        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = ClassicPalette.CardBackground,
            Padding = new Padding(16),
            RightToLeft = RightToLeft.Yes
        };

        _summaryTitleLabel.Dock = DockStyle.Top;
        _summaryTitleLabel.Height = 44;
        _summaryTitleLabel.Font = new Font("Microsoft Sans Serif", 16F, FontStyle.Bold);
        UiLayoutHelper.StyleSectionHeader(_summaryTitleLabel, 44);

        _summaryStatsLabel.Dock = DockStyle.Fill;
        _summaryStatsLabel.Font = new Font("Microsoft Sans Serif", 13F, FontStyle.Bold);
        _summaryStatsLabel.TextAlign = ContentAlignment.TopRight;
        _summaryStatsLabel.Padding = new Padding(12);
        _summaryStatsLabel.RightToLeft = RightToLeft.Yes;

        _summaryHintLabel.Dock = DockStyle.Top;
        _summaryHintLabel.Height = 36;
        _summaryHintLabel.TextAlign = ContentAlignment.MiddleRight;
        _summaryHintLabel.RightToLeft = RightToLeft.Yes;

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 96,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            RightToLeft = RightToLeft.Yes
        };

        _retryFailedButton.Text = "סשן נוסף על היחידות שדורגו חלש";
        _retryFailedButton.Width = 220;
        _retryFailedButton.Height = 38;
        UiLayoutHelper.StyleActionButton(_retryFailedButton, 220, 38);
        _retryFailedButton.Click += (_, _) => RetryFailedRequested?.Invoke(this, EventArgs.Empty);

        _closeSummaryButton.Text = "חזרה למסך הקודם";
        _closeSummaryButton.Width = 184;
        _closeSummaryButton.Height = 38;
        UiLayoutHelper.StyleActionButton(_closeSummaryButton, 184, 38);
        _closeSummaryButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        actions.Controls.Add(_retryFailedButton);
        actions.Controls.Add(_closeSummaryButton);

        card.Controls.Add(_summaryStatsLabel);
        card.Controls.Add(_summaryHintLabel);
        card.Controls.Add(actions);
        card.Controls.Add(_summaryTitleLabel);
        _summaryPanel.Controls.Add(card);
    }

    private Button CreateRatingButton(string text, ReviewRating rating, Color backColor)
    {
        var button = new Button
        {
            Text = text,
            Width = 120,
            Height = 36,
            BackColor = backColor
        };
        UiLayoutHelper.StyleActionButton(button, 120, 36);
        button.Click += (_, _) => RatingRequested?.Invoke(this, rating);
        return button;
    }

    private static Panel CreateTextPanel(string caption, out Panel bodyHost)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes
        };

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            Text = caption
        };
        UiLayoutHelper.StyleSectionHeader(header, 32);

        bodyHost = new Panel
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes
        };

        panel.Controls.Add(bodyHost);
        panel.Controls.Add(header);
        return panel;
    }

    private void SetAnswerVisible(bool isVisible)
    {
        _comparisonVisible = isVisible;
        _responsePanel.Visible = isVisible;
        _showComparisonButton.Enabled = !isVisible;
        foreach (Control control in _ratingButtonsPanel.Controls)
        {
            control.Enabled = isVisible;
        }
    }

    private static bool TryMapRatingShortcut(Keys keyData, out ReviewRating rating)
    {
        switch (keyData)
        {
            case Keys.D1:
            case Keys.NumPad1:
                rating = ReviewRating.Again;
                return true;
            case Keys.D2:
            case Keys.NumPad2:
                rating = ReviewRating.Hard;
                return true;
            case Keys.D3:
            case Keys.NumPad3:
                rating = ReviewRating.Good;
                return true;
            case Keys.D4:
            case Keys.NumPad4:
                rating = ReviewRating.Easy;
                return true;
            case Keys.D5:
            case Keys.NumPad5:
                rating = ReviewRating.Perfect;
                return true;
            default:
                rating = ReviewRating.Good;
                return false;
        }
    }

    private static void ConfigureDetailLabel(Label label, Font font)
    {
        label.Dock = DockStyle.Fill;
        label.Font = font;
        label.TextAlign = ContentAlignment.MiddleRight;
        label.RightToLeft = RightToLeft.Yes;
        label.Padding = new Padding(8, 0, 8, 0);
    }

    private static string TranslateDifficulty(StudyDifficulty difficulty)
    {
        return difficulty switch
        {
            StudyDifficulty.Easy => "קלה",
            StudyDifficulty.Medium => "בינונית",
            StudyDifficulty.Hard => "קשה",
            _ => "ללא"
        };
    }

    private static string TranslateOrderMode(ReviewSessionOrderMode orderMode)
    {
        return orderMode switch
        {
            ReviewSessionOrderMode.Random => "אקראי",
            ReviewSessionOrderMode.HardFirst => "מהקשים לקלים",
            ReviewSessionOrderMode.FailedFirst => "מהחלשים תחילה",
            ReviewSessionOrderMode.NewFirst => "מהחדשים תחילה",
            _ => "לפי הסדר הרגיל"
        };
    }
}
