namespace TrackerApp;

public sealed class DifficultySelectionForm : Form
{
    private readonly ComboBox _difficultyComboBox = new();

    public DifficultySelectionForm()
    {
        Text = "בחירת קושי";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 156);
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = false;
        UiLayoutHelper.ApplyFormDefaults(this);
        BuildLayout();
        UiLayoutHelper.ApplyRecursive(this);
    }

    public StudyDifficulty SelectedDifficulty =>
        _difficultyComboBox.SelectedItem is DifficultyChoice choice ? choice.Difficulty : StudyDifficulty.Medium;

    private void BuildLayout()
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

        _difficultyComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _difficultyComboBox.Dock = DockStyle.Fill;
        _difficultyComboBox.Items.AddRange(
        [
            new DifficultyChoice(StudyDifficulty.Hard, "קשה"),
            new DifficultyChoice(StudyDifficulty.Medium, "בינונית"),
            new DifficultyChoice(StudyDifficulty.Easy, "קלה")
        ]);
        _difficultyComboBox.SelectedIndex = 1;

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "קושי",
            TextAlign = ContentAlignment.MiddleRight
        }, 0, 0);
        layout.Controls.Add(_difficultyComboBox, 1, 0);

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
            Text = "אישור",
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

        okButton.Dock = DockStyle.Fill;
        cancelButton.Dock = DockStyle.Fill;
        buttons.Controls.Add(okButton, 0, 0);
        buttons.Controls.Add(cancelButton, 1, 0);
        layout.Controls.Add(buttons, 1, 1);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private sealed record DifficultyChoice(StudyDifficulty Difficulty, string Name)
    {
        public override string ToString() => Name;
    }
}
