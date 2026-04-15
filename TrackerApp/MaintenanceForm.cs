namespace TrackerApp;

public sealed class MaintenanceForm : Form
{
    private readonly AppDatabase _database;
    private readonly Label _schemaVersionLabel = new();
    private readonly Label _databasePathLabel = new();
    private readonly Label _backupFolderLabel = new();
    private readonly TextBox _reportTextBox = new();

    public MaintenanceForm(AppDatabase database)
    {
        _database = database;
        Text = "תחזוקה, גיבוי ושחזור";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(1000, 720);
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        UiLayoutHelper.ApplyFormDefaults(this);
        BuildLayout();
        UiLayoutHelper.ApplyRecursive(this);
        RefreshSummary();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
            RightToLeft = RightToLeft.Yes
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 292F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var summaryGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = ClassicPalette.CardBackground,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(0, 6, 0, 6)
        };
        summaryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        summaryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        summaryGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        summaryGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        summaryGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

        summaryGrid.Controls.Add(CreateSummaryCaption("גרסת סכימה"), 0, 0);
        summaryGrid.Controls.Add(_schemaVersionLabel, 1, 0);
        summaryGrid.Controls.Add(CreateSummaryCaption("קובץ מסד"), 0, 1);
        summaryGrid.Controls.Add(_databasePathLabel, 1, 1);
        summaryGrid.Controls.Add(CreateSummaryCaption("תיקיית גיבויים"), 0, 2);
        summaryGrid.Controls.Add(_backupFolderLabel, 1, 2);

        ConfigureSummaryValue(_schemaVersionLabel);
        ConfigureSummaryValue(_databasePathLabel);
        ConfigureSummaryValue(_backupFolderLabel);

        var actionsHost = new Panel
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(0, 6, 0, 6),
            BackColor = ClassicPalette.PanelBackground
        };

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Right,
            ColumnCount = 1,
            RowCount = 5,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(0),
            Margin = Padding.Empty,
            Width = 400
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var index = 0; index < 5; index++)
        {
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        }

        var validateButton = CreateActionButton("בדיקה ותיקון", (_, _) => RunValidationRepair(), 164);
        var backupButton = CreateActionButton("צור גיבוי", (_, _) => CreateBackup(), 144);
        var restoreButton = CreateActionButton("שחזר מגיבוי", (_, _) => RestoreFromBackup(), 156);
        var resetButton = CreateActionButton("איפוס נתוני משתמש", (_, _) => ResetUserData(), 196);
        var refreshButton = CreateActionButton("רענן נתונים", (_, _) => RefreshSummary(), 144);

        actions.Controls.Add(validateButton, 0, 0);
        actions.Controls.Add(backupButton, 0, 1);
        actions.Controls.Add(restoreButton, 0, 2);
        actions.Controls.Add(resetButton, 0, 3);
        actions.Controls.Add(refreshButton, 0, 4);
        actionsHost.Controls.Add(actions);

        _reportTextBox.Dock = DockStyle.Fill;
        _reportTextBox.Multiline = true;
        _reportTextBox.ReadOnly = true;
        _reportTextBox.ScrollBars = ScrollBars.Both;
        _reportTextBox.BackColor = Color.White;
        _reportTextBox.BorderStyle = BorderStyle.FixedSingle;
        _reportTextBox.Font = new Font("Microsoft Sans Serif", 9.5F, FontStyle.Regular);

        root.Controls.Add(summaryGrid, 0, 0);
        root.Controls.Add(actionsHost, 0, 1);
        root.Controls.Add(_reportTextBox, 0, 2);
        Controls.Add(root);
    }

    private void RefreshSummary()
    {
        _schemaVersionLabel.Text = _database.GetSchemaVersion().ToString();
        _databasePathLabel.Text = _database.DatabasePath;
        _backupFolderLabel.Text = _database.GetBackupsFolder();
        var migration = _database.GetLastMigrationSummary();
        _reportTextBox.Text =
            $"מסד פעיל: {_database.DatabasePath}{Environment.NewLine}" +
            $"גרסת סכימה נוכחית: {migration.CurrentVersion}{Environment.NewLine}" +
            $"מיגרציות שעלו בהרצה האחרונה: {(migration.AppliedMigrations.Count == 0 ? "ללא" : string.Join(", ", migration.AppliedMigrations.Select(item => $"v{item.Version}")))}{Environment.NewLine}" +
            $"גיבוי בטיחות אחרון: {(string.IsNullOrWhiteSpace(migration.SafetyBackupPath) ? "לא נוצר בהרצה האחרונה" : migration.SafetyBackupPath)}";
    }

    private void CreateBackup()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "SQLite database (*.db)|*.db",
            FileName = $"גיבוי-{DateTime.Today:yyyyMMdd}.db",
            Title = "שמירת גיבוי למסד"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _database.BackupDatabase(dialog.FileName);
        RefreshSummary();
        _reportTextBox.Text = $"הגיבוי נשמר בהצלחה:{Environment.NewLine}{dialog.FileName}";
    }

    private void RestoreFromBackup()
    {
        var confirm = MessageBox.Show(
            this,
            "השחזור יחליף את המסד הפעיל. לפני כן ייווצר גיבוי rollback אוטומטי. להמשיך?",
            "שחזור מסד",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "SQLite database (*.db)|*.db",
            Title = "בחירת גיבוי לשחזור"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var report = _database.RestoreDatabase(dialog.FileName);
        RefreshSummary();
        _reportTextBox.Text = _database.BuildBackupRestoreMarkdownReport(report);
    }

    private void RunValidationRepair()
    {
        var report = _database.ValidateAndRepairDatabase(autoRepair: true);
        RefreshSummary();
        _reportTextBox.Text = _database.BuildValidationMarkdownReport(report);
    }

    private void ResetUserData()
    {
        var confirm = MessageBox.Show(
            this,
            "הפעולה תאפס את כל נתוני השימוש והשמורים, אך תשאיר את הספרייה המובנית, הסכמה והמיגרציות. לפני האיפוס ייווצר גיבוי בטיחות אוטומטי. להמשיך?",
            "איפוס נתוני משתמש",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        var report = _database.ResetUserData();
        RefreshSummary();
        _reportTextBox.Text = _database.BuildUserDataResetMarkdownReport(report);
    }

    private static Label CreateSummaryCaption(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Microsoft Sans Serif", 9.25F, FontStyle.Bold)
        };
    }

    private static void ConfigureSummaryValue(Label label)
    {
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleRight;
        label.BackColor = Color.White;
        label.BorderStyle = BorderStyle.FixedSingle;
        label.Padding = new Padding(8, 6, 8, 6);
    }

    private static Button CreateActionButton(string text, EventHandler onClick, int minWidth)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Height = 44,
            Margin = new Padding(4, 4, 4, 6),
            FlatStyle = FlatStyle.Standard
        };
        UiLayoutHelper.StyleActionButton(button, minWidth, 44);
        button.TextAlign = ContentAlignment.MiddleRight;
        button.Padding = new Padding(16, 6, 22, 6);
        button.Click += onClick;
        return button;
    }
}
