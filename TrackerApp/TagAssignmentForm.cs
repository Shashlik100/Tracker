namespace TrackerApp;

public sealed class TagAssignmentForm : Form
{
    private readonly CheckedListBox _tagsListBox = new();

    public TagAssignmentForm(string itemTopic, IReadOnlyList<TagModel> availableTags, IReadOnlyCollection<int> selectedTagIds)
    {
        Text = "שיוך תגיות";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 480);
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = false;
        UiLayoutHelper.ApplyFormDefaults(this);
        BuildLayout(itemTopic, availableTags, selectedTagIds);
        UiLayoutHelper.ApplyRecursive(this);
    }

    public IReadOnlyList<int> SelectedTagIds
    {
        get
        {
            return _tagsListBox.CheckedItems
                .OfType<TagModel>()
                .Select(tag => tag.Id)
                .OrderBy(id => id)
                .ToArray();
        }
    }

    private void BuildLayout(string itemTopic, IReadOnlyList<TagModel> availableTags, IReadOnlyCollection<int> selectedTagIds)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"כרטיס: {itemTopic}",
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font(Font, FontStyle.Bold)
        }, 0, 0);

        _tagsListBox.Dock = DockStyle.Fill;
        _tagsListBox.BorderStyle = BorderStyle.FixedSingle;
        _tagsListBox.CheckOnClick = true;
        foreach (var tag in availableTags)
        {
            var index = _tagsListBox.Items.Add(tag);
            if (selectedTagIds.Contains(tag.Id))
            {
                _tagsListBox.SetItemChecked(index, true);
            }
        }

        root.Controls.Add(_tagsListBox, 0, 1);

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
        root.Controls.Add(buttons, 0, 2);

        Controls.Add(root);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
