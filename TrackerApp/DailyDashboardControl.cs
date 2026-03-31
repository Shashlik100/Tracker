namespace TrackerApp;

public sealed class DailyDashboardControl : UserControl
{
    private readonly RightAlignedDrawLabel _dueTodayLabel = new();
    private readonly RightAlignedDrawLabel _overdueLabel = new();
    private readonly RightAlignedDrawLabel _newLabel = new();
    private readonly RightAlignedDrawLabel _failedLabel = new();
    private readonly RightAlignedDrawLabel _reviewLaterLabel = new();
    private readonly RightAlignedDrawLabel _pausedLabel = new();
    private readonly RightAlignedDrawLabel _unitsStudiedLabel = new();
    private readonly RightAlignedDrawLabel _unitsWaitingLabel = new();
    private readonly RightAlignedDrawLabel _unitsDueLabel = new();
    private readonly RightAlignedDrawLabel _unitsFailedLabel = new();
    private readonly RightAlignedDrawLabel _unitsCompletedLabel = new();
    private readonly RightAlignedDrawLabel _unitsSelectedNodeLabel = new();
    private readonly ListBox _presetListBox = new();
    private readonly DataGridView _weakSpotsGrid = new();
    private readonly ComboBox _tagQueueComboBox = new();

    public event EventHandler? DailyReviewRequested;
    public event EventHandler? ResumeRequested;
    public event EventHandler? FailedRequested;
    public event EventHandler? HardRequested;
    public event EventHandler? FilteredRequested;
    public event EventHandler? OverdueRequested;
    public event EventHandler? NewRequested;
    public event EventHandler? ReviewLaterRequested;
    public event EventHandler? SelectedNodeRequested;
    public event EventHandler? UnitDueRequested;
    public event EventHandler? UnitFailedRequested;
    public event EventHandler? SelectedUnitRequested;
    public event EventHandler<int?>? TagQueueRequested;
    public event EventHandler? AddPresetRequested;
    public event EventHandler<ReviewPresetModel>? EditPresetRequested;
    public event EventHandler<ReviewPresetModel>? DeletePresetRequested;
    public event EventHandler<ReviewPresetModel>? RunPresetRequested;

    public DailyDashboardControl()
    {
        Dock = DockStyle.Fill;
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        AutoScroll = true;
        BuildLayout();
        UiLayoutHelper.ApplyRecursive(this);
    }

    public ReviewPresetModel? SelectedPreset => _presetListBox.SelectedItem as ReviewPresetModel;
    public int? SelectedTagId => _tagQueueComboBox.SelectedItem is TagChoice choice && choice.Id.HasValue ? choice.Id.Value : null;
    public int WeakSpotCount => _weakSpotsGrid.Rows.Count;

    public void AutomationSelectFirstPreset()
    {
        if (_presetListBox.Items.Count > 0)
        {
            _presetListBox.SelectedIndex = 0;
        }
    }

    public void AutomationSelectFirstWeakSpot()
    {
        if (_weakSpotsGrid.Rows.Count <= 0)
        {
            return;
        }

        _weakSpotsGrid.ClearSelection();
        _weakSpotsGrid.Rows[0].Selected = true;
        if (_weakSpotsGrid.Columns.Count > 0)
        {
            _weakSpotsGrid.CurrentCell = _weakSpotsGrid.Rows[0].Cells[0];
        }
    }

    public void Bind(DailyDashboardModel model, IReadOnlyList<ReviewPresetModel> presets, IReadOnlyList<WeakSpotModel> weakSpots, IReadOnlyList<TagModel> tags)
    {
        _dueTodayLabel.Text = model.DueTodayCount.ToString();
        _overdueLabel.Text = model.OverdueCount.ToString();
        _newLabel.Text = model.NewCount.ToString();
        _failedLabel.Text = model.FailedRecentlyCount.ToString();
        _reviewLaterLabel.Text = model.ReviewLaterCount.ToString();
        _pausedLabel.Text = model.HasPausedSession ? "כן" : "לא";
        _unitsStudiedLabel.Text = model.UnitsStudiedTodayCount.ToString();
        _unitsWaitingLabel.Text = model.UnitsWaitingCount.ToString();
        _unitsDueLabel.Text = model.UnitsDueCount.ToString();
        _unitsFailedLabel.Text = model.UnitsFailedCount.ToString();
        _unitsCompletedLabel.Text = model.UnitsCompletedCycleCount.ToString();
        _unitsSelectedNodeLabel.Text = model.UnitsForSelectedNodeCount.ToString();

        _presetListBox.BeginUpdate();
        _presetListBox.Items.Clear();
        foreach (var preset in presets)
        {
            _presetListBox.Items.Add(preset);
        }
        _presetListBox.EndUpdate();
        if (_presetListBox.Items.Count > 0 && _presetListBox.SelectedIndex < 0)
        {
            _presetListBox.SelectedIndex = 0;
        }

        var selectedTagId = SelectedTagId;
        _tagQueueComboBox.Items.Clear();
        _tagQueueComboBox.Items.Add(new TagChoice(null, "בחר תגית"));
        foreach (var tag in tags)
        {
            _tagQueueComboBox.Items.Add(new TagChoice(tag.Id, tag.Name));
        }

        _tagQueueComboBox.SelectedIndex = 0;
        if (selectedTagId.HasValue)
        {
            for (var index = 0; index < _tagQueueComboBox.Items.Count; index++)
            {
                if (_tagQueueComboBox.Items[index] is TagChoice choice && choice.Id == selectedTagId)
                {
                    _tagQueueComboBox.SelectedIndex = index;
                    break;
                }
            }
        }

        _weakSpotsGrid.Rows.Clear();
        foreach (var weakSpot in weakSpots)
        {
            _weakSpotsGrid.Rows.Add(
                weakSpot.Kind,
                weakSpot.Name,
                $"{weakSpot.LowRatingPercent:0.#}%",
                weakSpot.FailureCount,
                weakSpot.OverdueCount,
                weakSpot.ItemCount);
        }
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

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(8),
            RightToLeft = RightToLeft.Yes,
            Height = 1160,
            MinimumSize = new Size(0, 1160)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 252F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 620F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var summaryPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            AutoScroll = false,
            Padding = new Padding(6, 6, 6, 12),
            Margin = Padding.Empty,
            RightToLeft = RightToLeft.Yes
        };
        summaryPanel.Controls.Add(CreateSummaryBox("להיום", _dueTodayLabel));
        summaryPanel.Controls.Add(CreateSummaryBox("באיחור", _overdueLabel));
        summaryPanel.Controls.Add(CreateSummaryBox("חדשים", _newLabel));
        summaryPanel.Controls.Add(CreateSummaryBox("נכשלו לאחרונה", _failedLabel));
        summaryPanel.Controls.Add(CreateSummaryBox("לעיון חוזר", _reviewLaterLabel));
        summaryPanel.Controls.Add(CreateSummaryBox("סשן מושהה", _pausedLabel));
        summaryPanel.Controls.Add(CreateSummaryBox("נלמדו היום", _unitsStudiedLabel));
        summaryPanel.Controls.Add(CreateSummaryBox("יחידות ממתינות", _unitsWaitingLabel));
        summaryPanel.Controls.Add(CreateSummaryBox("יחידות לביצוע", _unitsDueLabel));
        summaryPanel.Controls.Add(CreateSummaryBox("יחידות חלשות", _unitsFailedLabel));
        summaryPanel.Controls.Add(CreateSummaryBox("מחזור הושלם", _unitsCompletedLabel));
        summaryPanel.Controls.Add(CreateSummaryBox("בצומת הנבחר", _unitsSelectedNodeLabel));

        var queueLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            AutoScroll = true,
            Padding = new Padding(10, 14, 10, 16),
            RightToLeft = RightToLeft.Yes,
            Margin = Padding.Empty
        };
        queueLayout.Controls.Add(CreateActionButton("התחל חזרה יומית", (_, _) => DailyReviewRequested?.Invoke(this, EventArgs.Empty)));
        queueLayout.Controls.Add(CreateActionButton("המשך סשן מושהה", (_, _) => ResumeRequested?.Invoke(this, EventArgs.Empty)));
        queueLayout.Controls.Add(CreateActionButton("חזרה על נכשלים", (_, _) => FailedRequested?.Invoke(this, EventArgs.Empty)));
        queueLayout.Controls.Add(CreateActionButton("חזרה על קשים", (_, _) => HardRequested?.Invoke(this, EventArgs.Empty)));
        queueLayout.Controls.Add(CreateActionButton("חזרה על חדשים", (_, _) => NewRequested?.Invoke(this, EventArgs.Empty)));
        queueLayout.Controls.Add(CreateActionButton("מסומנים לעיון חוזר", (_, _) => ReviewLaterRequested?.Invoke(this, EventArgs.Empty)));
        queueLayout.Controls.Add(CreateActionButton("לפי הצומת הנבחר", (_, _) => SelectedNodeRequested?.Invoke(this, EventArgs.Empty)));
        queueLayout.Controls.Add(CreateActionButton("חזרה על באיחור", (_, _) => OverdueRequested?.Invoke(this, EventArgs.Empty)));
        queueLayout.Controls.Add(CreateActionButton("פתח חזרה מסוננת", (_, _) => FilteredRequested?.Invoke(this, EventArgs.Empty)));
        queueLayout.Controls.Add(CreateActionButton("יחידות שהגיע זמנן", (_, _) => UnitDueRequested?.Invoke(this, EventArgs.Empty)));
        queueLayout.Controls.Add(CreateActionButton("יחידות חלשות", (_, _) => UnitFailedRequested?.Invoke(this, EventArgs.Empty)));
        queueLayout.Controls.Add(CreateActionButton("חזרת יחידה נבחרת", (_, _) => SelectedUnitRequested?.Invoke(this, EventArgs.Empty)));
        var queuePanel = CreateSectionContainer("תורים חכמים", CreateRightAnchoredBody(queueLayout, 720));

        var presetLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8),
            RightToLeft = RightToLeft.Yes
        };
        presetLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        presetLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        presetLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 164F));

        var tagQueuePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(4)
        };
        tagQueuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232F));
        tagQueuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tagQueuePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        _tagQueueComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _tagQueueComboBox.Dock = DockStyle.Fill;
        _tagQueueComboBox.RightToLeft = RightToLeft.Yes;
        tagQueuePanel.Controls.Add(CreateActionButton("חזרה לפי תגית", (_, _) => TagQueueRequested?.Invoke(this, SelectedTagId)), 0, 0);
        tagQueuePanel.Controls.Add(_tagQueueComboBox, 1, 0);

        _presetListBox.Dock = DockStyle.Fill;
        _presetListBox.IntegralHeight = false;
        _presetListBox.DrawMode = DrawMode.OwnerDrawFixed;
        _presetListBox.ItemHeight = 28;
        _presetListBox.DrawItem += DrawPresetListItem;

        var presetButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(0, 8, 0, 4)
        };
        presetButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        presetButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        presetButtons.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
        presetButtons.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
        presetButtons.Controls.Add(CreateActionButton("הפעל", (_, _) => { if (SelectedPreset is not null) RunPresetRequested?.Invoke(this, SelectedPreset); }), 0, 0);
        presetButtons.Controls.Add(CreateActionButton("חדש", (_, _) => AddPresetRequested?.Invoke(this, EventArgs.Empty)), 1, 0);
        presetButtons.Controls.Add(CreateActionButton("ערוך", (_, _) => { if (SelectedPreset is not null) EditPresetRequested?.Invoke(this, SelectedPreset); }), 0, 1);
        presetButtons.Controls.Add(CreateActionButton("מחק", (_, _) => { if (SelectedPreset is not null) DeletePresetRequested?.Invoke(this, SelectedPreset); }), 1, 1);

        presetLayout.Controls.Add(tagQueuePanel, 0, 0);
        presetLayout.Controls.Add(CreateRightAnchoredBody(_presetListBox, 0), 0, 1);
        presetLayout.Controls.Add(presetButtons, 0, 2);
        var presetPanel = CreateSectionContainer("פריסטים שמורים", presetLayout);

        _weakSpotsGrid.Dock = DockStyle.Fill;
        _weakSpotsGrid.AllowUserToAddRows = false;
        _weakSpotsGrid.AllowUserToDeleteRows = false;
        _weakSpotsGrid.ReadOnly = true;
        _weakSpotsGrid.RowHeadersVisible = false;
        _weakSpotsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _weakSpotsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _weakSpotsGrid.RightToLeft = RightToLeft.Yes;
        UiLayoutHelper.StyleDataGridView(_weakSpotsGrid);
        _weakSpotsGrid.Columns.Add("Kind", "סוג");
        _weakSpotsGrid.Columns.Add("Name", "שם");
        _weakSpotsGrid.Columns.Add("LowRatingPercent", "% דירוג נמוך");
        _weakSpotsGrid.Columns.Add("FailureCount", "כשלונות");
        _weakSpotsGrid.Columns.Add("OverdueCount", "באיחור");
        _weakSpotsGrid.Columns.Add("ItemCount", "כרטיסים");

        var weakSpotsBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(6),
            BackColor = ClassicPalette.CardBackground
        };
        weakSpotsBody.Controls.Add(CreateRightAnchoredBody(_weakSpotsGrid, 0));
        var weakSpotsPanel = CreateSectionContainer("נקודות חולשה", weakSpotsBody);

        root.Controls.Add(summaryPanel, 0, 0);
        root.SetColumnSpan(summaryPanel, 2);
        root.Controls.Add(queuePanel, 0, 1);
        root.Controls.Add(presetPanel, 1, 1);
        root.Controls.Add(weakSpotsPanel, 0, 2);
        root.SetColumnSpan(weakSpotsPanel, 2);

        scrollHost.Controls.Add(root);
        Controls.Add(scrollHost);
    }

    private static Control CreateSummaryBox(string title, RightAlignedDrawLabel valueLabel)
    {
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Font = new Font("Microsoft Sans Serif", 16F, FontStyle.Bold);
        valueLabel.TextAlign = ContentAlignment.MiddleRight;
        valueLabel.RightToLeft = RightToLeft.Yes;
        valueLabel.Padding = new Padding(2, 0, 10, 0);
        valueLabel.LeftInset = 2;
        valueLabel.RightInset = 10;
        valueLabel.DrawRightAligned = true;
        valueLabel.Margin = Padding.Empty;

        var header = new RightAlignedDrawLabel
        {
            Text = title,
            Dock = DockStyle.Right,
            Width = Math.Max(120, TextRenderer.MeasureText(title, new Font("Microsoft Sans Serif", 10F, FontStyle.Bold)).Width + 28)
        };
        UiLayoutHelper.StyleSectionHeader(header, 34);
        header.LeftInset = 2;
        header.RightInset = 10;
        header.DrawRightAligned = true;

        var headerHost = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = ClassicPalette.Teal,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(0),
            Margin = Padding.Empty,
            RightToLeft = RightToLeft.Yes
        };
        headerHost.Controls.Add(header);

        var valueHost = new Panel
        {
            Dock = DockStyle.Right,
            Width = 136,
            Padding = new Padding(0),
            Margin = Padding.Empty,
            BackColor = ClassicPalette.CardBackground,
            RightToLeft = RightToLeft.Yes
        };
        valueHost.Controls.Add(valueLabel);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ClassicPalette.CardBackground,
            Padding = new Padding(0, 8, 6, 4),
            RightToLeft = RightToLeft.Yes
        };
        body.Controls.Add(valueHost);

        var box = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ClassicPalette.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty,
            MinimumSize = new Size(0, 100),
            RightToLeft = RightToLeft.Yes
        };
        box.Controls.Add(body);
        box.Controls.Add(headerHost);
        UiLayoutHelper.ApplyRecursive(box);

        var host = new GroupBoxHostPanel();
        host.Width = 176;
        host.Height = 112;
        host.Controls.Add(box);
        return host;
    }

    private void DrawPresetListItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _presetListBox.Items.Count)
        {
            return;
        }

        var text = _presetListBox.Items[e.Index]?.ToString() ?? string.Empty;
        var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var textColor = isSelected ? SystemColors.HighlightText : _presetListBox.ForeColor;
        var bounds = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top, Math.Max(0, e.Bounds.Width - 10), e.Bounds.Height);

        TextRenderer.DrawText(
            e.Graphics,
            text,
            _presetListBox.Font,
            bounds,
            textColor,
            TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        e.DrawFocusRectangle();
    }

    private static Button CreateActionButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Width = 248,
            Height = 55,
            Margin = new Padding(4, 4, 4, 6)
        };
        UiLayoutHelper.StyleActionButton(button, 220, 55);
        button.Click += onClick;
        return button;
    }

    private static Panel CreateSectionContainer(string title, Control content)
    {
        content.Dock = DockStyle.Fill;
        content.Margin = Padding.Empty;

        var bodyHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ClassicPalette.CardBackground,
            Padding = new Padding(8),
            RightToLeft = RightToLeft.Yes
        };
        bodyHost.Controls.Add(content);

        var header = new RightAlignedDrawLabel
        {
            Text = title,
            Dock = DockStyle.Right,
            Width = Math.Max(140, TextRenderer.MeasureText(title, new Font("Microsoft Sans Serif", 10F, FontStyle.Bold)).Width + 30)
        };
        UiLayoutHelper.StyleSectionHeader(header, 34);
        header.LeftInset = 2;
        header.RightInset = 10;
        header.DrawRightAligned = true;

        var headerHost = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = ClassicPalette.Teal,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RightToLeft = RightToLeft.Yes
        };
        headerHost.Controls.Add(header);

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ClassicPalette.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
            RightToLeft = RightToLeft.Yes,
            Margin = new Padding(4)
        };
        panel.Controls.Add(bodyHost);
        panel.Controls.Add(headerHost);
        return panel;
    }

    private static Control CreateRightAnchoredBody(Control content, int preferredWidth)
    {
        content.Dock = DockStyle.Fill;
        content.Margin = Padding.Empty;

        var host = new Panel
        {
            Dock = preferredWidth > 0 ? DockStyle.Right : DockStyle.Fill,
            Width = preferredWidth > 0 ? preferredWidth : 0,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes
        };
        host.Controls.Add(content);
        return host;
    }

    private sealed record TagChoice(int? Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed class GroupBoxHostPanel : Panel
    {
        public GroupBoxHostPanel()
        {
            Dock = DockStyle.None;
            Margin = new Padding(4, 4, 4, 10);
            Padding = new Padding(0, 0, 0, 4);
            BackColor = Color.Transparent;
        }
    }

    private sealed class RightAlignedDrawLabel : Label
    {
        public int LeftInset { get; set; } = 8;
        public int RightInset { get; set; } = 14;
        public bool DrawRightAligned { get; set; } = true;

        protected override void OnPaint(PaintEventArgs e)
        {
            using var backBrush = new SolidBrush(BackColor);
            e.Graphics.FillRectangle(backBrush, ClientRectangle);

            var textBounds = new Rectangle(
                LeftInset,
                0,
                Math.Max(0, Width - LeftInset - RightInset),
                Height);

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textBounds,
                ForeColor,
                (DrawRightAligned
                    ? TextFormatFlags.Right | TextFormatFlags.RightToLeft
                    : TextFormatFlags.Left) |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);

            if (BorderStyle == BorderStyle.FixedSingle)
            {
                ControlPaint.DrawBorder(e.Graphics, ClientRectangle, SystemColors.ControlDark, ButtonBorderStyle.Solid);
            }
        }
    }
}
