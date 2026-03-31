namespace TrackerApp;

public sealed class CsvImportForm : Form
{
    private readonly AppDatabase _database;
    private readonly int? _fallbackSubjectId;
    private readonly TextBox _filePathTextBox = new();
    private readonly CheckBox _hasHeaderCheckBox = new();
    private readonly Label _fallbackLabel = new();
    private readonly Label _summaryLabel = new();
    private readonly DataGridView _previewGrid = new();
    private readonly Dictionary<CsvFieldType, ComboBox> _mappingBoxes = new();
    private string[] _columnNames = [];
    private CsvImportPreviewResult? _lastPreview;

    public CsvImportForm(AppDatabase database, int? fallbackSubjectId, string fallbackSubjectPath)
    {
        _database = database;
        _fallbackSubjectId = fallbackSubjectId;

        Text = "ייבוא CSV";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(1080, 720);
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = false;
        UiLayoutHelper.ApplyFormDefaults(this);

        BuildLayout(fallbackSubjectPath);
        UiLayoutHelper.ApplyRecursive(this);
    }

    public CsvImportPreviewResult? LastPreview => _lastPreview;

    private void BuildLayout(string fallbackSubjectPath)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 208F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F));

        var filePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4
        };
        filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
        filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));

        _filePathTextBox.Dock = DockStyle.Fill;
        _filePathTextBox.RightToLeft = RightToLeft.No;
        _hasHeaderCheckBox.Text = "לקובץ יש שורת כותרת";
        _hasHeaderCheckBox.Checked = true;
        _hasHeaderCheckBox.Dock = DockStyle.Fill;

        var browseButton = new Button { Text = "בחירת קובץ", Dock = DockStyle.Fill };
        browseButton.Click += (_, _) => BrowseFile();
        var previewButton = new Button { Text = "טעינת תצוגה מקדימה", Dock = DockStyle.Fill };
        previewButton.Click += (_, _) => LoadPreview();

        filePanel.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "קובץ", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
        filePanel.Controls.Add(_filePathTextBox, 1, 0);
        filePanel.Controls.Add(browseButton, 2, 0);
        filePanel.Controls.Add(previewButton, 3, 0);

        var mappingLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4
        };
        for (var index = 0; index < 4; index++)
        {
            mappingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        AddMappingRow(mappingLayout, 0, CsvFieldType.Topic, "נושא");
        AddMappingRow(mappingLayout, 1, CsvFieldType.Question, "שאלה");
        AddMappingRow(mappingLayout, 2, CsvFieldType.Answer, "תשובה");
        AddMappingRow(mappingLayout, 3, CsvFieldType.Book, "ספר");
        AddMappingRow(mappingLayout, 4, CsvFieldType.Chapter, "פרק");
        AddMappingRow(mappingLayout, 5, CsvFieldType.Verse, "פסוק");
        AddMappingRow(mappingLayout, 6, CsvFieldType.Difficulty, "קושי");
        AddMappingRow(mappingLayout, 7, CsvFieldType.Tags, "תגיות");

        var mappingHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _fallbackLabel.Dock = DockStyle.Bottom;
        _fallbackLabel.Height = 22;
        _fallbackLabel.TextAlign = ContentAlignment.MiddleRight;
        _fallbackLabel.Text = _fallbackSubjectId.HasValue
            ? $"מיקום ברירת מחדל: {fallbackSubjectPath}"
            : "אין כרגע צומת נבחר. יש למפות ספר/פרק/פסוק או לבחור צומת לפני ייבוא.";
        mappingHost.Controls.Add(mappingLayout);
        mappingHost.Controls.Add(_fallbackLabel);

        _previewGrid.Dock = DockStyle.Fill;
        _previewGrid.AllowUserToAddRows = false;
        _previewGrid.AllowUserToDeleteRows = false;
        _previewGrid.ReadOnly = true;
        _previewGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _previewGrid.RowHeadersVisible = false;
        UiLayoutHelper.StyleDataGridView(_previewGrid);

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleRight;

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        var importButton = new Button { Text = "ייבוא", Width = 112, Height = 38 };
        UiLayoutHelper.StyleActionButton(importButton, 112, 38);
        importButton.Click += (_, _) => ExecuteImport();
        var cancelButton = new Button { Text = "ביטול", Width = 100, Height = 38, DialogResult = DialogResult.Cancel };
        UiLayoutHelper.StyleActionButton(cancelButton, 100, 38);
        importButton.Dock = DockStyle.Fill;
        cancelButton.Dock = DockStyle.Fill;
        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleRight;
        footer.Controls.Add(importButton, 0, 0);
        footer.Controls.Add(cancelButton, 1, 0);
        footer.Controls.Add(_summaryLabel, 2, 0);
        _summaryLabel.AutoSize = true;
        _summaryLabel.Margin = new Padding(18, 10, 0, 0);

        root.Controls.Add(filePanel, 0, 0);
        root.Controls.Add(mappingHost, 0, 1);
        root.Controls.Add(_previewGrid, 0, 2);
        root.Controls.Add(footer, 0, 3);

        Controls.Add(root);
        CancelButton = cancelButton;
    }

    private void AddMappingRow(TableLayoutPanel layout, int index, CsvFieldType fieldType, string caption)
    {
        var row = index / 4;
        if (layout.RowCount <= row)
        {
            layout.RowCount = row + 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        }

        var column = (index % 4);
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var combo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _mappingBoxes[fieldType] = combo;

        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = caption,
            TextAlign = ContentAlignment.MiddleRight
        }, 0, 0);
        panel.Controls.Add(combo, 1, 0);
        layout.Controls.Add(panel, column, row);
    }

    private void BrowseFile()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            Title = "בחירת קובץ CSV"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _filePathTextBox.Text = dialog.FileName;
        LoadColumns(dialog.FileName);
    }

    public void AutomationLoadPreview(string filePath)
    {
        _filePathTextBox.Text = filePath;
        LoadColumns(filePath);
        LoadPreview();
    }

    public int AutomationImport()
    {
        if (_lastPreview is null)
        {
            LoadPreview();
        }

        var imported = _database.ImportCsvWithMapping(_filePathTextBox.Text, BuildMapping());
        DialogResult = DialogResult.OK;
        Close();
        return imported;
    }

    private void LoadColumns(string filePath)
    {
        var rows = CsvUtility.ReadRows(filePath);
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("קובץ ה-CSV ריק.");
        }

        _columnNames = (_hasHeaderCheckBox.Checked ? rows[0] : rows[0].Select((_, index) => $"עמודה {index + 1}").ToArray());
        foreach (var pair in _mappingBoxes)
        {
            var combo = pair.Value;
            combo.Items.Clear();
            combo.Items.Add(new ColumnChoice(-1, "לא למפות"));
            for (var index = 0; index < _columnNames.Length; index++)
            {
                combo.Items.Add(new ColumnChoice(index, _columnNames[index]));
            }

            combo.SelectedIndex = 0;
        }

        AutoMapColumns();
    }

    private void AutoMapColumns()
    {
        MapByKeyword(CsvFieldType.Topic, ["נושא", "topic"]);
        MapByKeyword(CsvFieldType.Question, ["שאלה", "question"]);
        MapByKeyword(CsvFieldType.Answer, ["תשובה", "answer"]);
        MapByKeyword(CsvFieldType.Book, ["ספר", "book"]);
        MapByKeyword(CsvFieldType.Chapter, ["פרק", "chapter"]);
        MapByKeyword(CsvFieldType.Verse, ["פסוק", "verse"]);
        MapByKeyword(CsvFieldType.Difficulty, ["קושי", "difficulty"]);
        MapByKeyword(CsvFieldType.Tags, ["תגיות", "tags"]);
    }

    private void MapByKeyword(CsvFieldType fieldType, string[] keywords)
    {
        if (_columnNames.Length == 0)
        {
            return;
        }

        for (var index = 0; index < _columnNames.Length; index++)
        {
            if (keywords.Any(keyword => _columnNames[index].Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                _mappingBoxes[fieldType].SelectedIndex = index + 1;
                return;
            }
        }
    }

    private void LoadPreview()
    {
        if (!File.Exists(_filePathTextBox.Text))
        {
            MessageBox.Show(this, "יש לבחור קובץ CSV תקין.", "קובץ חסר", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return;
        }

        if (_columnNames.Length == 0)
        {
            LoadColumns(_filePathTextBox.Text);
        }

        var mapping = BuildMapping();
        _lastPreview = _database.PreviewCsvImport(_filePathTextBox.Text, mapping);

        _previewGrid.DataSource = _lastPreview.Rows.Select(row => new
        {
            שורה = row.RowNumber,
            תקין = row.IsValid ? "כן" : "לא",
            נושא = row.Topic,
            מיקום = row.SubjectPath,
            קושי = row.Difficulty,
            תגיות = row.Tags,
            סיבה = row.Reason
        }).ToList();

        _summaryLabel.Text = $"יתקבלו: {_lastPreview.AcceptedCount} | יידחו: {_lastPreview.RejectedCount}";
    }

    private void ExecuteImport()
    {
        if (_lastPreview is null)
        {
            LoadPreview();
        }

        if (_lastPreview is null)
        {
            return;
        }

        var imported = _database.ImportCsvWithMapping(_filePathTextBox.Text, BuildMapping());
        MessageBox.Show(
            this,
            $"ייבוא הושלם. נוספו {imported} כרטיסים.",
            "ייבוא CSV",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        DialogResult = DialogResult.OK;
        Close();
    }

    private CsvImportMapping BuildMapping()
    {
        var map = new Dictionary<int, CsvFieldType>();
        foreach (var pair in _mappingBoxes)
        {
            if (pair.Value.SelectedItem is ColumnChoice choice && choice.Index >= 0)
            {
                map[choice.Index] = pair.Key;
            }
        }

        return new CsvImportMapping
        {
            ColumnMappings = map,
            FallbackSubjectId = _fallbackSubjectId,
            HasHeaderRow = _hasHeaderCheckBox.Checked
        };
    }

    private sealed record ColumnChoice(int Index, string Name)
    {
        public override string ToString() => Name;
    }
}
