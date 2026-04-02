using System.Diagnostics;

namespace TrackerApp;

public sealed class MainForm : Form
{
    private const string DummyNodeText = "...";

    private readonly AppDatabase _database;
    private readonly HebrewTreeView _subjectTreeView = new();
    private readonly FlowLayoutPanel _cardPanel = new();
    private readonly DailyDashboardControl _statusView = new();
    private readonly SearchFilterControl _searchFilterView = new();
    private readonly ReviewFilterControl _reviewFilterView = new();
    private readonly ReviewSessionControl _reviewSessionView = new();
    private readonly TagsManagerControl _tagsManagerView = new();
    private readonly Panel _cardsWorkspace = new();
    private readonly Panel _unitActionBar = new();
    private readonly FlowLayoutPanel _bulkActionBar = new();
    private readonly Label _contentTitleLabel = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly Label _unitStatusLabel = new();
    private readonly Button _markUnitLearnedButton = new();
    private readonly Button _resetUnitCycleButton = new();
    private readonly Button _reviewUnitButton = new();
    private readonly ToolStripMenuItem _questionsTabItem = new();
    private readonly ToolStripMenuItem _tagsTabItem = new();
    private readonly ToolStripMenuItem _searchTabItem = new();
    private readonly ToolStripMenuItem _reviewTabItem = new();
    private readonly ToolStripMenuItem _statusTabItem = new();
    private readonly TanakhVerificationRequest? _verificationRequest;
    private readonly CoreVerificationRequest? _coreVerificationRequest;
    private readonly ManagementVerificationRequest? _managementVerificationRequest;
    private readonly ReviewVerificationRequest? _reviewVerificationRequest;
    private readonly DailyWorkflowVerificationRequest? _dailyWorkflowVerificationRequest;
    private readonly LibraryVerificationRequest? _libraryVerificationRequest;
    private readonly TagFlowVerificationRequest? _tagFlowVerificationRequest;
    private readonly BottomActionsVerificationRequest? _bottomActionsVerificationRequest;
    private readonly ProductVerificationRequest? _productVerificationRequest;
    private readonly StudyUnitVerificationRequest? _studyUnitVerificationRequest;
    private readonly FreshStartVerificationRequest? _freshStartVerificationRequest;
    private readonly MaintenanceVerificationRequest? _maintenanceVerificationRequest;
    private readonly long _applicationStartTickCount;

    private int? _selectedSubjectId;
    private bool _verificationStarted;
    private MainWorkspaceMode _currentWorkspace = MainWorkspaceMode.Questions;
    private MainWorkspaceMode _reviewSessionReturnMode = MainWorkspaceMode.Review;
    private List<StudyItemModel> _reviewSessionItems = [];
    private List<int> _reviewFailedItemIds = [];
    private HashSet<int> _reviewSessionSkippedItemIds = [];
    private HashSet<int> _reviewSessionReviewLaterItemIds = [];
    private int _reviewSessionCompletedCount;
    private int _reviewSessionFailedCount;
    private int _reviewSessionHighRatingCount;
    private int _reviewSessionLowRatingCount;
    private string _reviewSessionTitle = string.Empty;
    private ReviewSessionOrderMode _reviewSessionOrderMode = ReviewSessionOrderMode.Default;
    private bool _isUnitReviewSession;
    private int? _activeUnitReviewSubjectId;
    private readonly List<ReviewRating> _unitSessionRatings = [];

    public MainForm(AppDatabase database, TanakhVerificationRequest? verificationRequest = null, CoreVerificationRequest? coreVerificationRequest = null, ManagementVerificationRequest? managementVerificationRequest = null, ReviewVerificationRequest? reviewVerificationRequest = null, DailyWorkflowVerificationRequest? dailyWorkflowVerificationRequest = null, LibraryVerificationRequest? libraryVerificationRequest = null, TagFlowVerificationRequest? tagFlowVerificationRequest = null, BottomActionsVerificationRequest? bottomActionsVerificationRequest = null, ProductVerificationRequest? productVerificationRequest = null, StudyUnitVerificationRequest? studyUnitVerificationRequest = null, FreshStartVerificationRequest? freshStartVerificationRequest = null, MaintenanceVerificationRequest? maintenanceVerificationRequest = null, long applicationStartTickCount = 0)
    {
        _database = database;
        _verificationRequest = verificationRequest;
        _coreVerificationRequest = coreVerificationRequest;
        _managementVerificationRequest = managementVerificationRequest;
        _reviewVerificationRequest = reviewVerificationRequest;
        _dailyWorkflowVerificationRequest = dailyWorkflowVerificationRequest;
        _libraryVerificationRequest = libraryVerificationRequest;
        _tagFlowVerificationRequest = tagFlowVerificationRequest;
        _bottomActionsVerificationRequest = bottomActionsVerificationRequest;
        _productVerificationRequest = productVerificationRequest;
        _studyUnitVerificationRequest = studyUnitVerificationRequest;
        _freshStartVerificationRequest = freshStartVerificationRequest;
        _maintenanceVerificationRequest = maintenanceVerificationRequest;
        _applicationStartTickCount = applicationStartTickCount;

        Text = "מערכת חזרות";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1280, 820);
        Size = new Size(1500, 940);
        BackColor = ClassicPalette.WindowBackground;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        KeyPreview = true;
        UiLayoutHelper.ApplyFormDefaults(this);

        BuildLayout();
        UiLayoutHelper.ApplyRecursive(this);
        WireEvents();
        Load += (_, _) => InitializeScreen();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_reviewSessionView.Visible && _reviewSessionView.HandleShortcut(keyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void BuildLayout()
    {
        SuspendLayout();
        Controls.Add(CreateMainArea());
        Controls.Add(CreateTopChrome());
        Controls.Add(CreateStatusStrip());
        ResumeLayout(true);
    }

    private void WireEvents()
    {
        _subjectTreeView.BeforeExpand += (_, e) => HandleTreeBeforeExpand(e);
        _subjectTreeView.AfterSelect += (_, _) => HandleTreeSelection();

        _searchFilterView.SearchRequested += (_, _) => RunSearch();
        _searchFilterView.ClearRequested += (_, _) =>
        {
            PopulateCards(Array.Empty<StudyItemModel>());
            _searchFilterView.SetResultSummary("אין תוצאות מוצגות.");
            _statusLabel.Text = "החיפוש אופס.";
        };

        _reviewFilterView.ReviewRequested += (_, _) => StartFilteredReviewSession();
        _reviewFilterView.ResetRequested += (_, _) => RunReview();
        _reviewFilterView.ResumeRequested += (_, _) => ResumePausedReviewSession();

        _reviewSessionView.RevealAnswerRequested += (_, _) => _reviewSessionView.RevealAnswer();
        _reviewSessionView.RatingRequested += (_, rating) => HandleReviewSessionRating(rating);
        _reviewSessionView.RetryFailedRequested += (_, _) => RetryFailedReviewSession();
        _reviewSessionView.CloseRequested += (_, _) => ExitReviewSession();
        _reviewSessionView.PauseRequested += (_, _) => PauseReviewSession();
        _reviewSessionView.SkipRequested += (_, _) => SkipCurrentReviewItem();
        _reviewSessionView.ReviewLaterRequested += (_, _) => MoveCurrentReviewItemToEnd();

        _tagsManagerView.AddTagRequested += (_, _) => CreateTag();
        _tagsManagerView.EditTagRequested += (_, tag) => EditTag(tag);
        _tagsManagerView.DeleteTagRequested += (_, tag) => DeleteTag(tag);
        _tagsManagerView.FilterByTagRequested += (_, tag) => FilterByTag(tag);

        _statusView.DailyReviewRequested += (_, _) => StartSmartQueue(SmartQueueKind.Daily);
        _statusView.ResumeRequested += (_, _) => ResumePausedReviewSession();
        _statusView.FailedRequested += (_, _) => StartSmartQueue(SmartQueueKind.FailedRecently);
        _statusView.HardRequested += (_, _) => StartSmartQueue(SmartQueueKind.Hard);
        _statusView.FilteredRequested += (_, _) => ShowReviewView();
        _statusView.OverdueRequested += (_, _) => StartSmartQueue(SmartQueueKind.Overdue);
        _statusView.NewRequested += (_, _) => StartSmartQueue(SmartQueueKind.New);
        _statusView.ReviewLaterRequested += (_, _) => StartSmartQueue(SmartQueueKind.ReviewLater);
        _statusView.SelectedNodeRequested += (_, _) => StartSmartQueue(SmartQueueKind.SelectedNode);
        _statusView.UnitDueRequested += (_, _) => StartNextDueUnitReview();
        _statusView.UnitFailedRequested += (_, _) => StartFailedUnitReview();
        _statusView.SelectedUnitRequested += (_, _) => StartSelectedUnitReview();
        _statusView.TagQueueRequested += (_, tagId) => StartSmartQueue(SmartQueueKind.Tag, tagId);
        _statusView.AddPresetRequested += (_, _) => CreateReviewPreset();
        _statusView.EditPresetRequested += (_, preset) => EditReviewPreset(preset);
        _statusView.DeletePresetRequested += (_, preset) => DeleteReviewPreset(preset);
        _statusView.RunPresetRequested += (_, preset) => RunReviewPreset(preset);

        _cardPanel.SizeChanged += (_, _) => ResizeCards();
    }

    private Control CreateTopChrome()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 118,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = ClassicPalette.ToolbarBackground,
            RightToLeft = RightToLeft.Yes
        };

        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));

        ApplyMirroredRtlWhenAvailable(host);
        host.Controls.Add(CreateTabsBar(), 0, 0);
        host.Controls.Add(CreateFormattingToolbar(), 0, 1);
        return host;
    }

    private Control CreateTabsBar()
    {
        var menu = new MenuStrip
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 62,
            Stretch = true,
            RenderMode = ToolStripRenderMode.System,
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = ClassicPalette.ToolbarBackground,
            ForeColor = Color.White,
            Padding = new Padding(8, 14, 8, 10),
            Margin = Padding.Empty,
            RightToLeft = RightToLeft.Yes
        };

        ApplyMirroredRtlWhenAvailable(menu);
        menu.Items.AddRange(
        [
            CreateTopTab(_questionsTabItem, "יחידות לימוד", (_, _) => ShowStudyCards()),
            CreateTopTab(_tagsTabItem, "תגיות", (_, _) => ShowTagsView()),
            CreateTopTab(_searchTabItem, "חיפוש", (_, _) => ShowSearchView()),
            CreateTopTab(_reviewTabItem, "חזרה יומית", (_, _) => ShowReviewView()),
            CreateTopTab(_statusTabItem, "מצב", (_, _) => ShowStatusView()),
            CreateTopTab(new ToolStripMenuItem(), "תחזוקה וגיבוי", (_, _) => OpenMaintenanceDialog()),
            CreateTopTab(new ToolStripMenuItem(), "עזרה", (_, _) => ShowHelp()),
            CreateTopTab(new ToolStripMenuItem(), "אודות", (_, _) => ShowAbout())
        ]);

        return menu;
    }

    private Control CreateFormattingToolbar()
    {
        var toolbar = new ToolStrip
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 56,
            Stretch = true,
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System,
            BackColor = Color.FromArgb(127, 191, 184),
            Padding = new Padding(8, 10, 8, 10),
            Margin = Padding.Empty,
            RightToLeft = RightToLeft.Yes,
            LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
            CanOverflow = false
        };

        ApplyMirroredRtlWhenAvailable(toolbar);
        toolbar.Items.Add(CreateFormatCombo());
        toolbar.Items.Add(CreateToolStripSeparator());
        toolbar.Items.Add(CreateFormatButton("ימין"));
        toolbar.Items.Add(CreateFormatButton("מרכז"));
        toolbar.Items.Add(CreateFormatButton("שמאל"));
        toolbar.Items.Add(CreateToolStripSeparator());
        toolbar.Items.Add(CreateFormatButton("U", new Font("Microsoft Sans Serif", 9F, FontStyle.Bold | FontStyle.Underline)));
        toolbar.Items.Add(CreateFormatButton("I", new Font("Times New Roman", 10F, FontStyle.Bold | FontStyle.Italic)));
        toolbar.Items.Add(CreateFormatButton("B", new Font("Microsoft Sans Serif", 10F, FontStyle.Bold)));
        toolbar.Items.Add(CreateToolStripSeparator());
        toolbar.Items.Add(CreateFormatButton("16"));
        toolbar.Items.Add(CreateFormatButton("14"));
        toolbar.Items.Add(CreateFormatButton("12"));
        return toolbar;
    }

    private Control CreateMainArea()
    {
        var container = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ClassicPalette.PanelBackground,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RightToLeft = RightToLeft.Yes
        };

        ApplyMirroredRtlWhenAvailable(container);
        container.Controls.Add(CreateContentPanel());
        container.Controls.Add(CreateTreePanel());
        return container;
    }

    private Control CreateTreePanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 330,
            BackColor = ClassicPalette.PanelBackground,
            Padding = new Padding(6),
            Margin = Padding.Empty,
            RightToLeft = RightToLeft.Yes
        };

        ApplyMirroredRtlWhenAvailable(panel);

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text = "ספרייה"
        };
        UiLayoutHelper.StyleSectionHeader(header, 34);

        _subjectTreeView.Dock = DockStyle.Fill;
        _subjectTreeView.BorderStyle = BorderStyle.FixedSingle;
        _subjectTreeView.HideSelection = false;
        _subjectTreeView.BackColor = Color.White;
        _subjectTreeView.RightToLeft = RightToLeft.Yes;
        _subjectTreeView.ShowNodeToolTips = true;

        panel.Controls.Add(_subjectTreeView);
        panel.Controls.Add(header);
        return panel;
    }

    private Control CreateContentPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ClassicPalette.PanelBackground,
            Padding = new Padding(6),
            Margin = Padding.Empty,
            RightToLeft = RightToLeft.Yes
        };

        ApplyMirroredRtlWhenAvailable(panel);

        var headerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 132,
            BackColor = ClassicPalette.Teal,
            BorderStyle = BorderStyle.FixedSingle,
            RightToLeft = RightToLeft.Yes,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(6, 6, 6, 6)
        };
        headerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        headerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));

        var headerActions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        for (var index = 0; index < 4; index++)
        {
            headerActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        var printButton = CreateHeaderActionButton("הדפסה", (_, _) => ExportPrintRange());
        var exportButton = CreateHeaderActionButton("ייצוא CSV", (_, _) => ExportCsv());
        var importButton = CreateHeaderActionButton("ייבוא CSV", (_, _) => ImportCsv());
        var addCardButton = CreateHeaderActionButton("יחידת לימוד חדשה", (_, _) => OpenAddDialog());
        printButton.Dock = DockStyle.Fill;
        exportButton.Dock = DockStyle.Fill;
        importButton.Dock = DockStyle.Fill;
        addCardButton.Dock = DockStyle.Fill;
        headerActions.Controls.Add(addCardButton, 0, 0);
        headerActions.Controls.Add(importButton, 1, 0);
        headerActions.Controls.Add(exportButton, 2, 0);
        headerActions.Controls.Add(printButton, 3, 0);

        _contentTitleLabel.Dock = DockStyle.Fill;
        _contentTitleLabel.Font = new Font(Font, FontStyle.Bold);
        _contentTitleLabel.ForeColor = Color.White;
        _contentTitleLabel.TextAlign = ContentAlignment.MiddleRight;
        _contentTitleLabel.RightToLeft = RightToLeft.Yes;

        headerPanel.Controls.Add(_contentTitleLabel, 0, 0);
        headerPanel.Controls.Add(headerActions, 0, 1);

        _cardPanel.Dock = DockStyle.Fill;
        _cardPanel.AutoScroll = true;
        _cardPanel.FlowDirection = FlowDirection.TopDown;
        _cardPanel.WrapContents = false;
        _cardPanel.BackColor = ClassicPalette.PanelBackground;
        _cardPanel.RightToLeft = RightToLeft.Yes;

        BuildUnitActionBar();
        BuildBulkActionBar();

        _cardsWorkspace.Dock = DockStyle.Fill;
        _cardsWorkspace.BackColor = ClassicPalette.PanelBackground;
        _reviewSessionView.Visible = false;
        _cardsWorkspace.Controls.Add(_cardPanel);
        _cardsWorkspace.Controls.Add(_reviewSessionView);
        _cardsWorkspace.Controls.Add(_bulkActionBar);
        _cardsWorkspace.Controls.Add(_unitActionBar);
        _cardsWorkspace.Controls.Add(_reviewFilterView);
        _cardsWorkspace.Controls.Add(_searchFilterView);

        _searchFilterView.Visible = false;
        _reviewFilterView.Visible = false;

        _statusView.Dock = DockStyle.Fill;
        _statusView.Visible = false;

        _tagsManagerView.Dock = DockStyle.Fill;
        _tagsManagerView.Visible = false;

        panel.Controls.Add(_cardsWorkspace);
        panel.Controls.Add(_statusView);
        panel.Controls.Add(_tagsManagerView);
        panel.Controls.Add(headerPanel);
        return panel;
    }

    private void BuildUnitActionBar()
    {
        _unitActionBar.Dock = DockStyle.Top;
        _unitActionBar.Height = 158;
        _unitActionBar.BackColor = Color.FromArgb(231, 242, 240);
        _unitActionBar.Padding = new Padding(8, 8, 8, 8);
        _unitActionBar.BorderStyle = BorderStyle.FixedSingle;
        _unitActionBar.RightToLeft = RightToLeft.Yes;

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            RightToLeft = RightToLeft.Yes,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 0, 0, 4)
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));

        var buttonsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 0),
            Padding = new Padding(0, 2, 0, 2)
        };
        buttonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        buttonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        buttonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));

        ConfigureUnitActionButton(_reviewUnitButton, "חזרת יחידה", (_, _) => StartSelectedUnitReview());
        ConfigureUnitActionButton(_resetUnitCycleButton, "איפוס מחזור", (_, _) => ResetSelectedUnitCycle());
        ConfigureUnitActionButton(_markUnitLearnedButton, "סמן כנלמד", (_, _) => MarkSelectedUnitAsLearned());
        _reviewUnitButton.Dock = DockStyle.Fill;
        _resetUnitCycleButton.Dock = DockStyle.Fill;
        _markUnitLearnedButton.Dock = DockStyle.Fill;
        buttonsPanel.Controls.Add(_markUnitLearnedButton, 0, 0);
        buttonsPanel.Controls.Add(_resetUnitCycleButton, 1, 0);
        buttonsPanel.Controls.Add(_reviewUnitButton, 2, 0);

        _unitStatusLabel.Dock = DockStyle.Fill;
        _unitStatusLabel.TextAlign = ContentAlignment.MiddleRight;
        _unitStatusLabel.RightToLeft = RightToLeft.Yes;
        _unitStatusLabel.Padding = new Padding(12, 4, 12, 4);
        _unitStatusLabel.Font = new Font("Microsoft Sans Serif", 9.5F, FontStyle.Bold);
        _unitStatusLabel.Text = "בחרו יחידה בספרייה כדי לעקוב אחר לימוד וחזרות.";

        rootLayout.Controls.Add(_unitStatusLabel, 0, 0);
        rootLayout.Controls.Add(buttonsPanel, 0, 1);
        _unitActionBar.Controls.Add(rootLayout);
    }

    private static void ConfigureUnitActionButton(Button button, string text, EventHandler onClick)
    {
        button.Text = text;
        button.Width = Math.Max(128, TextRenderer.MeasureText(text, new Font("Microsoft Sans Serif", 9.5F, FontStyle.Bold)).Width + 30);
        button.Height = 42;
        button.Margin = new Padding(2, 2, 2, 2);
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Color.White;
        button.FlatAppearance.BorderColor = Color.FromArgb(45, 117, 110);
        UiLayoutHelper.StyleActionButton(button, 128, 42);
        button.Click += onClick;
    }

    private void BuildBulkActionBar()
    {
        _bulkActionBar.Dock = DockStyle.Top;
        _bulkActionBar.Height = 114;
        _bulkActionBar.FlowDirection = FlowDirection.RightToLeft;
        _bulkActionBar.RightToLeft = RightToLeft.No;
        _bulkActionBar.WrapContents = true;
        _bulkActionBar.BackColor = Color.FromArgb(222, 238, 236);
        _bulkActionBar.Padding = new Padding(6, 10, 6, 10);
        _bulkActionBar.Controls.Add(CreateHeaderActionButton("חזרה על הנבחרים", (_, _) => ReviewSelectedCards()));
        _bulkActionBar.Controls.Add(CreateHeaderActionButton("מחיקה מרובה", (_, _) => BulkDeleteCards()));
        _bulkActionBar.Controls.Add(CreateHeaderActionButton("שינוי קושי", (_, _) => BulkChangeDifficulty()));
        _bulkActionBar.Controls.Add(CreateHeaderActionButton("הסר תגית", (_, _) => BulkRemoveTag()));
        _bulkActionBar.Controls.Add(CreateHeaderActionButton("הוסף תגית", (_, _) => BulkAddTag()));
        _bulkActionBar.Controls.Add(CreateHeaderActionButton("נקה בחירה", (_, _) => SetAllCardSelections(false)));
        _bulkActionBar.Controls.Add(CreateHeaderActionButton("בחר הכל", (_, _) => SetAllCardSelections(true)));
    }

    private StatusStrip CreateStatusStrip()
    {
        var statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            RenderMode = ToolStripRenderMode.System,
            RightToLeft = RightToLeft.Yes,
            SizingGrip = false
        };

        ApplyMirroredRtlWhenAvailable(statusStrip);
        _statusLabel.Text = "מוכן";
        statusStrip.Items.Add(_statusLabel);
        return statusStrip;
    }

    private void InitializeScreen()
    {
        AppendStartupLog("InitializeScreen start");
        ReloadTree();
        RefreshTagBindings();
        RefreshPausedSessionIndicator();
        _subjectTreeView.SelectedNode = null;
        _selectedSubjectId = null;
        ShowStatusView();
        AppendStartupLog($"InitializeScreen complete | root nodes={_subjectTreeView.Nodes.Count}");
        StartPendingVerificationIfNeeded();
    }

    private void StartPendingVerificationIfNeeded()
    {
        if (_verificationStarted || (_verificationRequest is null && _coreVerificationRequest is null && _managementVerificationRequest is null && _reviewVerificationRequest is null && _dailyWorkflowVerificationRequest is null && _libraryVerificationRequest is null && _tagFlowVerificationRequest is null && _bottomActionsVerificationRequest is null && _productVerificationRequest is null && _studyUnitVerificationRequest is null && _freshStartVerificationRequest is null && _maintenanceVerificationRequest is null) || IsDisposed)
        {
            return;
        }

        _verificationStarted = true;
        if (_maintenanceVerificationRequest is not null)
        {
            BeginInvoke(new Action(() => _ = RunMaintenanceVerificationAsync(_maintenanceVerificationRequest)));
            return;
        }

        if (_productVerificationRequest is not null)
        {
            BeginInvoke(new Action(() => _ = RunProductVerificationAsync(_productVerificationRequest)));
            return;
        }

        if (_studyUnitVerificationRequest is not null)
        {
            BeginInvoke(new Action(() => _ = RunStudyUnitVerificationAsync(_studyUnitVerificationRequest)));
            return;
        }

        if (_freshStartVerificationRequest is not null)
        {
            BeginInvoke(new Action(() => _ = RunFreshStartVerificationAsync(_freshStartVerificationRequest)));
            return;
        }

        if (_tagFlowVerificationRequest is not null)
        {
            BeginInvoke(new Action(() => _ = RunTagFlowVerificationAsync(_tagFlowVerificationRequest)));
            return;
        }

        if (_bottomActionsVerificationRequest is not null)
        {
            BeginInvoke(new Action(() => _ = RunBottomActionsVerificationAsync(_bottomActionsVerificationRequest)));
            return;
        }

        if (_libraryVerificationRequest is not null)
        {
            BeginInvoke(new Action(() => _ = RunLibraryVerificationAsync(_libraryVerificationRequest)));
            return;
        }

        if (_dailyWorkflowVerificationRequest is not null)
        {
            BeginInvoke(new Action(() => _ = RunDailyWorkflowVerificationAsync(_dailyWorkflowVerificationRequest)));
            return;
        }

        if (_reviewVerificationRequest is not null)
        {
            BeginInvoke(new Action(() => _ = RunReviewVerificationAsync(_reviewVerificationRequest)));
            return;
        }

        if (_managementVerificationRequest is not null)
        {
            BeginInvoke(new Action(() => _ = RunManagementVerificationAsync(_managementVerificationRequest)));
            return;
        }

        if (_coreVerificationRequest is not null)
        {
            BeginInvoke(new Action(() => _ = RunCoreVerificationAsync(_coreVerificationRequest)));
            return;
        }

        BeginInvoke(new Action(() => _ = RunTanakhVerificationAsync(
            _verificationRequest!.OverviewScreenshotPath,
            _verificationRequest.DetailScreenshotPath,
            _verificationRequest.ReportPath)));
    }

    private void ReloadTree()
    {
        _subjectTreeView.BeginUpdate();
        _subjectTreeView.Nodes.Clear();
        PopulateTreeNodes(_subjectTreeView.Nodes, _database.GetTreeChildren(null));
        _subjectTreeView.EndUpdate();
    }

    private void RefreshTagBindings()
    {
        var tags = _database.GetTags();
        _searchFilterView.BindTags(tags);
        _reviewFilterView.BindTags(tags);
        _tagsManagerView.BindTags(tags);
    }

    private void HandleTreeBeforeExpand(TreeViewCancelEventArgs e)
    {
        if (e.Node?.Nodes.Count == 1 && e.Node.Nodes[0].Text == DummyNodeText)
        {
            if (!TryPopulateTreeChildren(e.Node))
            {
                e.Cancel = true;
            }
        }
    }

    private void PopulateTreeChildren(TreeNode parentNode)
    {
        if (parentNode.Tag is not int parentId)
        {
            return;
        }

        _database.EnsureSubjectChildrenLoaded(parentId);
        parentNode.Nodes.Clear();
        PopulateTreeNodes(parentNode.Nodes, _database.GetTreeChildren(parentId));
    }

    private bool TryPopulateTreeChildren(TreeNode parentNode)
    {
        try
        {
            PopulateTreeChildren(parentNode);
            return true;
        }
        catch (Exception exception)
        {
            parentNode.Nodes.Clear();
            parentNode.Nodes.Add(new TreeNode(DummyNodeText));
            ShowTreeLoadError(exception);
            return false;
        }
    }

    private static void PopulateTreeNodes(TreeNodeCollection targetNodes, IReadOnlyList<SubjectNodeModel> subjects)
    {
        foreach (var subject in subjects)
        {
            var node = new TreeNode(subject.Name) { Tag = subject.Id };
            ApplyTreeNodeUnitPresentation(node, subject);
            if (subject.HasChildren)
            {
                node.Nodes.Add(new TreeNode(DummyNodeText));
            }

            targetNodes.Add(node);
        }
    }

    private static void ApplyTreeNodeUnitPresentation(TreeNode node, SubjectNodeModel subject)
    {
        if (subject.UnitStatus == StudyUnitStatus.NotStudied)
        {
            node.ToolTipText = "לא נלמד עדיין";
            return;
        }

        node.ToolTipText = subject.UnitStatusText;
        switch (subject.UnitStatus)
        {
            case StudyUnitStatus.DueReview:
                node.BackColor = Color.FromArgb(255, 248, 211);
                node.ForeColor = Color.FromArgb(120, 72, 0);
                break;
            case StudyUnitStatus.EarlyReviewNeeded:
                node.BackColor = Color.FromArgb(255, 230, 227);
                node.ForeColor = Color.FromArgb(145, 53, 34);
                break;
            case StudyUnitStatus.ReviewCompleted:
                node.BackColor = Color.FromArgb(228, 244, 230);
                node.ForeColor = Color.FromArgb(33, 96, 47);
                break;
            default:
                node.BackColor = Color.FromArgb(235, 247, 245);
                node.ForeColor = Color.FromArgb(34, 90, 85);
                break;
        }
    }

    private void HandleTreeSelection()
    {
        _selectedSubjectId = _subjectTreeView.SelectedNode?.Tag is int subjectId ? subjectId : null;
        UpdateSelectedNodeLabels();

        switch (_currentWorkspace)
        {
            case MainWorkspaceMode.Questions:
                ShowStudyCards();
                break;
            case MainWorkspaceMode.Search when _searchFilterView.LimitToSelectedNode:
                RunSearch();
                break;
            case MainWorkspaceMode.Review when _reviewFilterView.RestrictToSelectedNode:
                RunReview();
                break;
        }
    }

    private void UpdateSelectedNodeLabels()
    {
        var path = _selectedSubjectId.HasValue ? TranslatePath(_database.GetSubjectPath(_selectedSubjectId.Value)) : "כל הספרייה";
        _searchFilterView.SetSelectedNodePath(path);
        _reviewFilterView.SetSelectedNodePath(path);
        RefreshSelectedUnitStatus();
    }

    private void ShowStudyCards()
    {
        _currentWorkspace = MainWorkspaceMode.Questions;
        ShowCardsWorkspace();
        _searchFilterView.Visible = false;
        _reviewFilterView.Visible = false;
        ActivateTab(_questionsTabItem);

        var items = _database.GetStudyItemsForSubject(_selectedSubjectId);
        _contentTitleLabel.Text = _selectedSubjectId.HasValue
            ? $"יחידות לימוד / {TranslatePath(_database.GetSubjectPath(_selectedSubjectId.Value))}"
            : "יחידות לימוד / כל הספרייה";

        PopulateCards(items);
        _statusLabel.Text = $"נטענו {items.Count} יחידות לימוד | לביצוע כעת: {items.Count(item => item.IsDue)}";
    }

    private void ShowSearchView()
    {
        _currentWorkspace = MainWorkspaceMode.Search;
        ShowCardsWorkspace();
        _searchFilterView.Visible = true;
        _reviewFilterView.Visible = false;
        ActivateTab(_searchTabItem);
        _contentTitleLabel.Text = "חיפוש / יחידות לימוד";
        UpdateSelectedNodeLabels();
        RunSearch();
    }

    private void ShowReviewView()
    {
        _currentWorkspace = MainWorkspaceMode.Review;
        ShowCardsWorkspace();
        _reviewFilterView.Visible = true;
        _searchFilterView.Visible = false;
        ActivateTab(_reviewTabItem);
        _contentTitleLabel.Text = "חזרה יומית / יחידות לימוד";
        UpdateSelectedNodeLabels();
        RefreshPausedSessionIndicator();
        RunReview();
    }

    private void ShowTagsView()
    {
        _currentWorkspace = MainWorkspaceMode.Tags;
        _cardsWorkspace.Visible = false;
        _statusView.Visible = false;
        _tagsManagerView.Visible = true;
        RefreshTagBindings();
        ActivateTab(_tagsTabItem);
        _contentTitleLabel.Text = "תגיות / ניהול";
        _statusLabel.Text = "ניהול תגיות פעיל.";
    }

    private void ShowStatusView()
    {
        _currentWorkspace = MainWorkspaceMode.Status;
        _cardsWorkspace.Visible = false;
        _tagsManagerView.Visible = false;
        _statusView.Visible = true;
        ActivateTab(_statusTabItem);
        _contentTitleLabel.Text = "לוח יומי / תורים / חולשות";

        RefreshDashboard();
    }

    private void ShowCardsWorkspace()
    {
        _cardsWorkspace.Visible = true;
        _statusView.Visible = false;
        _tagsManagerView.Visible = false;
        _reviewSessionView.Visible = false;
        _cardPanel.Visible = true;
        _unitActionBar.Visible = true;
        _bulkActionBar.Visible = true;
        RefreshSelectedUnitStatus();
    }

    private void RefreshPausedSessionIndicator()
    {
        var pausedSession = _database.GetPausedReviewSession();
        if (pausedSession is null)
        {
            _reviewFilterView.SetPausedSessionSummary("אין סשן מושהה.", false);
            return;
        }

        _reviewFilterView.SetPausedSessionSummary(
            $"סשן מושהה: {pausedSession.Title} | נשארו {pausedSession.PendingItemIds.Count} | עודכן {pausedSession.UpdatedAt:HH:mm}",
            true);
    }

    private void RefreshDashboard()
    {
        var dashboard = _database.GetDailyDashboardModel(_selectedSubjectId);
        var presets = _database.GetReviewPresets();
        var weakSpots = _database.GetWeakSpots();
        var tags = _database.GetTags();
        _statusView.Bind(dashboard, presets, weakSpots, tags);
        _statusLabel.Text = $"להיום {dashboard.DueTodayCount} | באיחור {dashboard.OverdueCount} | חדשים {dashboard.NewCount} | נכשלו לאחרונה {dashboard.FailedRecentlyCount}";
    }

    private void RefreshSelectedUnitStatus()
    {
        if (!_selectedSubjectId.HasValue)
        {
            _unitStatusLabel.Text = "בחרו יחידה בספרייה כדי לסמן לימוד ולהפעיל חזרת יחידה.";
            _markUnitLearnedButton.Enabled = false;
            _resetUnitCycleButton.Enabled = false;
            _reviewUnitButton.Enabled = false;
            _markUnitLearnedButton.Text = "סמן כנלמד";
            return;
        }

        var progress = _database.GetStudyUnitProgress(_selectedSubjectId.Value);
        var path = TranslatePath(_database.GetSubjectPath(_selectedSubjectId.Value));
        if (progress is null)
        {
            _unitStatusLabel.Text = $"יחידה נבחרת: {path} | מצב: לא נלמד | לחצו על \"סמן כנלמד\" כדי להתחיל מעקב.";
            _markUnitLearnedButton.Text = "סמן כנלמד";
            _markUnitLearnedButton.Enabled = true;
            _resetUnitCycleButton.Enabled = false;
            _reviewUnitButton.Enabled = _database.GetStudyItemsForSubject(_selectedSubjectId.Value).Count > 0;
            return;
        }

        var nextReviewText = progress.NextReviewDate.HasValue
            ? progress.NextReviewDate.Value.ToString("dd/MM/yyyy")
            : "ללא";
        _unitStatusLabel.Text =
            $"יחידה: {path} | מצב: {AppDatabase.TranslateStudyUnitStatus(progress)} | " +
            $"שלב: {Math.Max(progress.CurrentStage, 1)} | לימודים: {progress.StudyCount} | " +
            $"חזרות שהושלמו: {progress.CompletedReviewCount} | חזרה הבאה: {nextReviewText} | " +
            $"תוצאה אחרונה: {AppDatabase.TranslateStudyUnitResult(progress.LastResult)}";
        _markUnitLearnedButton.Text = progress.IsCycleCompleted ? "התחל מחזור חדש" : "למדתי שוב";
        _markUnitLearnedButton.Enabled = true;
        _resetUnitCycleButton.Enabled = true;
        _reviewUnitButton.Enabled = progress.LinkedCardCount > 0;
    }

    private void MarkSelectedUnitAsLearned()
    {
        if (!_selectedSubjectId.HasValue)
        {
            _statusLabel.Text = "אין יחידה נבחרת לסימון לימוד.";
            return;
        }

        var previous = _database.GetStudyUnitProgress(_selectedSubjectId.Value);
        var progress = _database.MarkSubjectAsLearned(_selectedSubjectId.Value, previous?.IsCycleCompleted == true);
        ReloadTree();
        SelectNodeBySubjectId(_selectedSubjectId.Value);
        RefreshSelectedUnitStatus();
        RefreshDashboard();
        _statusLabel.Text = $"היחידה סומנה כנלמדת. החזרה הבאה: {(progress.NextReviewDate.HasValue ? progress.NextReviewDate.Value.ToString("dd/MM/yyyy") : "ללא")}.";
    }

    private void ResetSelectedUnitCycle()
    {
        if (!_selectedSubjectId.HasValue)
        {
            _statusLabel.Text = "אין יחידה נבחרת לאיפוס.";
            return;
        }

        var progress = _database.ResetStudyUnitCycle(_selectedSubjectId.Value);
        ReloadTree();
        SelectNodeBySubjectId(_selectedSubjectId.Value);
        RefreshSelectedUnitStatus();
        RefreshDashboard();
        _statusLabel.Text = $"מחזור היחידה אופס. החזרה הבאה: {(progress.NextReviewDate.HasValue ? progress.NextReviewDate.Value.ToString("dd/MM/yyyy") : "ללא")}.";
    }

    private void StartSelectedUnitReview()
    {
        if (!_selectedSubjectId.HasValue)
        {
            _statusLabel.Text = "אין יחידה נבחרת להפעלת חזרה.";
            return;
        }

        StartUnitReviewSession(_selectedSubjectId.Value, MainWorkspaceMode.Questions);
    }

    private void StartNextDueUnitReview()
    {
        var nextUnit = _database.GetStudyUnitsDueForReview().FirstOrDefault();
        if (nextUnit is null)
        {
            _statusLabel.Text = "אין כרגע יחידות שהגיע זמן חזרתן.";
            return;
        }

        SelectNodeBySubjectId(nextUnit.SubjectId);
        StartUnitReviewSession(nextUnit.SubjectId, MainWorkspaceMode.Status);
    }

    private void StartFailedUnitReview()
    {
        var failedUnit = _database.GetStudyUnitsFailedRecently(_selectedSubjectId).FirstOrDefault();
        if (failedUnit is null)
        {
            _statusLabel.Text = "אין יחידות חלשות להפעלת חזרה.";
            return;
        }

        SelectNodeBySubjectId(failedUnit.SubjectId);
        StartUnitReviewSession(failedUnit.SubjectId, MainWorkspaceMode.Status);
    }

    private void StartUnitReviewSession(int subjectId, MainWorkspaceMode returnMode)
    {
        var items = _database.GetStudyItemsForSubject(subjectId);
        if (items.Count == 0)
        {
            _statusLabel.Text = "אין יחידות לימוד משויכות לצומת זה, ולכן אין חזרה להפעלה.";
            return;
        }

        var progress = _database.GetStudyUnitProgress(subjectId) ?? _database.MarkSubjectAsLearned(subjectId);
        _isUnitReviewSession = true;
        _activeUnitReviewSubjectId = subjectId;
        _unitSessionRatings.Clear();
        StartReviewSession(
            items,
            $"חזרת יחידה / {TranslatePath(_database.GetSubjectPath(subjectId))}",
            returnMode,
            ReviewSessionOrderMode.Default,
            isUnitSession: true,
            unitSubjectId: subjectId);
        _statusLabel.Text = $"נפתחה חזרת יחידה עבור {TranslatePath(_database.GetSubjectPath(subjectId))}. שלב נוכחי: {Math.Max(progress.CurrentStage, 1)}.";
    }

    private void RunSearch()
    {
        var items = _database.SearchStudyItems(new StudySearchQuery
        {
            SearchText = _searchFilterView.SearchText,
            Book = _searchFilterView.Book,
            Chapter = _searchFilterView.Chapter,
            Verse = _searchFilterView.Verse,
            SubjectId = _selectedSubjectId,
            TagId = _searchFilterView.SelectedTagId,
            LimitToSelectedNode = _searchFilterView.LimitToSelectedNode,
            FailedRecentlyOnly = _searchFilterView.FailedRecentlyOnly,
            Difficulty = _searchFilterView.SelectedDifficulty
        });

        PopulateCards(items);
        _searchFilterView.SetResultSummary($"נמצאו {items.Count} יחידות לימוד");
        _statusLabel.Text = $"חיפוש הושלם: {items.Count} תוצאות.";
    }

    private void RunReview()
    {
        var items = _database.GetStudyItemsForReview(new StudyReviewFilter
        {
            SubjectId = _selectedSubjectId,
            TagId = _reviewFilterView.SelectedTagId,
            RestrictToSelectedNode = _reviewFilterView.RestrictToSelectedNode,
            DueOnly = _reviewFilterView.DueOnly,
            FailedRecentlyOnly = _reviewFilterView.FailedRecentlyOnly,
            Difficulty = _reviewFilterView.SelectedDifficulty
        });

        PopulateCards(items);
        _reviewFilterView.SetResultSummary($"לתרגול כעת: {items.Count} יחידות לימוד");
        _statusLabel.Text = $"חזרה מסוננת: {items.Count} יחידות לימוד.";
    }

    private void StartSmartQueue(SmartQueueKind queueKind, int? tagId = null)
    {
        IReadOnlyList<StudyItemModel> items;
        string title;

        switch (queueKind)
        {
            case SmartQueueKind.Daily:
                items = _database.GetSmartQueueItems(queueKind);
                title = "תור חכם / חזרה יומית";
                break;
            case SmartQueueKind.Overdue:
                items = _database.GetSmartQueueItems(queueKind);
                title = "תור חכם / באיחור";
                break;
            case SmartQueueKind.FailedRecently:
                items = _database.GetSmartQueueItems(queueKind);
                title = "תור חכם / נכשלים לאחרונה";
                break;
            case SmartQueueKind.Hard:
                items = _database.GetSmartQueueItems(queueKind);
                title = "תור חכם / קשים";
                break;
            case SmartQueueKind.New:
                items = _database.GetSmartQueueItems(queueKind);
                title = "תור חכם / חדשים";
                break;
            case SmartQueueKind.ReviewLater:
                items = _database.GetSmartQueueItems(queueKind);
                title = "תור חכם / מסומנים לעיון חוזר";
                break;
            case SmartQueueKind.SelectedNode:
                if (!_selectedSubjectId.HasValue)
                {
                    _statusLabel.Text = "אין צומת נבחר לתור זה.";
                    return;
                }

                items = _database.GetSmartQueueItems(queueKind, _selectedSubjectId);
                title = $"תור חכם / {TranslatePath(_database.GetSubjectPath(_selectedSubjectId.Value))}";
                break;
            case SmartQueueKind.Tag:
                if (!tagId.HasValue)
                {
                    _statusLabel.Text = "בחרו תגית לתור זה.";
                    return;
                }

                items = _database.GetSmartQueueItems(queueKind, null, tagId);
                var tagName = _database.GetTags().FirstOrDefault(tag => tag.Id == tagId.Value)?.Name ?? "תגית";
                title = $"תור חכם / תגית {tagName}";
                break;
            default:
                return;
        }

        StartReviewSession(items, title, MainWorkspaceMode.Status, ReviewSessionOrderMode.Default);
    }

    private void RunReviewPreset(ReviewPresetModel preset)
    {
        var items = _database.GetStudyItemsForReview(new StudyReviewFilter
        {
            SubjectId = preset.SubjectId,
            TagId = preset.TagId,
            RestrictToSelectedNode = preset.RestrictToSubject,
            DueOnly = preset.DueOnly,
            FailedRecentlyOnly = preset.FailedRecentlyOnly,
            Difficulty = preset.Difficulty
        });

        StartReviewSession(items, $"פריסט / {preset.Name}", MainWorkspaceMode.Status, preset.OrderMode);
    }

    private void CreateReviewPreset()
    {
        using var dialog = new ReviewPresetForm("פריסט חזרה חדש", _selectedSubjectId.HasValue ? TranslatePath(_database.GetSubjectPath(_selectedSubjectId.Value)) : string.Empty, _database.GetTags());
        PrepareDialog(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _database.AddReviewPreset(
            dialog.PresetName,
            dialog.RestrictToSelectedNode ? _selectedSubjectId : null,
            dialog.RestrictToSelectedNode,
            dialog.SelectedTagId,
            dialog.DueOnly,
            dialog.FailedRecentlyOnly,
            dialog.SelectedDifficulty,
            dialog.SelectedOrderMode);
        RefreshDashboard();
        _statusLabel.Text = $"נשמר פריסט: {dialog.PresetName}";
    }

    private void EditReviewPreset(ReviewPresetModel preset)
    {
        var selectedPath = preset.SubjectId.HasValue ? TranslatePath(_database.GetSubjectPath(preset.SubjectId.Value)) : string.Empty;
        using var dialog = new ReviewPresetForm("עריכת פריסט", selectedPath, _database.GetTags(), preset);
        PrepareDialog(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _database.UpdateReviewPreset(
            preset.Id,
            dialog.PresetName,
            dialog.RestrictToSelectedNode ? (preset.SubjectId ?? _selectedSubjectId) : null,
            dialog.RestrictToSelectedNode,
            dialog.SelectedTagId,
            dialog.DueOnly,
            dialog.FailedRecentlyOnly,
            dialog.SelectedDifficulty,
            dialog.SelectedOrderMode);
        RefreshDashboard();
        _statusLabel.Text = $"הפריסט עודכן: {dialog.PresetName}";
    }

    private void DeleteReviewPreset(ReviewPresetModel preset)
    {
        var result = MessageBox.Show(
            this,
            $"למחוק את הפריסט '{preset.Name}'?",
            "מחיקת פריסט",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        if (result != DialogResult.Yes)
        {
            return;
        }

        _database.DeleteReviewPreset(preset.Id);
        RefreshDashboard();
        _statusLabel.Text = $"הפריסט נמחק: {preset.Name}";
    }

    private void StartFilteredReviewSession()
    {
        var items = _database.GetStudyItemsForReview(new StudyReviewFilter
        {
            SubjectId = _selectedSubjectId,
            TagId = _reviewFilterView.SelectedTagId,
            RestrictToSelectedNode = _reviewFilterView.RestrictToSelectedNode,
            DueOnly = _reviewFilterView.DueOnly,
            FailedRecentlyOnly = _reviewFilterView.FailedRecentlyOnly,
            Difficulty = _reviewFilterView.SelectedDifficulty
        });

        var title = _selectedSubjectId.HasValue
            ? $"סשן חזרה / {TranslatePath(_database.GetSubjectPath(_selectedSubjectId.Value))}"
            : "סשן חזרה / כל הספרייה";

        StartReviewSession(items, title, MainWorkspaceMode.Review, _reviewFilterView.SelectedOrderMode);
    }

    private void StartReviewSession(IReadOnlyList<StudyItemModel> items, string title, MainWorkspaceMode returnMode, ReviewSessionOrderMode orderMode, bool preserveCounters = false, bool isUnitSession = false, int? unitSubjectId = null)
    {
        if (items.Count == 0)
        {
            _statusLabel.Text = "אין יחידות לימוד זמינות לסשן חזרה.";
            return;
        }

        _reviewSessionItems = OrderReviewSessionItems(items, orderMode).ToList();
        _isUnitReviewSession = isUnitSession;
        _activeUnitReviewSubjectId = isUnitSession ? unitSubjectId : null;
        if (!preserveCounters)
        {
            _unitSessionRatings.Clear();
        }
        _reviewFailedItemIds = preserveCounters ? _reviewFailedItemIds : [];
        _reviewSessionSkippedItemIds = preserveCounters ? _reviewSessionSkippedItemIds : [];
        _reviewSessionReviewLaterItemIds = preserveCounters ? _reviewSessionReviewLaterItemIds : [];
        _reviewSessionCompletedCount = preserveCounters ? _reviewSessionCompletedCount : 0;
        _reviewSessionFailedCount = preserveCounters ? _reviewSessionFailedCount : 0;
        _reviewSessionHighRatingCount = preserveCounters ? _reviewSessionHighRatingCount : 0;
        _reviewSessionLowRatingCount = preserveCounters ? _reviewSessionLowRatingCount : 0;
        _reviewSessionTitle = title;
        _reviewSessionReturnMode = returnMode;
        _reviewSessionOrderMode = orderMode;
        _currentWorkspace = MainWorkspaceMode.ReviewSession;
        _database.ClearPausedReviewSession();

        _cardsWorkspace.Visible = true;
        _statusView.Visible = false;
        _tagsManagerView.Visible = false;
        _searchFilterView.Visible = false;
        _reviewFilterView.Visible = false;
        _bulkActionBar.Visible = false;
        _cardPanel.Visible = false;
        _reviewSessionView.Visible = true;
        ActivateTab(_reviewTabItem);
        _contentTitleLabel.Text = title;

        ShowCurrentReviewSessionItem();
        _statusLabel.Text = $"התחיל סשן חזרה עם {_reviewSessionItems.Count} יחידות לימוד.";
        RefreshPausedSessionIndicator();
    }

    private void ShowCurrentReviewSessionItem()
    {
        if (_reviewSessionItems.Count == 0)
        {
            CompleteReviewSession();
            return;
        }

        var currentItem = _reviewSessionItems[0];
        var progress = new ReviewSessionProgress
        {
            TotalCount = _reviewSessionCompletedCount + _reviewSessionItems.Count,
            CompletedCount = _reviewSessionCompletedCount,
            FailedCount = _reviewSessionFailedCount,
            RemainingCount = _reviewSessionItems.Count
        };

        _contentTitleLabel.Text = $"{_reviewSessionTitle} / יחידה {_reviewSessionCompletedCount + 1} מתוך {progress.TotalCount}";
        _reviewSessionView.ShowReviewItem(currentItem, progress, _reviewSessionTitle, _reviewSessionOrderMode);
    }

    private void HandleReviewSessionRating(ReviewRating rating)
    {
        if (_reviewSessionItems.Count == 0)
        {
            return;
        }

        var currentItem = _reviewSessionItems[0];
        var updated = _database.RateStudyItem(currentItem.Id, rating);
        if (_isUnitReviewSession)
        {
            _unitSessionRatings.Add(rating);
        }

        _reviewSessionCompletedCount++;
        _reviewSessionSkippedItemIds.Remove(currentItem.Id);
        _reviewSessionReviewLaterItemIds.Remove(currentItem.Id);
        if (rating == ReviewRating.Again)
        {
            _reviewSessionFailedCount++;
            _reviewFailedItemIds.Add(currentItem.Id);
        }

        if (rating is ReviewRating.Easy or ReviewRating.Perfect)
        {
            _reviewSessionHighRatingCount++;
        }

        if (rating is ReviewRating.Again or ReviewRating.Hard)
        {
            _reviewSessionLowRatingCount++;
        }

        _reviewSessionItems.RemoveAt(0);
        _statusLabel.Text = $"היחידה '{updated.Topic}' דורגה כ-{TranslateRating(rating)}. נשארו {_reviewSessionItems.Count} יחידות לימוד.";
        ShowCurrentReviewSessionItem();
    }

    private void SkipCurrentReviewItem()
    {
        if (_reviewSessionItems.Count == 0)
        {
            return;
        }

        var currentItem = _reviewSessionItems[0];
        _reviewSessionItems.RemoveAt(0);
        _reviewSessionItems.Add(currentItem);
        _reviewSessionSkippedItemIds.Add(currentItem.Id);
        _statusLabel.Text = $"היחידה '{currentItem.Topic}' דולגה ותופיע שוב בהמשך.";
        ShowCurrentReviewSessionItem();
    }

    private void MoveCurrentReviewItemToEnd()
    {
        if (_reviewSessionItems.Count == 0)
        {
            return;
        }

        var currentItem = _reviewSessionItems[0];
        _reviewSessionItems.RemoveAt(0);
        _reviewSessionItems.Add(currentItem);
        _reviewSessionReviewLaterItemIds.Add(currentItem.Id);
        _database.SetReviewLaterFlag(currentItem.Id, true);
        _statusLabel.Text = $"היחידה '{currentItem.Topic}' סומנה לעיון חוזר בסוף הסשן.";
        ShowCurrentReviewSessionItem();
    }

    private void CompleteReviewSession()
    {
        StudyUnitReviewCompletion? unitCompletion = null;
        if (_isUnitReviewSession && _activeUnitReviewSubjectId.HasValue && _unitSessionRatings.Count > 0)
        {
            unitCompletion = _database.CompleteStudyUnitReview(_activeUnitReviewSubjectId.Value, _unitSessionRatings);
        }

        var summary = new ReviewSessionSummary
        {
            Title = unitCompletion is null ? "סיכום סשן חזרה" : "סיכום חזרת יחידה",
            TotalCount = _reviewSessionCompletedCount + _reviewSessionItems.Count,
            CompletedCount = _reviewSessionCompletedCount,
            FailedCount = _reviewSessionFailedCount,
            HighRatingCount = _reviewSessionHighRatingCount,
            LowRatingCount = _reviewSessionLowRatingCount,
            RemainingCount = _reviewSessionItems.Count,
            ReviewLaterCount = _reviewSessionReviewLaterItemIds.Count,
            SkippedCount = _reviewSessionSkippedItemIds.Count,
            FailedItemIds = [.. _reviewFailedItemIds],
            AdditionalSummaryText = unitCompletion is null
                ? string.Empty
                : $"יחידה: {TranslatePath(_database.GetSubjectPath(unitCompletion.Progress.SubjectId))}{Environment.NewLine}" +
                  $"תוצאת יחידה: {AppDatabase.TranslateStudyUnitResult(unitCompletion.Result)}{Environment.NewLine}" +
                  $"שלב קודם: {unitCompletion.StageBefore} | שלב חדש: {unitCompletion.StageAfter}{Environment.NewLine}" +
                  $"חזרה הבאה: {(unitCompletion.Progress.NextReviewDate.HasValue ? unitCompletion.Progress.NextReviewDate.Value.ToString("dd/MM/yyyy") : "המחזור הושלם")}"
        };

        _reviewSessionView.ShowSummary(summary);
        _contentTitleLabel.Text = unitCompletion is null ? "סיכום סשן חזרה" : "סיכום חזרת יחידה";
        _statusLabel.Text = unitCompletion is null
            ? $"הסשן הסתיים: הושלמו {summary.CompletedCount}, נכשלו {summary.FailedCount}, נשארו {summary.RemainingCount}."
            : $"חזרת היחידה הושלמה: {AppDatabase.TranslateStudyUnitResult(unitCompletion.Result)} | חזרה הבאה: {(unitCompletion.Progress.NextReviewDate.HasValue ? unitCompletion.Progress.NextReviewDate.Value.ToString("dd/MM/yyyy") : "המחזור הושלם")}.";
        _database.ClearPausedReviewSession();
        if (unitCompletion is not null)
        {
            ReloadTree();
            SelectNodeBySubjectId(unitCompletion.Progress.SubjectId);
            RefreshSelectedUnitStatus();
            RefreshDashboard();
        }
        RefreshPausedSessionIndicator();
    }

    private void RetryFailedReviewSession()
    {
        if (_reviewFailedItemIds.Count == 0)
        {
            _statusLabel.Text = "אין יחידות לימוד שנכשלו בסשן האחרון.";
            return;
        }

        var failedItems = _database.GetStudyItemsByIds([.. _reviewFailedItemIds]);
        _reviewFailedItemIds = [];
        _reviewSessionSkippedItemIds = [];
        _reviewSessionReviewLaterItemIds = [];
        _reviewSessionCompletedCount = 0;
        _reviewSessionFailedCount = 0;
        _reviewSessionHighRatingCount = 0;
        _reviewSessionLowRatingCount = 0;
        StartReviewSession(
            failedItems,
            "סשן נוסף על היחידות שדורגו חלש",
            _reviewSessionReturnMode,
            ReviewSessionOrderMode.Default,
            isUnitSession: _isUnitReviewSession,
            unitSubjectId: _activeUnitReviewSubjectId);
    }

    private void PauseReviewSession()
    {
        if (_reviewSessionItems.Count == 0)
        {
            return;
        }

        _database.SavePausedReviewSession(new PausedReviewSessionState
        {
            Title = _reviewSessionTitle,
            ReturnMode = _reviewSessionReturnMode.ToString(),
            OrderMode = _reviewSessionOrderMode,
            SelectedSubjectId = _selectedSubjectId,
            TotalCount = _reviewSessionCompletedCount + _reviewSessionItems.Count,
            CompletedCount = _reviewSessionCompletedCount,
            FailedCount = _reviewSessionFailedCount,
            HighRatingCount = _reviewSessionHighRatingCount,
            LowRatingCount = _reviewSessionLowRatingCount,
            PendingItemIds = _reviewSessionItems.Select(item => item.Id).ToList(),
            FailedItemIds = [.. _reviewFailedItemIds],
            ReviewLaterItemIds = [.. _reviewSessionReviewLaterItemIds],
            SkippedItemIds = [.. _reviewSessionSkippedItemIds],
            IsUnitReviewSession = _isUnitReviewSession,
            UnitSubjectId = _activeUnitReviewSubjectId,
            UnitRatings = [.. _unitSessionRatings],
            UpdatedAt = DateTime.Now
        });

        ExitReviewSession("הסשן הושהה ונשמר להמשך.");
    }

    private void ResumePausedReviewSession()
    {
        var pausedSession = _database.GetPausedReviewSession();
        if (pausedSession is null || pausedSession.PendingItemIds.Count == 0)
        {
            _statusLabel.Text = "אין סשן מושהה להמשך.";
            RefreshPausedSessionIndicator();
            return;
        }

        _reviewSessionItems = _database.GetStudyItemsByIds([.. pausedSession.PendingItemIds]).ToList();
        _reviewSessionItems = pausedSession.PendingItemIds
            .Select(id => _reviewSessionItems.First(item => item.Id == id))
            .ToList();
        _reviewFailedItemIds = [.. pausedSession.FailedItemIds];
        _reviewSessionSkippedItemIds = [.. pausedSession.SkippedItemIds];
        _reviewSessionReviewLaterItemIds = [.. pausedSession.ReviewLaterItemIds];
        _reviewSessionCompletedCount = pausedSession.CompletedCount;
        _reviewSessionFailedCount = pausedSession.FailedCount;
        _reviewSessionHighRatingCount = pausedSession.HighRatingCount;
        _reviewSessionLowRatingCount = pausedSession.LowRatingCount;
        _reviewSessionTitle = pausedSession.Title;
        _reviewSessionOrderMode = pausedSession.OrderMode;
        _isUnitReviewSession = pausedSession.IsUnitReviewSession;
        _activeUnitReviewSubjectId = pausedSession.UnitSubjectId;
        _unitSessionRatings.Clear();
        _unitSessionRatings.AddRange(pausedSession.UnitRatings);
        _reviewSessionReturnMode = Enum.TryParse<MainWorkspaceMode>(pausedSession.ReturnMode, out var returnMode)
            ? returnMode
            : MainWorkspaceMode.Review;
        _currentWorkspace = MainWorkspaceMode.ReviewSession;
        _database.ClearPausedReviewSession();

        _cardsWorkspace.Visible = true;
        _statusView.Visible = false;
        _tagsManagerView.Visible = false;
        _searchFilterView.Visible = false;
        _reviewFilterView.Visible = false;
        _bulkActionBar.Visible = false;
        _cardPanel.Visible = false;
        _reviewSessionView.Visible = true;
        ActivateTab(_reviewTabItem);

        ShowCurrentReviewSessionItem();
        _statusLabel.Text = $"המשך סשן: נשארו {_reviewSessionItems.Count} יחידות לימוד.";
        RefreshPausedSessionIndicator();
    }

    private IReadOnlyList<StudyItemModel> OrderReviewSessionItems(IReadOnlyList<StudyItemModel> items, ReviewSessionOrderMode orderMode)
    {
        return orderMode switch
        {
            ReviewSessionOrderMode.Random => items.OrderBy(_ => Guid.NewGuid()).ToList(),
            ReviewSessionOrderMode.HardFirst => items
                .OrderByDescending(item => item.Difficulty == StudyDifficulty.Hard)
                .ThenByDescending(item => item.Difficulty == StudyDifficulty.Medium)
                .ThenBy(item => item.EaseFactor)
                .ThenByDescending(item => item.Lapses)
                .ThenBy(item => item.Topic, StringComparer.Ordinal)
                .ToList(),
            ReviewSessionOrderMode.FailedFirst => items
                .OrderByDescending(item => item.LastRating is "Again" or "Hard")
                .ThenByDescending(item => item.Lapses)
                .ThenBy(item => item.DueDate)
                .ToList(),
            ReviewSessionOrderMode.NewFirst => items
                .OrderBy(item => item.TotalReviews)
                .ThenByDescending(item => item.CreatedAt)
                .ToList(),
            _ => items.ToList()
        };
    }

    private void ExitReviewSession(string statusMessage = "החזרה הסתיימה וחזרנו למסך הקודם.")
    {
        _reviewSessionView.ShowIdleState();
        _reviewSessionView.Visible = false;
        _reviewSessionItems = [];
        _reviewFailedItemIds = [];
        _reviewSessionSkippedItemIds = [];
        _reviewSessionReviewLaterItemIds = [];
        _reviewSessionCompletedCount = 0;
        _reviewSessionFailedCount = 0;
        _reviewSessionHighRatingCount = 0;
        _reviewSessionLowRatingCount = 0;
        _isUnitReviewSession = false;
        _activeUnitReviewSubjectId = null;
        _unitSessionRatings.Clear();
        ShowWorkspace(_reviewSessionReturnMode);
        _statusLabel.Text = statusMessage;
        RefreshPausedSessionIndicator();
    }

    private void PopulateCards(IReadOnlyList<StudyItemModel> items)
    {
        _cardPanel.SuspendLayout();
        foreach (Control control in _cardPanel.Controls)
        {
            control.Dispose();
        }

        _cardPanel.Controls.Clear();

        if (items.Count == 0)
        {
            var emptyLabel = new Label
            {
                AutoSize = false,
                Width = Math.Max(620, _cardPanel.ClientSize.Width - 30),
                Height = 72,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = ClassicPalette.CardBackground,
                Text = "אין יחידות לימוד בתצוגה זו. בחרו ענף אחר, הפעילו סינון אחר או הוסיפו יחידת לימוד חדשה.",
                TextAlign = ContentAlignment.MiddleCenter,
                RightToLeft = RightToLeft.Yes
            };

            _cardPanel.Controls.Add(emptyLabel);
        }
        else
        {
            foreach (var item in items)
            {
                var card = new StudyCardControl(item)
                {
                    Width = GetCardWidth()
                };

                card.RatingClicked += (_, rating) => HandleRating(item.Id, rating);
                card.EditClicked += (_, itemId) => OpenEditDialog(itemId);
                card.DeleteClicked += (_, itemId) => DeleteCard(itemId);
                card.TagsClicked += (_, itemId) => OpenTagsDialog(itemId);
                _cardPanel.Controls.Add(card);
            }
        }

        _cardPanel.ResumeLayout();
    }

    private void ResizeCards()
    {
        var width = GetCardWidth();
        foreach (Control control in _cardPanel.Controls)
        {
            control.Width = width;
        }
    }

    private int GetCardWidth()
    {
        return Math.Max(620, _cardPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);
    }

    private void HandleRating(int itemId, ReviewRating rating)
    {
        var updated = _database.RateStudyItem(itemId, rating);
        _statusLabel.Text = $"היחידה '{updated.Topic}' סומנה כ-{TranslateRating(rating)}. התאריך הבא: {updated.DueDate:dd/MM/yyyy}";
        RefreshCurrentView();
    }

    private void RefreshCurrentView()
    {
        if (_currentWorkspace == MainWorkspaceMode.ReviewSession)
        {
            ShowWorkspace(_reviewSessionReturnMode);
            return;
        }

        ShowWorkspace(_currentWorkspace);
    }

    private void ShowWorkspace(MainWorkspaceMode mode)
    {
        switch (mode)
        {
            case MainWorkspaceMode.Questions:
                ShowStudyCards();
                break;
            case MainWorkspaceMode.Search:
                ShowSearchView();
                break;
            case MainWorkspaceMode.Review:
                ShowReviewView();
                break;
            case MainWorkspaceMode.Tags:
                ShowTagsView();
                break;
            case MainWorkspaceMode.Status:
                ShowStatusView();
                break;
            case MainWorkspaceMode.ReviewSession:
                break;
        }
    }

    private void OpenAddDialog()
    {
        var subjects = _database.GetAssignableSubjects();
        var tags = _database.GetTags();
        using var dialog = new AddStudyItemForm(subjects, tags, _selectedSubjectId, null, _database.GetTags, CreateTagFromCardDialog);
        PrepareDialog(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var itemId = _database.AddStudyItem(dialog.Draft);
        _database.SetTagsForStudyItem(itemId, dialog.SelectedTagIds);
        RefreshTagBindings();
        ReloadTree();
        SelectNodeBySubjectId(dialog.SelectedSubjectId);
        RefreshCurrentView();
        _statusLabel.Text = $"נוספה יחידת לימוד חדשה: {dialog.Topic}";
    }

    private void OpenEditDialog(int itemId)
    {
        var item = _database.GetStudyItem(itemId);
        if (item is null)
        {
            return;
        }

        var subjects = _database.GetAssignableSubjects();
        var tags = _database.GetTags();
        using var dialog = new AddStudyItemForm(subjects, tags, item.SubjectId, item, _database.GetTags, CreateTagFromCardDialog);
        PrepareDialog(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _database.UpdateStudyItem(itemId, dialog.Draft);
        _database.SetTagsForStudyItem(itemId, dialog.SelectedTagIds);
        RefreshTagBindings();
        ReloadTree();
        SelectNodeBySubjectId(dialog.SelectedSubjectId);
        RefreshCurrentView();
        _statusLabel.Text = $"יחידת הלימוד עודכנה: {dialog.Topic}";
    }

    private void OpenTagsDialog(int itemId)
    {
        var item = _database.GetStudyItem(itemId);
        if (item is null)
        {
            return;
        }

        var tags = _database.GetTags();
        using var dialog = new TagAssignmentForm(item.Topic, tags, item.Tags.Select(tag => tag.Id).ToArray());
        PrepareDialog(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _database.SetTagsForStudyItem(itemId, dialog.SelectedTagIds);
        RefreshTagBindings();
        RefreshCurrentView();
        _statusLabel.Text = $"התגיות עודכנו עבור: {item.Topic}";
    }

    private void DeleteCard(int itemId)
    {
        var item = _database.GetStudyItem(itemId);
        if (item is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"למחוק את '{item.Topic}'?",
            "מחיקת יחידת לימוד",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

        if (result != DialogResult.Yes)
        {
            return;
        }

        _database.DeleteStudyItem(itemId);
        RefreshCurrentView();
        _statusLabel.Text = $"יחידת הלימוד נמחקה: {item.Topic}";
    }

    private void ImportCsv()
    {
        using var dialog = new CsvImportForm(
            _database,
            _selectedSubjectId,
            _selectedSubjectId.HasValue ? TranslatePath(_database.GetSubjectPath(_selectedSubjectId.Value)) : "ללא צומת ברירת מחדל");
        PrepareDialog(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ReloadTree();
        RefreshTagBindings();
        RefreshCurrentView();
        _statusLabel.Text = "ייבוא ה-CSV הושלם.";
    }

    private void BulkAddTag()
    {
        var selectedIds = GetSelectedCardIds();
        if (selectedIds.Length == 0)
        {
            _statusLabel.Text = "לא נבחרו יחידות לימוד.";
            return;
        }

        using var dialog = new TagSelectionForm("הוספת תגית ליחידות לימוד נבחרות", _database.GetTags());
        PrepareDialog(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedTagIds.Count == 0)
        {
            return;
        }

        _database.AddTagsToStudyItems(selectedIds, dialog.SelectedTagIds);
        RefreshTagBindings();
        RefreshCurrentView();
        _statusLabel.Text = $"נוספו תגיות ל-{selectedIds.Length} יחידות לימוד.";
    }

    private void BulkRemoveTag()
    {
        var selectedIds = GetSelectedCardIds();
        if (selectedIds.Length == 0)
        {
            _statusLabel.Text = "לא נבחרו יחידות לימוד.";
            return;
        }

        using var dialog = new TagSelectionForm("הסרת תגית מיחידות לימוד נבחרות", _database.GetTags());
        PrepareDialog(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedTagIds.Count == 0)
        {
            return;
        }

        _database.RemoveTagsFromStudyItems(selectedIds, dialog.SelectedTagIds);
        RefreshTagBindings();
        RefreshCurrentView();
        _statusLabel.Text = $"הוסרו תגיות מ-{selectedIds.Length} יחידות לימוד.";
    }

    private void BulkChangeDifficulty()
    {
        var selectedIds = GetSelectedCardIds();
        if (selectedIds.Length == 0)
        {
            _statusLabel.Text = "לא נבחרו יחידות לימוד.";
            return;
        }

        using var dialog = new DifficultySelectionForm();
        PrepareDialog(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _database.SetManualDifficultyForStudyItems(selectedIds, dialog.SelectedDifficulty);
        RefreshCurrentView();
        _statusLabel.Text = $"הקושי עודכן עבור {selectedIds.Length} יחידות לימוד.";
    }

    private void BulkDeleteCards()
    {
        var selectedIds = GetSelectedCardIds();
        if (selectedIds.Length == 0)
        {
            _statusLabel.Text = "לא נבחרו יחידות לימוד.";
            return;
        }

        var result = MessageBox.Show(
            this,
            $"למחוק {selectedIds.Length} יחידות לימוד נבחרות?",
            "מחיקה מרובה",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        if (result != DialogResult.Yes)
        {
            return;
        }

        _database.DeleteStudyItems(selectedIds);
        RefreshCurrentView();
        _statusLabel.Text = $"נמחקו {selectedIds.Length} יחידות לימוד.";
    }

    private void ReviewSelectedCards()
    {
        var selectedIds = GetSelectedCardIds();
        if (selectedIds.Length == 0)
        {
            _statusLabel.Text = "לא נבחרו יחידות לימוד.";
            return;
        }

        StartReviewSession(_database.GetStudyItemsByIds(selectedIds), $"סשן חזרה / נבחרו {selectedIds.Length} יחידות לימוד", _currentWorkspace, _reviewFilterView.SelectedOrderMode);
    }

    private int[] GetSelectedCardIds()
    {
        return _cardPanel.Controls
            .OfType<StudyCardControl>()
            .Where(card => card.IsSelected)
            .Select(card => card.ItemId)
            .ToArray();
    }

    private void SetAllCardSelections(bool isSelected)
    {
        foreach (var card in _cardPanel.Controls.OfType<StudyCardControl>())
        {
            card.IsSelected = isSelected;
        }

        _statusLabel.Text = isSelected ? "כל יחידות הלימוד בתצוגה נבחרו." : "בחירת יחידות הלימוד נוקתה.";
    }

    private void CreateTag()
    {
        using var dialog = new TagEditForm("תגית חדשה");
        PrepareDialog(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _database.AddTag(dialog.TagName);
        RefreshTagBindings();
        _statusLabel.Text = $"התגית נוספה: {dialog.TagName}";
    }

    private TagModel? CreateTagFromCardDialog(IWin32Window owner, string? suggestedName)
    {
        string tagName;
        if (!string.IsNullOrWhiteSpace(suggestedName))
        {
            tagName = suggestedName.Trim();
        }
        else
        {
            using var dialog = new TagEditForm("תגית חדשה");
            PrepareDialog(dialog);
            if (dialog.ShowDialog(owner) != DialogResult.OK)
            {
                return null;
            }

            tagName = dialog.TagName;
        }

        var tagId = _database.AddTag(tagName);
        RefreshTagBindings();
        var createdTag = _database.GetTags().FirstOrDefault(tag => tag.Id == tagId);
        if (createdTag is not null)
        {
            _statusLabel.Text = $"התגית נוספה: {createdTag.Name}";
        }

        return createdTag;
    }

    private void EditTag(TagModel tag)
    {
        using var dialog = new TagEditForm("עריכת תגית", tag.Name);
        PrepareDialog(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _database.UpdateTag(tag.Id, dialog.TagName);
        RefreshTagBindings();
        _statusLabel.Text = $"התגית עודכנה: {dialog.TagName}";
    }

    private void DeleteTag(TagModel tag)
    {
        var result = MessageBox.Show(
            this,
            $"למחוק את התגית '{tag.Name}'?",
            "מחיקת תגית",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

        if (result != DialogResult.Yes)
        {
            return;
        }

        _database.DeleteTag(tag.Id);
        RefreshTagBindings();
        _statusLabel.Text = $"התגית נמחקה: {tag.Name}";
    }

    private void FilterByTag(TagModel tag)
    {
        _currentWorkspace = MainWorkspaceMode.Search;
        ShowCardsWorkspace();
        _searchFilterView.Visible = true;
        _reviewFilterView.Visible = false;
        ActivateTab(_searchTabItem);
        _contentTitleLabel.Text = $"חיפוש / תגית {tag.Name}";

        var items = _database.SearchStudyItems(new StudySearchQuery
        {
            SearchText = string.Empty,
            SubjectId = _selectedSubjectId,
            TagId = tag.Id,
            LimitToSelectedNode = false
        });

        PopulateCards(items);
        _searchFilterView.SetResultSummary($"התגית '{tag.Name}' מחזירה {items.Count} יחידות לימוד");
        _statusLabel.Text = $"סינון לפי תגית: {tag.Name}";
    }

    private void BackupDatabase()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "SQLite database (*.db)|*.db",
            FileName = $"גיבוי-{DateTime.Today:yyyyMMdd}.db",
            Title = "גיבוי מסד נתונים"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _database.BackupDatabase(dialog.FileName);
        _statusLabel.Text = "הגיבוי נשמר בהצלחה";
    }

    private void OpenMaintenanceDialog()
    {
        using var dialog = new MaintenanceForm(_database);
        PrepareDialog(dialog);
        dialog.ShowDialog(this);
        ReloadTree();
        RefreshTagBindings();
        RefreshCurrentView();
        RefreshPausedSessionIndicator();
        _statusLabel.Text = "מסך התחזוקה נסגר.";
    }

    private void ExportCsv()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = "ייצוא-יחידות-לימוד.csv",
            Title = "ייצוא יחידות לימוד ל-CSV"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _database.ExportToCsv(dialog.FileName);
        _statusLabel.Text = "קובץ ה-CSV נשמר בהצלחה";
    }

    private void ExportPrintRange()
    {
        using var rangeForm = new PrintRangeForm();
        PrepareDialog(rangeForm);
        if (rangeForm.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "HTML files (*.html)|*.html",
            FileName = $"הדפסה-{rangeForm.StartDate:yyyyMMdd}-{rangeForm.EndDate:yyyyMMdd}.html",
            Title = "ייצוא קובץ HTML להדפסה"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _database.ExportPrintableHtml(rangeForm.StartDate, rangeForm.EndDate, dialog.FileName);
        _statusLabel.Text = "קובץ ההדפסה נשמר בהצלחה";
    }

    private void ShowHelp()
    {
        MessageBox.Show(
            this,
              "לשימוש מהיר:\n\n1. בחרו צומת לימוד מהעץ הימני.\n2. הוסיפו יחידת לימוד חדשה או פתחו חיפוש/תגיות.\n3. להפעלת חזרה מסוננת עברו ללשונית חזרה יומית.\n4. ניהול תגיות מתבצע בלשונית תגיות או מתוך כל יחידת לימוד.",
            "עזרה",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            this,
              "מערכת לימוד וחזרות מקומית ללימוד תורה עם מסד SQLite, אלגוריתם תזמון חזרות, תגיות, חיפוש, קישור מלא לעץ הספרייה ומעקב יחידות לימוד.",
            "אודות",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
    }

    private void SelectNodeBySubjectId(int subjectId)
    {
        TreeNodeCollection nodes = _subjectTreeView.Nodes;
        TreeNode? node = null;

        foreach (var subject in _database.GetSubjectLineage(subjectId))
        {
            node = FindNode(nodes, subject.Id);
            if (node is null)
            {
                return;
            }

            if (node.Nodes.Count == 1 && node.Nodes[0].Text == DummyNodeText)
            {
                if (!TryPopulateTreeChildren(node))
                {
                    return;
                }
            }

            nodes = node.Nodes;
        }

        if (node is not null)
        {
            _subjectTreeView.SelectedNode = node;
            node.EnsureVisible();
        }
    }

    private static TreeNode? FindNode(TreeNodeCollection nodes, int subjectId)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is int id && id == subjectId)
            {
                return node;
            }

            var child = FindNode(node.Nodes, subjectId);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static void PrepareDialog(Form dialog)
    {
        UiLayoutHelper.ApplyFormDefaults(dialog);
        dialog.RightToLeftLayout = false;
        ApplyRightToLeftRecursive(dialog);
        UiLayoutHelper.ApplyRecursive(dialog);
    }

    private static void ApplyRightToLeftRecursive(Control parent)
    {
        parent.RightToLeft = RightToLeft.Yes;
        ApplyMirroredRtlWhenAvailable(parent);
        foreach (Control child in parent.Controls)
        {
            ApplyRightToLeftRecursive(child);
        }
    }

    private static string TranslateRating(ReviewRating rating)
    {
        return rating switch
        {
            ReviewRating.Again => "גרוע",
            ReviewRating.Hard => "חלש",
            ReviewRating.Good => "בסדר",
            ReviewRating.Easy => "טוב",
            ReviewRating.Perfect => "מצוין",
            _ => string.Empty
        };
    }

    private static string TranslatePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? "כל הספרייה"
            : path.Replace(" > ", " / ", StringComparison.Ordinal);
    }

    private static ToolStripMenuItem CreateTopTab(ToolStripMenuItem item, string text, EventHandler onClick)
    {
        var font = new Font("Microsoft Sans Serif", 10.5F, FontStyle.Bold);
        item.Text = text;
        item.Font = font;
        item.AutoSize = false;
        item.Width = Math.Max(124, TextRenderer.MeasureText(text, font).Width + 40);
        item.Height = 38;
        item.Margin = new Padding(3, 0, 3, 0);
        item.Padding = new Padding(12, 4, 12, 4);
        item.BackColor = ClassicPalette.ToolbarBackground;
        item.ForeColor = Color.White;
        item.Click += onClick;
        return item;
    }

    private static ToolStripButton CreateFormatButton(string text, Font? font = null)
    {
        var resolvedFont = font ?? new Font("Microsoft Sans Serif", 9.5F, FontStyle.Bold);
        var button = new ToolStripButton
        {
            Text = text,
            AutoSize = false,
            Width = Math.Max(text.Length > 2 ? 72 : 48, TextRenderer.MeasureText(text, resolvedFont).Width + 24),
            Height = 32,
            Margin = new Padding(2, 0, 2, 0),
            BackColor = Color.FromArgb(216, 242, 239),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ForeColor = Color.Black
        };

        button.Font = resolvedFont;
        return button;
    }

    private static ToolStripComboBox CreateFormatCombo()
    {
        var combo = new ToolStripComboBox
        {
            Width = 70,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        combo.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular);
        combo.Items.AddRange(["8", "10", "12", "14", "16", "18"]);
        combo.SelectedIndex = 2;
        return combo;
    }

    private static ToolStripSeparator CreateToolStripSeparator()
    {
        return new ToolStripSeparator
        {
            Margin = new Padding(4, 0, 4, 0)
        };
    }

    private static Button CreateHeaderActionButton(string text, EventHandler onClick)
    {
        var font = new Font("Microsoft Sans Serif", 9.5F, FontStyle.Bold);
        var button = new Button
        {
            Text = text,
            Width = Math.Max(124, TextRenderer.MeasureText(text, font).Width + 34),
            Height = 42,
            Margin = new Padding(1, 1, 1, 1),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(208, 237, 234),
            Font = font
        };

        button.FlatAppearance.BorderColor = Color.FromArgb(45, 117, 110);
        UiLayoutHelper.StyleActionButton(button, 124, 42);
        button.Click += onClick;
        return button;
    }

    private static void ApplyMirroredRtlWhenAvailable(object target)
    {
        if (target is Control control)
        {
            control.RightToLeft = RightToLeft.Yes;
        }

        var property = target.GetType().GetProperty("RightToLeftLayout");
        if (property is not null && property.CanWrite && property.PropertyType == typeof(bool))
        {
            property.SetValue(target, true);
        }
    }

    private void ActivateTab(ToolStripMenuItem activeItem)
    {
        foreach (var item in new[] { _questionsTabItem, _tagsTabItem, _searchTabItem, _reviewTabItem, _statusTabItem })
        {
            item.BackColor = item == activeItem
                ? Color.FromArgb(72, 155, 147)
                : ClassicPalette.ToolbarBackground;
            item.ForeColor = Color.White;
        }
    }

    private void ShowTreeLoadError(Exception exception)
    {
        File.AppendAllText(
            Path.Combine(AppDatabase.GetDefaultLogsFolder(), "tree-load-errors.log"),
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");

        MessageBox.Show(
            this,
            $"לא ניתן לטעון כעת את נתוני הספרייה.\n\n{exception.Message}",
            "שגיאת טעינה",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
    }

    public async Task RunTanakhVerificationAsync(string overviewScreenshotPath, string detailScreenshotPath, string reportPath)
    {
        var expandedPaths = new List<string>();
        Exception? failure = null;
        var logPath = Path.ChangeExtension(reportPath, ".log");

        try
        {
            await Task.Delay(1200);
            var tanakhNode = FindNodeByText(_subjectTreeView.Nodes, "תנ\"ך")
                ?? throw new InvalidOperationException("צומת תנ\"ך לא נמצא בעץ.");
            await ExpandNodeAsync(tanakhNode, expandedPaths);

            var torahNode = FindChildNodeByText(tanakhNode, "תורה")
                ?? throw new InvalidOperationException("צומת תורה לא נמצא.");
            var prophetsNode = FindChildNodeByText(tanakhNode, "נביאים")
                ?? throw new InvalidOperationException("צומת נביאים לא נמצא.");
            var writingsNode = FindChildNodeByText(tanakhNode, "כתובים")
                ?? throw new InvalidOperationException("צומת כתובים לא נמצא.");

            await ExpandNodeAsync(torahNode, expandedPaths);
            await ExpandNodeAsync(prophetsNode, expandedPaths);
            await ExpandNodeAsync(writingsNode, expandedPaths);

            var torahBook = GetFirstRealChild(torahNode) ?? throw new InvalidOperationException("לא נמצא ספר בתורה.");
            await ExpandNodeAsync(torahBook, expandedPaths);
            var torahChapter = GetFirstRealChild(torahBook) ?? throw new InvalidOperationException("לא נמצא פרק בספר.");
            await ExpandNodeAsync(torahChapter, expandedPaths);
            var verseNode = GetFirstRealChild(torahChapter) ?? throw new InvalidOperationException("לא נמצא פסוק בפרק.");

            _subjectTreeView.SelectedNode = tanakhNode;
            tanakhNode.EnsureVisible();
            await Task.Delay(600);
            CaptureWindowScreenshot(overviewScreenshotPath);

            _subjectTreeView.SelectedNode = verseNode;
            verseNode.EnsureVisible();
            await Task.Delay(600);
            CaptureWindowScreenshot(detailScreenshotPath);
        }
        catch (Exception exception)
        {
            failure = exception;
            AppendVerificationLog(logPath, exception.ToString());
        }
        finally
        {
            SaveTanakhVerificationReport(reportPath, overviewScreenshotPath, detailScreenshotPath, expandedPaths, failure);
            await Task.Delay(400);
            BeginInvoke(new Action(Close));
        }
    }

    private async Task RunCoreVerificationAsync(CoreVerificationRequest request)
    {
        Exception? failure = null;
        var logPath = Path.ChangeExtension(request.ReportPath, ".log");
        var payload = new Dictionary<string, object?>();

        try
        {
            await Task.Delay(1200);

            var tanakhNode = FindNodeByText(_subjectTreeView.Nodes, "תנ\"ך")
                ?? throw new InvalidOperationException("צומת תנ\"ך לא נמצא בעץ.");
            await ExpandNodeAsync(tanakhNode, []);
            var torahNode = FindChildNodeByText(tanakhNode, "תורה")
                ?? throw new InvalidOperationException("צומת תורה לא נמצא.");
            await ExpandNodeAsync(torahNode, []);

            var bookNode = GetFirstRealChild(torahNode)
                ?? throw new InvalidOperationException("לא נמצא ספר בתורה.");
            await ExpandNodeAsync(bookNode, []);

            var chapterNode = GetFirstRealChild(bookNode)
                ?? throw new InvalidOperationException("לא נמצא פרק בספר.");
            await ExpandNodeAsync(chapterNode, []);

            var verseNode = GetFirstRealChild(chapterNode)
                ?? throw new InvalidOperationException("לא נמצא פסוק בפרק.");
            _subjectTreeView.SelectedNode = verseNode;
            HandleTreeSelection();

            var searchTagId = _database.AddTag("אאא-חיפוש");
            var hardTagId = _database.AddTag("אאא-קשה");
            RefreshTagBindings();

            var seedItems = EnsureCoreVerificationStudyItems((int)verseNode.Tag!, searchTagId, hardTagId);
            payload["SeededItemIds"] = seedItems;
            payload["SelectedPath"] = verseNode.FullPath;

            ShowStudyCards();
            await Task.Delay(500);
            CaptureWindowScreenshot(request.TreeScreenshotPath);

            ShowTagsView();
            RefreshTagBindings();
            _tagsManagerView.SelectTag(searchTagId);
            await Task.Delay(400);
            CaptureWindowScreenshot(request.TagsScreenshotPath);

            ShowSearchView();
            _searchFilterView.SetSearchState("אימותחיפוש", true, searchTagId);
            RunSearch();
            await Task.Delay(400);
            CaptureWindowScreenshot(request.SearchScreenshotPath);

            ShowReviewView();
            _reviewFilterView.SetFilterState(true, false, true, StudyDifficulty.Hard, hardTagId);
            RunReview();
            await Task.Delay(400);
            CaptureWindowScreenshot(request.ReviewScreenshotPath);

            payload["SearchTagId"] = searchTagId;
            payload["HardTagId"] = hardTagId;
            payload["SearchResults"] = _database.SearchStudyItems(new StudySearchQuery
            {
                SearchText = "אימותחיפוש",
                SubjectId = (int)verseNode.Tag!,
                TagId = searchTagId,
                LimitToSelectedNode = true
            }).Count;
            payload["ReviewResults"] = _database.GetStudyItemsForReview(new StudyReviewFilter
            {
                SubjectId = (int)verseNode.Tag!,
                TagId = hardTagId,
                RestrictToSelectedNode = true,
                DueOnly = false,
                FailedRecentlyOnly = true,
                Difficulty = StudyDifficulty.Hard
            }).Count;
        }
        catch (Exception exception)
        {
            failure = exception;
            AppendVerificationLog(logPath, exception.ToString());
        }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.ReportPath)!);
            payload["Success"] = failure is null;
            payload["Failure"] = failure?.ToString();
            payload["SearchScreenshot"] = request.SearchScreenshotPath;
            payload["TagsScreenshot"] = request.TagsScreenshotPath;
            payload["ReviewScreenshot"] = request.ReviewScreenshotPath;
            payload["TreeScreenshot"] = request.TreeScreenshotPath;

            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(request.ReportPath, json);
            await Task.Delay(400);
            BeginInvoke(new Action(Close));
        }
    }

    private async Task RunManagementVerificationAsync(ManagementVerificationRequest request)
    {
        Exception? failure = null;
        var payload = new Dictionary<string, object?>();

        try
        {
            await Task.Delay(1200);
            var tanakhNode = FindNodeByText(_subjectTreeView.Nodes, "תנ\"ך")
                ?? throw new InvalidOperationException("צומת תנ\"ך לא נמצא בעץ.");
            await ExpandNodeAsync(tanakhNode, []);
            var torahNode = FindChildNodeByText(tanakhNode, "תורה")
                ?? throw new InvalidOperationException("צומת תורה לא נמצא.");
            await ExpandNodeAsync(torahNode, []);
            var bookNode = GetFirstRealChild(torahNode)
                ?? throw new InvalidOperationException("לא נמצא ספר בתורה.");
            await ExpandNodeAsync(bookNode, []);
            var chapterNode = GetFirstRealChild(bookNode)
                ?? throw new InvalidOperationException("לא נמצא פרק.");
            await ExpandNodeAsync(chapterNode, []);
            var verseNode = GetFirstRealChild(chapterNode)
                ?? throw new InvalidOperationException("לא נמצא פסוק.");
            _subjectTreeView.SelectedNode = verseNode;
            HandleTreeSelection();

            var searchTagId = _database.AddTag("אאא-חיפוש");
            var hardTagId = _database.AddTag("אאא-קשה");
            RefreshTagBindings();
            var itemIds = EnsureCoreVerificationStudyItems((int)verseNode.Tag!, searchTagId, hardTagId);

            var sampleCsvPath = Path.Combine(Path.GetDirectoryName(request.ReportPath)!, "sample-import.csv");
            CsvUtility.WriteRows(sampleCsvPath,
            [
                ["נושא", "שאלה", "תשובה", "ספר", "פרק", "פסוק", "קושי", "תגיות"],
                ["ייבוא בדיקה", "שאלה מקובץ CSV", "תשובת ייבוא", bookNode.Text, chapterNode.Text, verseNode.Text, "בינונית", "אאא-חיפוש;ייבוא"]
            ]);

            using (var importForm = new CsvImportForm(_database, (int)verseNode.Tag!, TranslatePath(_database.GetSubjectPath((int)verseNode.Tag!))))
            {
                PrepareDialog(importForm);
                importForm.Show(this);
                importForm.AutomationLoadPreview(sampleCsvPath);
                await Task.Delay(500);
                CaptureControlScreenshot(importForm, request.ImportScreenshotPath);
                payload["ImportPreviewAccepted"] = importForm.LastPreview?.AcceptedCount;
                payload["ImportPreviewRejected"] = importForm.LastPreview?.RejectedCount;
                payload["ImportedCount"] = importForm.AutomationImport();
                await Task.Delay(300);
            }

            var existingItem = _database.GetStudyItem(itemIds[0]) ?? throw new InvalidOperationException("כרטיס לא נמצא.");
            using (var cardForm = new AddStudyItemForm(_database.GetAssignableSubjects(), _database.GetTags(), existingItem.SubjectId, existingItem))
            {
                PrepareDialog(cardForm);
                cardForm.Show(this);
                await Task.Delay(500);
                CaptureControlScreenshot(cardForm, request.CardDialogScreenshotPath);
                cardForm.Close();
            }

            ShowSearchView();
            _searchFilterView.SetAdvancedState("אימותמתקדם", bookNode.Text, chapterNode.Text.Replace("פרק ", string.Empty, StringComparison.Ordinal), verseNode.Text.Replace("פסוק ", string.Empty, StringComparison.Ordinal), hardTagId, StudyDifficulty.Hard, true, true);
            RunSearch();
            await Task.Delay(400);
            CaptureWindowScreenshot(request.AdvancedSearchScreenshotPath);
            payload["AdvancedSearchResults"] = _database.SearchStudyItems(new StudySearchQuery
            {
                SearchText = "אימותמתקדם",
                Book = bookNode.Text,
                Chapter = chapterNode.Text,
                Verse = verseNode.Text,
                TagId = hardTagId,
                Difficulty = StudyDifficulty.Hard,
                FailedRecentlyOnly = true,
                SubjectId = (int)verseNode.Tag!,
                LimitToSelectedNode = true
            }).Count;

            ShowStudyCards();
            SetAllCardSelections(true);
            await Task.Delay(300);
            CaptureWindowScreenshot(request.BulkScreenshotPath);
            payload["BulkSelectedCount"] = GetSelectedCardIds().Length;
            payload["TagsAfterRun"] = _database.GetTags().Count;
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.ReportPath)!);
            payload["Success"] = failure is null;
            payload["Failure"] = failure?.ToString();
            payload["ImportScreenshot"] = request.ImportScreenshotPath;
            payload["CardDialogScreenshot"] = request.CardDialogScreenshotPath;
            payload["AdvancedSearchScreenshot"] = request.AdvancedSearchScreenshotPath;
            payload["BulkScreenshot"] = request.BulkScreenshotPath;
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(request.ReportPath, json);
            await Task.Delay(400);
            BeginInvoke(new Action(Close));
        }
    }

    private async Task RunTagFlowVerificationAsync(TagFlowVerificationRequest request)
    {
        Exception? failure = null;
        var payload = new Dictionary<string, object?>();

        try
        {
            await Task.Delay(1200);
            var tanakhNode = FindNodeByText(_subjectTreeView.Nodes, "תנ\"ך")
                ?? throw new InvalidOperationException("צומת תנ\"ך לא נמצא בעץ.");
            await ExpandNodeAsync(tanakhNode, []);
            var torahNode = FindChildNodeByText(tanakhNode, "תורה")
                ?? throw new InvalidOperationException("צומת תורה לא נמצא.");
            await ExpandNodeAsync(torahNode, []);
            var bookNode = GetFirstRealChild(torahNode)
                ?? throw new InvalidOperationException("לא נמצא ספר בתורה.");
            await ExpandNodeAsync(bookNode, []);
            var chapterNode = GetFirstRealChild(bookNode)
                ?? throw new InvalidOperationException("לא נמצא פרק.");
            await ExpandNodeAsync(chapterNode, []);
            var verseNode = GetFirstRealChild(chapterNode)
                ?? throw new InvalidOperationException("לא נמצא פסוק.");

            _subjectTreeView.SelectedNode = verseNode;
            HandleTreeSelection();

            var tagName = $"אאא-תגית-דיאלוג-{DateTime.Now:HHmmss}";
            var subjectId = (int)verseNode.Tag!;

            using (var createDialog = new AddStudyItemForm(_database.GetAssignableSubjects(), _database.GetTags(), subjectId, null, _database.GetTags, CreateTagFromCardDialog))
            {
                PrepareDialog(createDialog);
                createDialog.Show(this);
                await Task.Delay(350);
                var createdTag = createDialog.AutomationCreateTag(tagName)
                    ?? throw new InvalidOperationException("לא ניתן היה ליצור תגית מתוך דיאלוג הכרטיס.");
                await Task.Delay(250);

                payload["CreatedTagId"] = createdTag.Id;
                payload["CreatedThroughDialog"] = true;
                payload["TagVisibleImmediately"] = createDialog.HasTagNamed(tagName);
                payload["TagCheckedImmediately"] = createDialog.IsTagChecked(createdTag.Id);
                createDialog.Close();
            }

            using (var reopenDialog = new AddStudyItemForm(_database.GetAssignableSubjects(), _database.GetTags(), subjectId, null, _database.GetTags, CreateTagFromCardDialog))
            {
                PrepareDialog(reopenDialog);
                reopenDialog.Show(this);
                await Task.Delay(350);

                var visibleOnReopen = reopenDialog.HasTagNamed(tagName);
                var checkedOnReopen = reopenDialog.AutomationSelectTag(tagName);
                await Task.Delay(220);
                CaptureControlScreenshot(reopenDialog, request.ScreenshotPath);

                payload["TagVisibleOnReopen"] = visibleOnReopen;
                payload["TagCheckedOnReopen"] = checkedOnReopen;
                payload["SelectedTagCount"] = reopenDialog.SelectedTagIds.Count;
                payload["HasVisibleTags"] = reopenDialog.HasVisibleTags;
                reopenDialog.Close();
            }

            RefreshTagBindings();
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.ReportPath)!);
            payload["Success"] = failure is null;
            payload["Failure"] = failure?.ToString();
            payload["Screenshot"] = request.ScreenshotPath;
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(request.ReportPath, json);
            await Task.Delay(400);
            BeginInvoke(new Action(Close));
        }
    }

    private async Task RunBottomActionsVerificationAsync(BottomActionsVerificationRequest request)
    {
        Exception? failure = null;
        var payload = new Dictionary<string, object?>();

        try
        {
            await Task.Delay(1200);
            var tanakhNode = FindNodeByText(_subjectTreeView.Nodes, "תנ\"ך")
                ?? throw new InvalidOperationException("צומת תנ\"ך לא נמצא בעץ.");
            await ExpandNodeAsync(tanakhNode, []);
            var torahNode = FindChildNodeByText(tanakhNode, "תורה")
                ?? throw new InvalidOperationException("צומת תורה לא נמצא.");
            await ExpandNodeAsync(torahNode, []);
            var bookNode = GetFirstRealChild(torahNode)
                ?? throw new InvalidOperationException("לא נמצא ספר בתורה.");
            await ExpandNodeAsync(bookNode, []);
            var chapterNode = GetFirstRealChild(bookNode)
                ?? throw new InvalidOperationException("לא נמצא פרק.");
            await ExpandNodeAsync(chapterNode, []);
            var verseNode = GetFirstRealChild(chapterNode)
                ?? throw new InvalidOperationException("לא נמצא פסוק.");

            _subjectTreeView.SelectedNode = verseNode;
            HandleTreeSelection();
            var verifyTagId = _database.AddTag("אאא-כפתורים");
            _ = EnsureCoreVerificationStudyItems((int)verseNode.Tag!, verifyTagId, verifyTagId);
            ShowStudyCards();
            HandleTreeSelection();
            await Task.Delay(350);

            var cards = _cardPanel.Controls.OfType<StudyCardControl>().ToList();
            if (cards.Count == 0)
            {
                throw new InvalidOperationException("לא נמצאו כרטיסים לצילום.");
            }

            CaptureControlScreenshot(cards[0], request.CardScreenshotPath);
            payload["CardButtonRowVisible"] = true;
            payload["CardWidth"] = cards[0].Width;
            payload["CardHeight"] = cards[0].Height;

            var itemIds = EnsureReviewVerificationStudyItems((int)verseNode.Tag!);
            var items = _database.GetStudyItemsByIds(itemIds);
            StartReviewSession(items, "אימות כפתורים תחתונים", MainWorkspaceMode.Review, ReviewSessionOrderMode.Random);
            await Task.Delay(350);
            _reviewSessionView.AutomationRevealAnswer();
            await Task.Delay(220);
            CaptureControlScreenshot(_reviewSessionView, request.ReviewScreenshotPath);

            payload["ReviewButtonRowVisible"] = true;
            payload["ReviewWidth"] = _reviewSessionView.Width;
            payload["ReviewHeight"] = _reviewSessionView.Height;
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.ReportPath)!);
            payload["Success"] = failure is null;
            payload["Failure"] = failure?.ToString();
            payload["CardScreenshot"] = request.CardScreenshotPath;
            payload["ReviewScreenshot"] = request.ReviewScreenshotPath;
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(request.ReportPath, json);
            await Task.Delay(400);
            BeginInvoke(new Action(Close));
        }
    }

    private async Task RunLibraryVerificationAsync(LibraryVerificationRequest request)
    {
        Exception? failure = null;
        var payload = new Dictionary<string, object?>();

        try
        {
            await Task.Delay(1200);

            var talmudNode = FindNodeByText(_subjectTreeView.Nodes, "תלמוד")
                ?? throw new InvalidOperationException("צומת תלמוד לא נמצא בעץ.");
            await ExpandNodeAsync(talmudNode, []);

            var bavliNode = FindChildNodeByText(talmudNode, "בבלי")
                ?? throw new InvalidOperationException("צומת בבלי לא נמצא.");
            await ExpandNodeAsync(bavliNode, []);
            var bavliSederNode = GetFirstRealChild(bavliNode)
                ?? throw new InvalidOperationException("לא נמצא סדר בבבלי.");
            await ExpandNodeAsync(bavliSederNode, []);
            var bavliTractateNode = GetFirstRealChild(bavliSederNode)
                ?? throw new InvalidOperationException("לא נמצאה מסכת בבבלי.");
            await ExpandNodeAsync(bavliTractateNode, []);
            var bavliChapterNode = GetFirstRealChild(bavliTractateNode)
                ?? throw new InvalidOperationException("לא נמצא פרק בבבלי.");
            await ExpandNodeAsync(bavliChapterNode, []);
            var bavliPageNode = GetFirstRealChild(bavliChapterNode)
                ?? throw new InvalidOperationException("לא נמצא דף בבבלי.");

            var yerushalmiNode = FindChildNodeByText(talmudNode, "ירושלמי")
                ?? throw new InvalidOperationException("צומת ירושלמי לא נמצא.");
            await ExpandNodeAsync(yerushalmiNode, []);
            var yerushalmiSederNode = GetFirstRealChild(yerushalmiNode)
                ?? throw new InvalidOperationException("לא נמצא סדר בירושלמי.");
            await ExpandNodeAsync(yerushalmiSederNode, []);
            var yerushalmiTractateNode = GetFirstRealChild(yerushalmiSederNode)
                ?? throw new InvalidOperationException("לא נמצאה מסכת בירושלמי.");
            await ExpandNodeAsync(yerushalmiTractateNode, []);
            var yerushalmiChapterNode = GetFirstRealChild(yerushalmiTractateNode)
                ?? throw new InvalidOperationException("לא נמצא פרק בירושלמי.");
            await ExpandNodeAsync(yerushalmiChapterNode, []);
            var yerushalmiHalakhahNode = GetFirstRealChild(yerushalmiChapterNode)
                ?? throw new InvalidOperationException("לא נמצאה הלכה בירושלמי.");

            var rambamNode = FindNodeByText(_subjectTreeView.Nodes, "רמב\"ם")
                ?? throw new InvalidOperationException("צומת רמב\"ם לא נמצא בעץ.");
            await ExpandNodeAsync(rambamNode, []);
            var rambamSeferNode = GetFirstRealChild(rambamNode)
                ?? throw new InvalidOperationException("לא נמצא ספר ברמב\"ם.");
            await ExpandNodeAsync(rambamSeferNode, []);
            var rambamHalakhotNode = GetFirstRealChild(rambamSeferNode)
                ?? throw new InvalidOperationException("לא נמצאו הלכות ברמב\"ם.");
            await ExpandNodeAsync(rambamHalakhotNode, []);
            var rambamChapterNode = GetFirstRealChild(rambamHalakhotNode)
                ?? throw new InvalidOperationException("לא נמצא פרק ברמב\"ם.");
            await ExpandNodeAsync(rambamChapterNode, []);
            var rambamUnitNode = GetFirstRealChild(rambamChapterNode)
                ?? throw new InvalidOperationException("לא נמצאה הלכה ברמב\"ם.");

            var turNode = FindNodeByText(_subjectTreeView.Nodes, "טור")
                ?? throw new InvalidOperationException("צומת טור לא נמצא בעץ.");
            await ExpandNodeAsync(turNode, []);
            var turSectionNode = GetFirstRealChild(turNode)
                ?? throw new InvalidOperationException("לא נמצא חלק בטור.");
            await ExpandNodeAsync(turSectionNode, []);
            var turSimanNode = GetFirstRealChild(turSectionNode)
                ?? throw new InvalidOperationException("לא נמצא סימן בטור.");

            var shulchanArukhNode = FindNodeByText(_subjectTreeView.Nodes, "שולחן ערוך")
                ?? throw new InvalidOperationException("צומת שולחן ערוך לא נמצא בעץ.");
            await ExpandNodeAsync(shulchanArukhNode, []);
            var shulchanArukhSectionNode = GetFirstRealChild(shulchanArukhNode)
                ?? throw new InvalidOperationException("לא נמצא חלק בשולחן ערוך.");
            await ExpandNodeAsync(shulchanArukhSectionNode, []);
            var shulchanArukhSimanNode = GetFirstRealChild(shulchanArukhSectionNode)
                ?? throw new InvalidOperationException("לא נמצא סימן בשולחן ערוך.");
            await ExpandNodeAsync(shulchanArukhSimanNode, []);
            var shulchanArukhSeifNode = GetFirstRealChild(shulchanArukhSimanNode)
                ?? throw new InvalidOperationException("לא נמצא סעיף בשולחן ערוך.");

            _subjectTreeView.SelectedNode = shulchanArukhSeifNode;
            HandleTreeSelection();

            var selectedSubjectId = (int)shulchanArukhSeifNode.Tag!;
            var items = _database.GetStudyItemsForSubject(selectedSubjectId);
            if (items.Count == 0)
            {
                _database.AddStudyItem(selectedSubjectId, "כרטיס אימות שולחן ערוך", "מה נלמד בסעיף זה?", "זהו כרטיס בדיקה המשויך לרמת סעיף בשולחן ערוך לצורך אימות עץ הספרייה.");
                items = _database.GetStudyItemsForSubject(selectedSubjectId);
            }

            PopulateCards(items);
            ShowStudyCards();
            await Task.Delay(500);
            CaptureWindowScreenshot(request.ScreenshotPath);

            var summary = _database.BuildLibraryVerificationSummary();
            payload["RootCount"] = summary.RootCount;
            payload["LoadedSubjectCount"] = summary.LoadedSubjectCount;
            payload["TanakhSectionCount"] = summary.TanakhSectionCount;
            payload["TanakhBookCount"] = summary.TanakhBookCount;
            payload["TanakhChapterCount"] = summary.TanakhChapterCount;
            payload["TanakhVerseCount"] = summary.TanakhVerseCount;
            payload["TalmudCollectionCount"] = summary.TalmudCollectionCount;
            payload["TalmudSederCount"] = summary.TalmudSederCount;
            payload["TalmudTractateCount"] = summary.TalmudTractateCount;
            payload["TalmudChapterCount"] = summary.TalmudChapterCount;
            payload["TalmudPageCount"] = summary.TalmudPageCount;
            payload["BavliSederCount"] = summary.BavliSederCount;
            payload["BavliTractateCount"] = summary.BavliTractateCount;
            payload["BavliChapterCount"] = summary.BavliChapterCount;
            payload["BavliPageCount"] = summary.BavliPageCount;
            payload["YerushalmiSederCount"] = summary.YerushalmiSederCount;
            payload["YerushalmiTractateCount"] = summary.YerushalmiTractateCount;
            payload["YerushalmiChapterCount"] = summary.YerushalmiChapterCount;
            payload["YerushalmiHalakhahCount"] = summary.YerushalmiHalakhahCount;
            payload["MishnahSederCount"] = summary.MishnahSederCount;
            payload["MishnahTractateCount"] = summary.MishnahTractateCount;
            payload["MishnahChapterCount"] = summary.MishnahChapterCount;
            payload["MishnahUnitCount"] = summary.MishnahUnitCount;
            payload["RambamSeferCount"] = summary.RambamSeferCount;
            payload["RambamHalakhotCount"] = summary.RambamHalakhotCount;
            payload["RambamChapterCount"] = summary.RambamChapterCount;
            payload["RambamUnitCount"] = summary.RambamUnitCount;
            payload["TurSectionCount"] = summary.TurSectionCount;
            payload["TurSimanCount"] = summary.TurSimanCount;
            payload["ShulchanArukhSectionCount"] = summary.ShulchanArukhSectionCount;
            payload["ShulchanArukhSimanCount"] = summary.ShulchanArukhSimanCount;
            payload["ShulchanArukhSeifCount"] = summary.ShulchanArukhSeifCount;
            payload["DuplicateSiblingGroupCount"] = summary.DuplicateSiblingGroupCount;
            payload["CardsOnSelectedNode"] = items.Count;
            payload["SelectedTreePath"] = BuildTreePath(shulchanArukhSeifNode);
            payload["SelectedBavliPath"] = BuildTreePath(bavliPageNode);
            payload["SelectedYerushalmiPath"] = BuildTreePath(yerushalmiHalakhahNode);
            payload["SelectedRambamPath"] = BuildTreePath(rambamUnitNode);
            payload["SelectedTurPath"] = BuildTreePath(turSimanNode);
            payload["SelectedShulchanArukhPath"] = BuildTreePath(shulchanArukhSeifNode);
            payload["Audit"] = new
            {
                summary.Audit.ExistingRootsBefore,
                summary.Audit.AddedBranches,
                summary.Audit.FixedIssues,
                summary.Audit.CompletedBranches,
                summary.Audit.RemainingLimitations,
                summary.Audit.RenamedNodes,
                summary.Audit.DuplicateGroupsResolved,
                summary.Audit.MergedSubjects,
                summary.Audit.ReassignedStudyItems,
                summary.Audit.ReassignedPresets
            };
            payload["Requests"] = _database.GetSefariaRequestLog()
                .Select(entry => new { entry.Endpoint, entry.Success, entry.Transport, entry.Message })
                .ToArray();
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.ReportPath)!);
            payload["Success"] = failure is null;
            payload["Failure"] = failure?.ToString();
            payload["Screenshot"] = request.ScreenshotPath;
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(request.ReportPath, json);
            await Task.Delay(400);
            BeginInvoke(new Action(Close));
        }
    }

    private async Task RunFreshStartVerificationAsync(FreshStartVerificationRequest request)
    {
        Exception? failure = null;
        var payload = new Dictionary<string, object?>();
        var reportDirectory = Path.GetDirectoryName(request.ReportPath)!;
        var resetReportPath = Path.Combine(reportDirectory, "fresh-start-reset-hebrew.md");

        try
        {
            await Task.Delay(1200);
            Directory.CreateDirectory(reportDirectory);

            var resetReport = _database.ResetUserData();
            File.WriteAllText(resetReportPath, _database.BuildUserDataResetMarkdownReport(resetReport));

            ReloadTree();
            RefreshTagBindings();
            RefreshCurrentView();
            RefreshPausedSessionIndicator();
            await Task.Delay(450);

            var tanakhNode = FindNodeByText(_subjectTreeView.Nodes, "תנ\"ך")
                ?? throw new InvalidOperationException("צומת תנ\"ך לא נמצא אחרי האיפוס.");
            await ExpandNodeAsync(tanakhNode, []);
            var torahNode = FindChildNodeByText(tanakhNode, "תורה")
                ?? throw new InvalidOperationException("צומת תורה לא נמצא אחרי האיפוס.");
            await ExpandNodeAsync(torahNode, []);
            var bookNode = GetFirstRealChild(torahNode)
                ?? throw new InvalidOperationException("לא נמצא ספר בתורה אחרי האיפוס.");
            await ExpandNodeAsync(bookNode, []);
            var chapterNode = GetFirstRealChild(bookNode)
                ?? throw new InvalidOperationException("לא נמצא פרק אחרי האיפוס.");
            await ExpandNodeAsync(chapterNode, []);
            var verseNode = GetFirstRealChild(chapterNode)
                ?? throw new InvalidOperationException("לא נמצא פסוק אחרי האיפוס.");

            _subjectTreeView.SelectedNode = verseNode;
            HandleTreeSelection();
            ShowStudyCards();
            await Task.Delay(350);
            CaptureWindowScreenshot(request.MainScreenshotPath);

            ShowStatusView();
            await Task.Delay(300);
            CaptureWindowScreenshot(request.DashboardScreenshotPath);

            var allItems = _database.GetStudyItemsForSubject(null);
            var allTags = _database.GetTags();
            var allPresets = _database.GetReviewPresets();
            var trackedUnits = _database.GetTrackedUnitCount(null);
            var dashboard = _database.GetDailyDashboardModel();
            var librarySummary = _database.BuildLibraryVerificationSummary();

            if (allItems.Count != 0 ||
                allTags.Count != 0 ||
                allPresets.Count != 0 ||
                trackedUnits != 0 ||
                _database.HasPausedReviewSession() ||
                resetReport.After.ReviewHistoryCount != 0 ||
                resetReport.After.StudyUnitReviewHistoryCount != 0)
            {
                throw new InvalidOperationException("האיפוס לא השאיר מצב נקי לגמרי של נתוני המשתמש.");
            }

            payload["Success"] = true;
            payload["DatabasePath"] = _database.DatabasePath;
            payload["ResetReportPath"] = resetReportPath;
            payload["SafetyBackupPath"] = resetReport.SafetyBackupPath;
            payload["PreservedRoots"] = resetReport.PreservedRoots;
            payload["PreservedSubjectCount"] = resetReport.PreservedSubjectCount;
            payload["LibraryRootCount"] = librarySummary.RootCount;
            payload["StudyItemCount"] = allItems.Count;
            payload["TagCount"] = allTags.Count;
            payload["PresetCount"] = allPresets.Count;
            payload["TrackedUnitCount"] = trackedUnits;
            payload["HasPausedSession"] = _database.HasPausedReviewSession();
            payload["ReviewHistoryCount"] = resetReport.After.ReviewHistoryCount;
            payload["StudyUnitHistoryCount"] = resetReport.After.StudyUnitReviewHistoryCount;
            payload["DashboardSummary"] = dashboard;
            payload["SelectedPath"] = BuildTreePath(verseNode);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            Directory.CreateDirectory(reportDirectory);
            if (failure is not null)
            {
                payload["Success"] = false;
                payload["Failure"] = failure.ToString();
            }

            payload["MainScreenshot"] = request.MainScreenshotPath;
            payload["DashboardScreenshot"] = request.DashboardScreenshotPath;
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(request.ReportPath, json);
            await Task.Delay(400);
            BeginInvoke(new Action(Close));
        }
    }

    private async Task RunStudyUnitVerificationAsync(StudyUnitVerificationRequest request)
    {
        Exception? failure = null;
        var payload = new Dictionary<string, object?>();

        try
        {
            var tanakhNode = FindNodeByText(_subjectTreeView.Nodes, "תנ\"ך")
                ?? throw new InvalidOperationException("צומת תנ\"ך לא נמצא בעץ.");
            await ExpandNodeAsync(tanakhNode, []);
            var torahNode = FindChildNodeByText(tanakhNode, "תורה")
                ?? throw new InvalidOperationException("צומת תורה לא נמצא.");
            await ExpandNodeAsync(torahNode, []);
            var bookNode = GetFirstRealChild(torahNode)
                ?? throw new InvalidOperationException("לא נמצא ספר תורה.");
            await ExpandNodeAsync(bookNode, []);
            var chapterNode = GetFirstRealChild(bookNode)
                ?? throw new InvalidOperationException("לא נמצא פרק.");
            await ExpandNodeAsync(chapterNode, []);
            var unitNode = GetFirstRealChild(chapterNode)
                ?? throw new InvalidOperationException("לא נמצאה יחידת לימוד.");

            _subjectTreeView.SelectedNode = unitNode;
            HandleTreeSelection();

            var searchTagId = _database.AddTag("אאא-יחידה");
            var hardTagId = _database.AddTag("אאא-מבחן יחידה");
            var itemIds = EnsureCoreVerificationStudyItems((int)unitNode.Tag!, searchTagId, hardTagId);
            ShowStudyCards();
            HandleTreeSelection();
            await Task.Delay(320);

            MarkSelectedUnitAsLearned();
            await Task.Delay(260);
            CaptureWindowScreenshot(request.MarkScreenshotPath);

            _database.SetStudyUnitDueDateForVerification((int)unitNode.Tag!, DateTime.Today.AddDays(-1), StudyUnitStatus.DueReview);
            ReloadTree();
            SelectNodeBySubjectId((int)unitNode.Tag!);
            ShowStatusView();
            await Task.Delay(280);
            CaptureWindowScreenshot(request.DashboardScreenshotPath);

            StartSelectedUnitReview();
            await Task.Delay(350);
            _reviewSessionView.AutomationRevealAnswer();
            await Task.Delay(180);
            CaptureControlScreenshot(_reviewSessionView, request.SessionScreenshotPath);

            var ratings = new[] { ReviewRating.Good, ReviewRating.Easy, ReviewRating.Perfect };
            var ratingIndex = 0;
            var safetyCounter = 0;
            while (!_reviewSessionView.IsSummaryVisible && safetyCounter < 24)
            {
                _reviewSessionView.AutomationRate(ratings[ratingIndex % ratings.Length]);
                await Task.Delay(220);
                if (!_reviewSessionView.IsSummaryVisible)
                {
                    _reviewSessionView.AutomationRevealAnswer();
                    await Task.Delay(120);
                }
                ratingIndex++;
                safetyCounter++;
            }

            await Task.Delay(260);
            CaptureControlScreenshot(_reviewSessionView, request.SummaryScreenshotPath);

            var progress = _database.GetStudyUnitProgress((int)unitNode.Tag!)
                ?? throw new InvalidOperationException("לא נמצאה התקדמות יחידה אחרי האימות.");
            payload["SubjectPath"] = _database.GetSubjectPath((int)unitNode.Tag!);
            payload["StudyCount"] = progress.StudyCount;
            payload["CompletedReviewCount"] = progress.CompletedReviewCount;
            payload["CurrentStage"] = progress.CurrentStage;
            payload["Status"] = progress.Status.ToString();
            payload["StatusText"] = AppDatabase.TranslateStudyUnitStatus(progress);
            payload["NextReviewDate"] = progress.NextReviewDate?.ToString("yyyy-MM-dd");
            payload["LastResult"] = progress.LastResult?.ToString();
            payload["LastResultText"] = AppDatabase.TranslateStudyUnitResult(progress.LastResult);
            payload["LinkedCardCount"] = progress.LinkedCardCount;
            payload["Success"] = true;
        }
        catch (Exception exception)
        {
            failure = exception;
            payload["Success"] = false;
            payload["Failure"] = exception.ToString();
        }
        finally
        {
            payload["MarkScreenshot"] = request.MarkScreenshotPath;
            payload["DashboardScreenshot"] = request.DashboardScreenshotPath;
            payload["SessionScreenshot"] = request.SessionScreenshotPath;
            payload["SummaryScreenshot"] = request.SummaryScreenshotPath;
            File.WriteAllText(
                request.ReportPath,
                System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            if (failure is null)
            {
                Close();
            }
        }
    }

    private async Task RunMaintenanceVerificationAsync(MaintenanceVerificationRequest request)
    {
        Exception? failure = null;
        var payload = new Dictionary<string, object?>();

        try
        {
            await Task.Delay(1200);
            using var dialog = new MaintenanceForm(_database);
            PrepareDialog(dialog);
            dialog.Show(this);
            await Task.Delay(500);
            CaptureControlScreenshot(dialog, request.ScreenshotPath);

            var buttons = GetDescendantControls(dialog)
                .OfType<Button>()
                .Where(button => button.Visible)
                .Select(button => new
                {
                    button.Text,
                    button.Bounds,
                    FullyVisible = button.Right <= dialog.ClientSize.Width && button.Bottom <= dialog.ClientSize.Height
                })
                .ToArray();

            payload["Success"] = true;
            payload["ButtonCount"] = buttons.Length;
            payload["Buttons"] = buttons;
            payload["AllButtonsFullyVisible"] = buttons.All(button => button.FullyVisible);
            dialog.Close();
        }
        catch (Exception exception)
        {
            failure = exception;
            payload["Success"] = false;
            payload["Failure"] = exception.ToString();
        }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.ReportPath)!);
            payload["Screenshot"] = request.ScreenshotPath;
            File.WriteAllText(
                request.ReportPath,
                System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            await Task.Delay(250);
            BeginInvoke(new Action(Close));
        }
    }

    private async Task RunProductVerificationAsync(ProductVerificationRequest request)
    {
        Exception? failure = null;
        var payload = new Dictionary<string, object?>();
        var performanceSamples = new List<PerformanceSample>();
        var reportDirectory = Path.GetDirectoryName(request.ReportPath)!;
        var validationReportPath = Path.Combine(reportDirectory, "product-validation-hebrew.md");
        var backupRestoreReportPath = Path.Combine(reportDirectory, "product-backup-restore-hebrew.md");
        var performanceReportPath = Path.Combine(reportDirectory, "product-performance-hebrew.md");
        var uiAuditReportPath = Path.Combine(reportDirectory, "ui-audit-hebrew.md");
        var exportCsvPath = Path.Combine(reportDirectory, "product-export.csv");
        var importCsvPath = Path.Combine(reportDirectory, "product-import.csv");
        var backupPath = Path.Combine(reportDirectory, $"product-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        try
        {
            await Task.Delay(1200);
            Directory.CreateDirectory(reportDirectory);

            performanceSamples.Add(new PerformanceSample
            {
                Name = "startup",
                Milliseconds = Stopwatch.GetElapsedTime(_applicationStartTickCount).TotalMilliseconds,
                Notes = "מרגע כניסה ל-Program.Main ועד תחילת verify מוצרי."
            });

            var treeStopwatch = Stopwatch.StartNew();
            var tanakhNode = FindNodeByText(_subjectTreeView.Nodes, "תנ\"ך")
                ?? throw new InvalidOperationException("צומת תנ\"ך לא נמצא בעץ.");
            await ExpandNodeAsync(tanakhNode, []);
            var torahNode = FindChildNodeByText(tanakhNode, "תורה")
                ?? throw new InvalidOperationException("צומת תורה לא נמצא.");
            await ExpandNodeAsync(torahNode, []);
            var bookNode = GetFirstRealChild(torahNode)
                ?? throw new InvalidOperationException("לא נמצא ספר בתורה.");
            await ExpandNodeAsync(bookNode, []);
            var chapterNode = GetFirstRealChild(bookNode)
                ?? throw new InvalidOperationException("לא נמצא פרק.");
            await ExpandNodeAsync(chapterNode, []);
            var verseNode = GetFirstRealChild(chapterNode)
                ?? throw new InvalidOperationException("לא נמצא פסוק.");
            treeStopwatch.Stop();
            performanceSamples.Add(new PerformanceSample
            {
                Name = "tree-open",
                Milliseconds = treeStopwatch.Elapsed.TotalMilliseconds,
                Notes = $"נפתח הנתיב {BuildTreePath(verseNode)}"
            });

            _subjectTreeView.SelectedNode = verseNode;
            HandleTreeSelection();

            var selectedSubjectId = (int)verseNode.Tag!;
            var tempTopic = $"כרטיס מוצר {DateTime.Now:HHmmss}";
            var createdItemId = _database.AddStudyItem(selectedSubjectId, tempTopic, "שאלת אימות מוצר", "תשובת אימות מוצר", StudyDifficulty.Medium);
            _database.UpdateStudyItem(createdItemId, selectedSubjectId, tempTopic + " ערוך", "שאלת אימות מוצר ערוכה", "תשובת אימות מוצר ערוכה", StudyDifficulty.Hard);
            var createdItem = _database.GetStudyItem(createdItemId) ?? throw new InvalidOperationException("לא ניתן היה לטעון את הכרטיס שנוצר.");

            var tempTagId = _database.AddTag("תגית-אימות-מוצר");
            _database.SetTagsForStudyItem(createdItemId, [tempTagId]);

            var searchStopwatch = Stopwatch.StartNew();
            var searchResults = _database.SearchStudyItems(new StudySearchQuery
            {
                SearchText = "אימות מוצר",
                SubjectId = selectedSubjectId,
                TagId = tempTagId,
                LimitToSelectedNode = true,
                Difficulty = StudyDifficulty.Hard
            });
            searchStopwatch.Stop();
            performanceSamples.Add(new PerformanceSample
            {
                Name = "search",
                Milliseconds = searchStopwatch.Elapsed.TotalMilliseconds,
                Notes = $"תוצאות: {searchResults.Count}"
            });

            var reviewItems = _database.GetStudyItemsByIds([createdItemId]);
            var reviewStopwatch = Stopwatch.StartNew();
            StartReviewSession(reviewItems, "אימות מוצר / סשן קצר", MainWorkspaceMode.Review, ReviewSessionOrderMode.Default);
            await Task.Delay(250);
            _reviewSessionView.AutomationRevealAnswer();
            await Task.Delay(120);
            _reviewSessionView.AutomationPause();
            await Task.Delay(180);
            ShowReviewView();
            await Task.Delay(140);
            ResumePausedReviewSession();
            await Task.Delay(180);
            _reviewSessionView.AutomationRevealAnswer();
            await Task.Delay(120);
            _reviewSessionView.AutomationRate(ReviewRating.Good);
            await Task.Delay(180);
            reviewStopwatch.Stop();
            performanceSamples.Add(new PerformanceSample
            {
                Name = "review-session",
                Milliseconds = reviewStopwatch.Elapsed.TotalMilliseconds,
                Notes = "יצירת סשן, pause/resume, ודירוג בפועל."
            });

            _database.ExportToCsv(exportCsvPath);

            File.WriteAllText(
                importCsvPath,
                "נושא,שאלה,תשובה,תגיות" + Environment.NewLine +
                $"ייבוא מוצר,שאלת ייבוא,תשובת ייבוא,תגית-אימות-מוצר{Environment.NewLine}");
            var importedCount = _database.ImportCsvWithMapping(importCsvPath, new CsvImportMapping
            {
                FallbackSubjectId = selectedSubjectId,
                HasHeaderRow = true,
                ColumnMappings = new Dictionary<int, CsvFieldType>
                {
                    [0] = CsvFieldType.Topic,
                    [1] = CsvFieldType.Question,
                    [2] = CsvFieldType.Answer,
                    [3] = CsvFieldType.Tags
                }
            });

            var validationReport = _database.ValidateAndRepairDatabase(autoRepair: true);
            File.WriteAllText(validationReportPath, _database.BuildValidationMarkdownReport(validationReport));

            _database.BackupDatabase(backupPath);
            var restoreProbeTopic = $"שחזור זמני {DateTime.Now:HHmmss}";
            var restoreProbeId = _database.AddStudyItem(selectedSubjectId, restoreProbeTopic, "כרטיס זמני לבדיקה", "יימחק אחרי שחזור");
            var backupRestoreReport = _database.RestoreDatabase(backupPath);
            File.WriteAllText(backupRestoreReportPath, _database.BuildBackupRestoreMarkdownReport(backupRestoreReport));
            ReloadTree();
            RefreshTagBindings();
            var restoredProbe = _database.SearchStudyItems(new StudySearchQuery { SearchText = restoreProbeTopic });

            ShowStatusView();
            await Task.Delay(300);
            CaptureWindowScreenshot(request.ScreenshotPath);

            var uiIssues = new[]
            {
                "כפתורי dashboard היו בגובה 28 ורוחב 150, ולכן כיתובים כמו 'התחל חזרה יומית' נחתכו.",
                "כפתורי מסננים וחיפוש השתמשו בשורות 34 פיקסל עם RTL עברי, מה שגרם לחיתוך אנכי.",
                "DataGridView-ים לא הגדירו header/row height מספקים לעברית ול-DPI.",
                "כרטיסי review ו-dialogs השתמשו בכפתורים קשיחים קטנים מדי וללא padding אחיד."
            };
            File.WriteAllText(
                uiAuditReportPath,
                """
                # דוח audit רוחבי ל-UI

                ## מקומות שבהם נמצאו בעיות
                - DailyDashboardControl: כפתורי תורים חכמים, כותרות summary, טבלת weak spots.
                - ReviewFilterControl: כפתורי `התחל חזרה`, `המשך סשן`, שורות מסננים.
                - SearchFilterControl: כפתורי `חפש`, `נקה`, ותיבות טקסט עם גובה קטן מדי.
                - ReviewSessionControl: כפתורי reveal/rating/pause/skip/review later וסיכום סשן.
                - StudyCardControl: כפתורי דירוג וניהול, וגובה כללי של הכרטיס.
                - TagsManagerControl / ReviewPresetForm / TagEditForm / AddStudyItemForm / CsvImportForm: כפתורים, labels וטבלאות preview.
                - MainForm: top menu, action bar, bulk action bar, toolbar buttons.

                ## הגורם המרכזי
                רוב הבעיות נבעו משילוב של גדלים קשיחים קטנים מדי, חוסר padding אנכי, ו-controls שלא הותאמו באופן רוחבי לפונט עברי, RTL ו-DPI.

                ## מה תוקן
                - נוספה שכבת `UiLayoutHelper` שמחילה metrics אחידים על כפתורים, טבלאות, labels, group boxes, tree views ו-toolstrips.
                - הוגדלו גבהי controls, row heights ו-header heights.
                - כפתורי פעולה מקבלים רוחב מינימלי מחושב לפי הטקסט בפועל.
                - נוספו padding ויישור טקסט אחידים ל-RTL.
                """ + Environment.NewLine + string.Join(Environment.NewLine, uiIssues.Select(issue => "- " + issue)));

            File.WriteAllText(
                performanceReportPath,
                "# דוח ביצועים" + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, performanceSamples.Select(sample =>
                    $"- {sample.Name}: {sample.Milliseconds:0.##}ms | {sample.Notes}")));

            payload["Success"] = true;
            payload["SchemaVersion"] = _database.GetSchemaVersion();
            payload["Migration"] = _database.GetLastMigrationSummary();
            payload["ValidationReportPath"] = validationReportPath;
            payload["BackupRestoreReportPath"] = backupRestoreReportPath;
            payload["PerformanceReportPath"] = performanceReportPath;
            payload["UiAuditReportPath"] = uiAuditReportPath;
            payload["StartupMs"] = performanceSamples.First(sample => sample.Name == "startup").Milliseconds;
            payload["TreeOpenMs"] = performanceSamples.First(sample => sample.Name == "tree-open").Milliseconds;
            payload["SearchMs"] = performanceSamples.First(sample => sample.Name == "search").Milliseconds;
            payload["ReviewSessionMs"] = performanceSamples.First(sample => sample.Name == "review-session").Milliseconds;
            payload["CreatedCardId"] = createdItemId;
            payload["SearchResults"] = searchResults.Count;
            payload["ImportedCount"] = importedCount;
            payload["PauseResumeWorked"] = !_database.HasPausedReviewSession();
            payload["BackupVerified"] = backupRestoreReport.BackupVerified;
            payload["RestoreVerified"] = backupRestoreReport.RestoreVerified;
            payload["RestoreProbeStillExists"] = restoredProbe.Any();
            payload["DashboardSummary"] = _database.GetDailyDashboardModel();
            payload["LibrarySummary"] = _database.BuildLibraryVerificationSummary();
            payload["Requests"] = _database.GetSefariaRequestLog()
                .TakeLast(30)
                .Select(entry => new { entry.Endpoint, entry.Success, entry.Transport, entry.Message })
                .ToArray();
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            Directory.CreateDirectory(reportDirectory);
            if (failure is not null)
            {
                payload["Success"] = false;
                payload["Failure"] = failure.ToString();
            }

            payload["Screenshot"] = request.ScreenshotPath;
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(request.ReportPath, json);
            await Task.Delay(400);
            BeginInvoke(new Action(Close));
        }
    }

    private async Task RunDailyWorkflowVerificationAsync(DailyWorkflowVerificationRequest request)
    {
        Exception? failure = null;
        var payload = new Dictionary<string, object?>();

        try
        {
            await Task.Delay(1200);
            var tanakhNode = FindNodeByText(_subjectTreeView.Nodes, "תנ\"ך")
                ?? throw new InvalidOperationException("צומת תנ\"ך לא נמצא בעץ.");
            await ExpandNodeAsync(tanakhNode, []);
            var torahNode = FindChildNodeByText(tanakhNode, "תורה")
                ?? throw new InvalidOperationException("צומת תורה לא נמצא.");
            await ExpandNodeAsync(torahNode, []);
            var bookNode = GetFirstRealChild(torahNode)
                ?? throw new InvalidOperationException("לא נמצא ספר בתורה.");
            await ExpandNodeAsync(bookNode, []);
            var chapterNode = GetFirstRealChild(bookNode)
                ?? throw new InvalidOperationException("לא נמצא פרק.");
            await ExpandNodeAsync(chapterNode, []);
            var verseNode = GetFirstRealChild(chapterNode)
                ?? throw new InvalidOperationException("לא נמצא פסוק.");

            _subjectTreeView.SelectedNode = verseNode;
            HandleTreeSelection();

            var searchTagId = _database.AddTag("אאא-פסוקים חשובים");
            var hardTagId = _database.AddTag("אאא-הלכה קשה");
            RefreshTagBindings();
            var itemIds = EnsureCoreVerificationStudyItems((int)verseNode.Tag!, searchTagId, hardTagId);
            _database.SetReviewLaterFlag(itemIds[0], true);

            if (!_database.GetReviewPresets().Any(preset => preset.Name == "בראשית"))
            {
                _database.AddReviewPreset("בראשית", (int)verseNode.Tag!, true, null, true, false, StudyDifficulty.Any, ReviewSessionOrderMode.Default);
            }

            if (!_database.GetReviewPresets().Any(preset => preset.Name == "פסוקים חשובים"))
            {
                _database.AddReviewPreset("פסוקים חשובים", null, false, searchTagId, true, false, StudyDifficulty.Any, ReviewSessionOrderMode.Random);
            }

            ShowStatusView();
            await Task.Delay(400);
            CaptureWindowScreenshot(request.DashboardScreenshotPath);

            _statusView.AutomationSelectFirstPreset();
            await Task.Delay(300);
            CaptureWindowScreenshot(request.QueuesScreenshotPath);

            _statusView.AutomationSelectFirstWeakSpot();
            await Task.Delay(300);
            CaptureWindowScreenshot(request.WeakSpotsScreenshotPath);

            var dashboard = _database.GetDailyDashboardModel();
            payload["DueToday"] = dashboard.DueTodayCount;
            payload["Overdue"] = dashboard.OverdueCount;
            payload["NewCount"] = dashboard.NewCount;
            payload["FailedRecently"] = dashboard.FailedRecentlyCount;
            payload["ReviewLaterCount"] = dashboard.ReviewLaterCount;
            payload["HasPausedSession"] = dashboard.HasPausedSession;
            payload["PresetCount"] = _database.GetReviewPresets().Count;
            payload["WeakSpotCount"] = _statusView.WeakSpotCount;
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.ReportPath)!);
            payload["Success"] = failure is null;
            payload["Failure"] = failure?.ToString();
            payload["DashboardScreenshot"] = request.DashboardScreenshotPath;
            payload["QueuesScreenshot"] = request.QueuesScreenshotPath;
            payload["WeakSpotsScreenshot"] = request.WeakSpotsScreenshotPath;
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(request.ReportPath, json);
            await Task.Delay(400);
            BeginInvoke(new Action(Close));
        }
    }

    private async Task RunReviewVerificationAsync(ReviewVerificationRequest request)
    {
        Exception? failure = null;
        var payload = new Dictionary<string, object?>();

        try
        {
            await Task.Delay(1200);
            var tanakhNode = FindNodeByText(_subjectTreeView.Nodes, "תנ\"ך")
                ?? throw new InvalidOperationException("צומת תנ\"ך לא נמצא בעץ.");
            await ExpandNodeAsync(tanakhNode, []);
            var torahNode = FindChildNodeByText(tanakhNode, "תורה")
                ?? throw new InvalidOperationException("צומת תורה לא נמצא.");
            await ExpandNodeAsync(torahNode, []);
            var bookNode = GetFirstRealChild(torahNode)
                ?? throw new InvalidOperationException("לא נמצא ספר בתורה.");
            await ExpandNodeAsync(bookNode, []);
            var chapterNode = GetFirstRealChild(bookNode)
                ?? throw new InvalidOperationException("לא נמצא פרק.");
            await ExpandNodeAsync(chapterNode, []);
            var verseNode = GetFirstRealChild(chapterNode)
                ?? throw new InvalidOperationException("לא נמצא פסוק.");
            _subjectTreeView.SelectedNode = verseNode;
            HandleTreeSelection();

            var itemIds = EnsureReviewVerificationStudyItems((int)verseNode.Tag!);
            var items = _database.GetStudyItemsByIds(itemIds);

            StartReviewSession(items, "אימות סשן חזרה", MainWorkspaceMode.Review, ReviewSessionOrderMode.Random);
            await Task.Delay(450);
            _reviewSessionView.AutomationRevealAnswer();
            await Task.Delay(220);
            CaptureWindowScreenshot(request.SessionScreenshotPath);

            _reviewSessionView.AutomationSkip();
            await Task.Delay(220);
            _reviewSessionView.AutomationReviewLater();
            await Task.Delay(220);
            _reviewSessionView.AutomationPause();
            await Task.Delay(320);

            ShowReviewView();
            await Task.Delay(220);
            ResumePausedReviewSession();
            await Task.Delay(320);
            CaptureWindowScreenshot(request.ResumeScreenshotPath);

            payload["ItemIds"] = itemIds;
            payload["TotalItems"] = items.Count;
            payload["OrderMode"] = _reviewSessionOrderMode.ToString();
            payload["PausedSessionSaved"] = _database.HasPausedReviewSession() == false;
            payload["RemainingAfterResume"] = _reviewSessionItems.Count;
            payload["SkippedCount"] = _reviewSessionSkippedItemIds.Count;
            payload["ReviewLaterCount"] = _reviewSessionReviewLaterItemIds.Count;
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.ReportPath)!);
            payload["Success"] = failure is null;
            payload["Failure"] = failure?.ToString();
            payload["SessionScreenshot"] = request.SessionScreenshotPath;
            payload["ResumeScreenshot"] = request.ResumeScreenshotPath;
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(request.ReportPath, json);
            await Task.Delay(400);
            BeginInvoke(new Action(Close));
        }
    }

    private int[] EnsureReviewVerificationStudyItems(int subjectId)
    {
        var reviewTagId = _database.AddTag("אאא-סשן");
        var items = _database.GetStudyItemsForSubject(subjectId)
            .Where(item => item.Topic.StartsWith("כרטיס סשן", StringComparison.Ordinal))
            .ToList();

        while (items.Count < 4)
        {
            var nextNumber = items.Count + 1;
            _database.AddStudyItem(
                subjectId,
                $"כרטיס סשן {nextNumber}",
                $"שאלת אימות {nextNumber} על הפסוק הנבחר",
                $"תשובת אימות {nextNumber} לצורך סשן חזרה",
                nextNumber <= 2 ? StudyDifficulty.Hard : StudyDifficulty.Medium);
            items = _database.GetStudyItemsForSubject(subjectId)
                .Where(item => item.Topic.StartsWith("כרטיס סשן", StringComparison.Ordinal))
                .ToList();
        }

        foreach (var item in items.Take(4))
        {
            _database.SetTagsForStudyItem(item.Id, [reviewTagId]);
        }

        return items
            .OrderBy(item => item.Topic, StringComparer.Ordinal)
            .Take(4)
            .Select(item => item.Id)
            .ToArray();
    }

    private int[] EnsureCoreVerificationStudyItems(int subjectId, int searchTagId, int hardTagId)
    {
        var items = _database.GetStudyItemsForSubject(subjectId).ToList();
        if (items.Count < 3)
        {
            _database.AddStudyItem(subjectId, "כרטיס אימות 1", "אימותחיפוש שאלה על הפסוק הנבחר", "תשובת בדיקה ראשונה");
            _database.AddStudyItem(subjectId, "כרטיס אימות 2", "כרטיס מסומן כקשה לבדיקה", "תשובת בדיקה שנייה");
            _database.AddStudyItem(subjectId, "כרטיס אימות 3", "כרטיס נוסף לצומת הספרייה", "תשובת בדיקה שלישית");
            items = _database.GetStudyItemsForSubject(subjectId).ToList();
        }

        var item1 = items.First();
        var item2 = items.Skip(1).FirstOrDefault() ?? item1;
        var item3 = items.Skip(2).FirstOrDefault() ?? item2;

        _database.UpdateStudyItem(item2.Id, item2.SubjectId, item2.Topic, "אימותמתקדם קשה לחיפוש מתקדם", item2.Answer, StudyDifficulty.Hard);
        _database.SetTagsForStudyItem(item1.Id, [searchTagId]);
        _database.SetTagsForStudyItem(item2.Id, [hardTagId]);
        _database.SetTagsForStudyItem(item3.Id, [searchTagId, hardTagId]);

        _database.RateStudyItem(item2.Id, ReviewRating.Again);
        _database.RateStudyItem(item2.Id, ReviewRating.Again);

        return [item1.Id, item2.Id, item3.Id];
    }

    private async Task ExpandNodeAsync(TreeNode node, ICollection<string> expandedPaths)
    {
        _subjectTreeView.SelectedNode = node;
        node.EnsureVisible();
        node.Expand();
        await Task.Delay(500);

        if (node.Nodes.Count == 1 && node.Nodes[0].Text == DummyNodeText)
        {
            if (!TryPopulateTreeChildren(node))
            {
                throw new InvalidOperationException($"לא ניתן היה להרחיב את '{node.Text}'.");
            }
        }

        expandedPaths.Add(BuildTreePath(node));
    }

    private void SaveTanakhVerificationReport(string reportPath, string overviewScreenshotPath, string detailScreenshotPath, IReadOnlyCollection<string> expandedPaths, Exception? failure)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        var requests = _database.GetSefariaRequestLog()
            .Select(entry => new { entry.Endpoint, entry.Success, entry.Transport, entry.Message })
            .ToArray();

        var payload = new
        {
            Success = failure is null,
            FailureMessage = failure?.Message,
            OverviewScreenshot = overviewScreenshotPath,
            DetailScreenshot = detailScreenshotPath,
            ExpandedPaths = expandedPaths.ToArray(),
            FetchWorked = requests.Any(entry => entry.Success && entry.Transport is "httpclient" or "node"),
            Requests = requests
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(reportPath, json);
    }

    private void CaptureWindowScreenshot(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        TopMost = true;
        BringToFront();
        Activate();
        Refresh();
        Application.DoEvents();

        using var bitmap = new Bitmap(Width, Height);
        DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        TopMost = false;
    }

    private static void CaptureControlScreenshot(Control control, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        control.Refresh();
        Application.DoEvents();
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void CaptureControlScreenScreenshot(Control control, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        control.Refresh();
        Application.DoEvents();
        using var bitmap = new Bitmap(control.Width, control.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(control.PointToScreen(Point.Empty), Point.Empty, control.Size);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static TreeNode? FindNodeByText(TreeNodeCollection nodes, string text)
    {
        foreach (TreeNode node in nodes)
        {
            if (string.Equals(node.Text, text, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
    }

    private static TreeNode? FindChildNodeByText(TreeNode parentNode, string text)
    {
        foreach (TreeNode child in parentNode.Nodes)
        {
            if (string.Equals(child.Text, text, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static TreeNode? GetFirstRealChild(TreeNode parentNode)
    {
        foreach (TreeNode child in parentNode.Nodes)
        {
            if (!string.Equals(child.Text, DummyNodeText, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static string BuildTreePath(TreeNode node)
    {
        var segments = new Stack<string>();
        TreeNode? current = node;
        while (current is not null)
        {
            segments.Push(current.Text);
            current = current.Parent;
        }

        return string.Join(" / ", segments);
    }

    private static void AppendVerificationLog(string logPath, string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static void AppendStartupLog(string message)
    {
        var logPath = Path.Combine(AppDatabase.GetDefaultLogsFolder(), "startup-trace.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static IEnumerable<Control> GetDescendantControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in GetDescendantControls(child))
            {
                yield return descendant;
            }
        }
    }
}

internal enum MainWorkspaceMode
{
    Questions,
    Tags,
    Search,
    Review,
    ReviewSession,
    Status
}




