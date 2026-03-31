namespace TrackerApp;

public sealed class TagEditForm : Form
{
    private readonly TextBox _nameTextBox = new();

    public TagEditForm(string title, string initialName = "")
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 156);
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = false;
        UiLayoutHelper.ApplyFormDefaults(this);
        BuildLayout(initialName);
        UiLayoutHelper.ApplyRecursive(this);
    }

    public string TagName => _nameTextBox.Text.Trim();

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK && string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            MessageBox.Show(
                this,
                "יש להזין שם תגית.",
                "שדה חסר",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            e.Cancel = true;
        }

        base.OnFormClosing(e);
    }

    private void BuildLayout(string initialName)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));

        _nameTextBox.Dock = DockStyle.Fill;
        _nameTextBox.RightToLeft = RightToLeft.Yes;
        _nameTextBox.Text = initialName;

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "שם תגית",
            TextAlign = ContentAlignment.MiddleRight
        }, 0, 0);
        layout.Controls.Add(_nameTextBox, 1, 0);

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
        var okButton = new Button
        {
            Text = "שמור",
            DialogResult = DialogResult.OK,
            Width = 90
        };
        var cancelButton = new Button
        {
            Text = "ביטול",
            DialogResult = DialogResult.Cancel,
            Width = 90
        };
        okButton.Dock = DockStyle.Fill;
        cancelButton.Dock = DockStyle.Fill;
        buttons.Controls.Add(okButton, 0, 0);
        buttons.Controls.Add(cancelButton, 1, 0);
        layout.Controls.Add(buttons, 1, 1);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
