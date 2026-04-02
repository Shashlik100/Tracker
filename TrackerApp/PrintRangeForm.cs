namespace TrackerApp;

public sealed class PrintRangeForm : Form
{
    private readonly DateTimePicker _startPicker = new();
    private readonly DateTimePicker _endPicker = new();

    public PrintRangeForm()
    {
        Text = "הדפסה לשבת";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 198);
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        UiLayoutHelper.ApplyFormDefaults(this);

        BuildLayout();
        UiLayoutHelper.ApplyRecursive(this);
        SetWeekendDefaults();
    }

    public DateTime StartDate => _startPicker.Value.Date;
    public DateTime EndDate => _endPicker.Value.Date;

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(12),
            RightToLeft = RightToLeft.Yes
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));

        _startPicker.Format = DateTimePickerFormat.Short;
        _endPicker.Format = DateTimePickerFormat.Short;
        _startPicker.Dock = DockStyle.Fill;
        _endPicker.Dock = DockStyle.Fill;

        layout.Controls.Add(CreateLabel("תאריך התחלה"), 0, 0);
        layout.Controls.Add(_startPicker, 1, 0);
        layout.Controls.Add(CreateLabel("תאריך סיום"), 0, 1);
        layout.Controls.Add(_endPicker, 1, 1);

        var weekendButton = new Button
        {
            Text = "סוף השבוע הקרוב",
            Width = 150,
            Height = 34
        };
        weekendButton.Click += (_, _) => SetWeekendDefaults();

        var okButton = new Button
        {
            Text = "ייצוא",
            DialogResult = DialogResult.OK,
            Width = 96,
            Height = 34
        };

        var cancelButton = new Button
        {
            Text = "ביטול",
            DialogResult = DialogResult.Cancel,
            Width = 96,
            Height = 34
        };

        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes
        };
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 164F));
        okButton.Dock = DockStyle.Fill;
        cancelButton.Dock = DockStyle.Fill;
        weekendButton.Dock = DockStyle.Fill;
        buttonPanel.Controls.Add(okButton, 0, 0);
        buttonPanel.Controls.Add(cancelButton, 1, 0);
        buttonPanel.Controls.Add(weekendButton, 2, 0);

        layout.Controls.Add(buttonPanel, 1, 2);
        Controls.Add(layout);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void SetWeekendDefaults()
    {
        var today = DateTime.Today;
        var daysUntilFriday = ((int)DayOfWeek.Friday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilFriday == 0 && today.DayOfWeek == DayOfWeek.Friday)
        {
            daysUntilFriday = 7;
        }

        var friday = today.AddDays(daysUntilFriday);
        _startPicker.Value = friday;
        _endPicker.Value = friday.AddDays(1);
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleRight
        };
    }
}
