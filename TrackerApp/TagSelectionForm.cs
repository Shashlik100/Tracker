namespace TrackerApp;

public sealed class TagSelectionForm : Form
{
    private readonly CheckedListBox _listBox = new();

    public TagSelectionForm(string title, IReadOnlyList<TagModel> tags, IReadOnlyCollection<int>? initiallySelected = null)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 470);
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = false;
        UiLayoutHelper.ApplyFormDefaults(this);
        BuildLayout(tags, initiallySelected ?? []);
        UiLayoutHelper.ApplyRecursive(this);
    }

    public IReadOnlyList<int> SelectedTagIds =>
        _listBox.CheckedItems.OfType<TagModel>().Select(tag => tag.Id).OrderBy(id => id).ToArray();

    private void BuildLayout(IReadOnlyList<TagModel> tags, IReadOnlyCollection<int> selectedIds)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));

        _listBox.Dock = DockStyle.Fill;
        _listBox.CheckOnClick = true;
        foreach (var tag in tags)
        {
            var index = _listBox.Items.Add(tag);
            if (selectedIds.Contains(tag.Id))
            {
                _listBox.SetItemChecked(index, true);
            }
        }

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

        root.Controls.Add(_listBox, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
