namespace TrackerApp;

public sealed class TagsManagerControl : UserControl
{
    private readonly ListBox _tagListBox = new();
    private readonly Label _summaryLabel = new();
    private readonly Label _detailsLabel = new();

    public event EventHandler? AddTagRequested;
    public event EventHandler<TagModel>? EditTagRequested;
    public event EventHandler<TagModel>? DeleteTagRequested;
    public event EventHandler<TagModel>? FilterByTagRequested;

    public TagsManagerControl()
    {
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        Dock = DockStyle.Fill;
        BuildLayout();
        UiLayoutHelper.ApplyRecursive(this);
    }

    public TagModel? SelectedTag => _tagListBox.SelectedItem as TagModel;

    public void BindTags(IReadOnlyList<TagModel> tags)
    {
        var selectedId = SelectedTag?.Id;
        _tagListBox.BeginUpdate();
        _tagListBox.Items.Clear();
        foreach (var tag in tags)
        {
            _tagListBox.Items.Add(tag);
        }

        _tagListBox.EndUpdate();
        _summaryLabel.Text = $"סה\"כ תגיות: {tags.Count}";

        if (selectedId.HasValue)
        {
            for (var index = 0; index < _tagListBox.Items.Count; index++)
            {
                if (_tagListBox.Items[index] is TagModel tag && tag.Id == selectedId)
                {
                    _tagListBox.SelectedIndex = index;
                    return;
                }
            }
        }

        if (_tagListBox.Items.Count > 0)
        {
            _tagListBox.SelectedIndex = 0;
        }
        else
        {
            _detailsLabel.Text = "עדיין לא הוגדרו תגיות.";
        }
    }

    public void SelectTag(int tagId)
    {
        for (var index = 0; index < _tagListBox.Items.Count; index++)
        {
            if (_tagListBox.Items[index] is TagModel tag && tag.Id == tagId)
            {
                _tagListBox.SelectedIndex = index;
                return;
            }
        }
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(8)
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126F));

        var listPanel = CreateSectionContainer("רשימת תגיות", out var listBody);
        _tagListBox.Dock = DockStyle.Fill;
        _tagListBox.IntegralHeight = false;
        _tagListBox.DrawMode = DrawMode.OwnerDrawFixed;
        _tagListBox.ItemHeight = 28;
        _tagListBox.DrawItem += DrawTagListItem;
        _tagListBox.SelectedIndexChanged += (_, _) => UpdateSelectedTagDetails();
        listBody.Controls.Add(_tagListBox);

        var detailsPanel = CreateSectionContainer("פרטי תגית", out var detailsBody);
        _detailsLabel.Dock = DockStyle.Fill;
        _detailsLabel.Padding = new Padding(12);
        _detailsLabel.TextAlign = ContentAlignment.TopRight;
        _detailsLabel.RightToLeft = RightToLeft.Yes;
        detailsBody.Controls.Add(_detailsLabel);

        var footerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(0, 8, 0, 4)
        };
        footerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        footerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));

        var addButton = CreateButton("תגית חדשה");
        addButton.Click += (_, _) => AddTagRequested?.Invoke(this, EventArgs.Empty);
        var editButton = CreateButton("עריכת תגית");
        editButton.Click += (_, _) =>
        {
            if (SelectedTag is not null)
            {
                EditTagRequested?.Invoke(this, SelectedTag);
            }
        };
        var deleteButton = CreateButton("מחיקת תגית");
        deleteButton.Click += (_, _) =>
        {
            if (SelectedTag is not null)
            {
                DeleteTagRequested?.Invoke(this, SelectedTag);
            }
        };
        var filterButton = CreateButton("הצג כרטיסים");
        filterButton.Click += (_, _) =>
        {
            if (SelectedTag is not null)
            {
                FilterByTagRequested?.Invoke(this, SelectedTag);
            }
        };

        _summaryLabel.AutoSize = true;
        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleRight;
        _summaryLabel.Margin = new Padding(16, 6, 0, 0);

        var buttonsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes
        };
        buttonsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        buttonsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        buttonsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        buttonsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        buttonsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        addButton.Dock = DockStyle.Fill;
        editButton.Dock = DockStyle.Fill;
        deleteButton.Dock = DockStyle.Fill;
        filterButton.Dock = DockStyle.Fill;
        buttonsGrid.Controls.Add(addButton, 0, 0);
        buttonsGrid.Controls.Add(editButton, 1, 0);
        buttonsGrid.Controls.Add(deleteButton, 2, 0);
        buttonsGrid.Controls.Add(filterButton, 3, 0);

        footerLayout.Controls.Add(_summaryLabel, 0, 0);
        footerLayout.Controls.Add(buttonsGrid, 0, 1);

        root.Controls.Add(listPanel, 0, 0);
        root.Controls.Add(detailsPanel, 1, 0);
        root.Controls.Add(footerLayout, 0, 1);
        root.SetColumnSpan(footerLayout, 2);

        Controls.Add(root);
    }

    private void UpdateSelectedTagDetails()
    {
        if (SelectedTag is null)
        {
            _detailsLabel.Text = "בחרו תגית מהרשימה.";
            return;
        }

        _detailsLabel.Text =
            $"שם תגית: {SelectedTag.Name}{Environment.NewLine}{Environment.NewLine}" +
            $"מספר כרטיסים משויכים: {SelectedTag.UsageCount}{Environment.NewLine}{Environment.NewLine}" +
            "אפשר ליצור, לערוך, למחוק או להציג את כל הכרטיסים של תגית זו.";
    }

    private void DrawTagListItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _tagListBox.Items.Count)
        {
            return;
        }

        var text = _tagListBox.Items[e.Index]?.ToString() ?? string.Empty;
        var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var textColor = isSelected ? SystemColors.HighlightText : _tagListBox.ForeColor;
        var bounds = Rectangle.Inflate(e.Bounds, -10, 0);

        TextRenderer.DrawText(
            e.Graphics,
            text,
            _tagListBox.Font,
            bounds,
            textColor,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        e.DrawFocusRectangle();
    }

    private static Panel CreateSectionContainer(string title, out Panel bodyHost)
    {
        bodyHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ClassicPalette.CardBackground,
            Padding = new Padding(8),
            RightToLeft = RightToLeft.Yes
        };

        var header = new Label
        {
            Dock = DockStyle.Top,
            Text = title
        };
        UiLayoutHelper.StyleSectionHeader(header, 34);

        var container = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = ClassicPalette.CardBackground
        };
        container.Controls.Add(bodyHost);
        container.Controls.Add(header);
        return container;
    }

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Width = 152,
            Height = 42,
            Margin = new Padding(4, 6, 4, 6)
        };
        UiLayoutHelper.StyleActionButton(button, 138, 42);
        return button;
    }
}
