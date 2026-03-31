namespace TrackerApp;

internal static class UiLayoutHelper
{
    private static readonly Font BaseFont = new("Microsoft Sans Serif", 9.25F, FontStyle.Regular, GraphicsUnit.Point);
    private static readonly Font BoldFont = new("Microsoft Sans Serif", 9.25F, FontStyle.Bold, GraphicsUnit.Point);
    private static readonly Font SectionHeaderFont = new("Microsoft Sans Serif", 10F, FontStyle.Bold, GraphicsUnit.Point);

    public static void ApplyFormDefaults(Form form)
    {
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.Font = BaseFont;
        form.RightToLeft = RightToLeft.Yes;
        form.AutoScaleDimensions = new SizeF(96F, 96F);
    }

    public static void ApplyRecursive(Control root)
    {
        ApplyControlMetrics(root);
        foreach (Control child in root.Controls)
        {
            ApplyRecursive(child);
        }

        if (root is ToolStrip strip)
        {
            ApplyToolStripMetrics(strip);
        }
    }

    public static void StyleActionButton(Button button, int minWidth = 112, int minHeight = 38)
    {
        button.UseCompatibleTextRendering = true;
        button.RightToLeft = RightToLeft.Yes;
        button.TextAlign = ContentAlignment.MiddleRight;
        button.TextImageRelation = TextImageRelation.TextBeforeImage;
        button.Padding = new Padding(10, 6, 18, 6);
        button.AutoEllipsis = false;
        button.AutoSize = false;
        button.Font = button.Font.Style.HasFlag(FontStyle.Bold) ? button.Font : BoldFont;
        button.Height = Math.Max(button.Height, minHeight);
        button.MinimumSize = new Size(Math.Max(minWidth, MeasureButtonWidth(button.Text, button.Font)), minHeight);
        if (button.Dock == DockStyle.None && button.Width < button.MinimumSize.Width)
        {
            button.Width = button.MinimumSize.Width;
        }
    }

    public static void StyleSectionHeader(Label label, int minHeight = 32)
    {
        label.UseCompatibleTextRendering = true;
        label.AutoSize = false;
        label.AutoEllipsis = false;
        label.RightToLeft = RightToLeft.Yes;
        label.Font = SectionHeaderFont;
        label.BackColor = ClassicPalette.Teal;
        label.ForeColor = Color.White;
        label.BorderStyle = BorderStyle.FixedSingle;
        label.TextAlign = ContentAlignment.MiddleRight;
        label.Margin = Padding.Empty;
        label.Padding = new Padding(
            Math.Max(label.Padding.Left, 12),
            Math.Max(label.Padding.Top, 2),
            Math.Max(label.Padding.Right, 12),
            Math.Max(label.Padding.Bottom, 2));
        label.Height = Math.Max(label.Height, minHeight);
        label.MinimumSize = new Size(0, minHeight);
    }

    public static void StyleDataGridView(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = Math.Max(grid.ColumnHeadersHeight, 38);
        grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, 34);
        grid.DefaultCellStyle.Padding = new Padding(4, 5, 4, 5);
        grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.RightToLeft = RightToLeft.Yes;
        grid.Font = BaseFont;
    }

    public static int MeasureButtonWidth(string text, Font font, int extra = 36)
    {
        return TextRenderer.MeasureText(
            text,
            font,
            Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width + extra;
    }

    private static void ApplyControlMetrics(Control control)
    {
        control.RightToLeft = RightToLeft.Yes;
        if (control.Font.Size < BaseFont.Size)
        {
            control.Font = BaseFont;
        }

        switch (control)
        {
            case Button button:
                StyleActionButton(button, button.MinimumSize.Width > 0 ? button.MinimumSize.Width : 96);
                break;
            case Label label:
                label.UseCompatibleTextRendering = true;
                label.AutoEllipsis = false;
                if (label.Dock == DockStyle.Top && label.Height < 26)
                {
                    label.Height = 26;
                }
                if (label.TextAlign == ContentAlignment.TopRight)
                {
                    label.Padding = new Padding(Math.Max(label.Padding.Left, 6), Math.Max(label.Padding.Top, 6), Math.Max(label.Padding.Right, 6), Math.Max(label.Padding.Bottom, 6));
                }
                label.TextAlign = ConvertToRightAlignedTextAlign(label.TextAlign);
                break;
            case GroupBox groupBox:
                groupBox.Font = groupBox.Font.Style.HasFlag(FontStyle.Bold) ? groupBox.Font : BoldFont;
                groupBox.Padding = new Padding(Math.Max(groupBox.Padding.Left, 8), Math.Max(groupBox.Padding.Top, 14), Math.Max(groupBox.Padding.Right, 8), Math.Max(groupBox.Padding.Bottom, 8));
                break;
            case DataGridView grid:
                StyleDataGridView(grid);
                break;
            case ComboBox comboBox:
                comboBox.Font = BaseFont;
                comboBox.IntegralHeight = false;
                if (comboBox.Height < 32)
                {
                    comboBox.Height = 32;
                }
                break;
            case DateTimePicker dateTimePicker:
                dateTimePicker.Font = BaseFont;
                if (dateTimePicker.Height < 32)
                {
                    dateTimePicker.Height = 32;
                }
                break;
            case TextBox textBox:
                textBox.Font = BaseFont;
                if (!textBox.Multiline && textBox.Height < 32)
                {
                    textBox.Height = 32;
                }
                break;
            case CheckBox checkBox:
                checkBox.Font = BaseFont;
                checkBox.AutoSize = true;
                checkBox.Padding = new Padding(Math.Max(checkBox.Padding.Left, 2), Math.Max(checkBox.Padding.Top, 4), Math.Max(checkBox.Padding.Right, 2), Math.Max(checkBox.Padding.Bottom, 4));
                break;
            case RadioButton radioButton:
                radioButton.Font = BaseFont;
                radioButton.AutoSize = true;
                radioButton.Padding = new Padding(Math.Max(radioButton.Padding.Left, 2), Math.Max(radioButton.Padding.Top, 4), Math.Max(radioButton.Padding.Right, 2), Math.Max(radioButton.Padding.Bottom, 4));
                break;
            case CheckedListBox checkedListBox:
                checkedListBox.Font = BaseFont;
                checkedListBox.ItemHeight = Math.Max(checkedListBox.ItemHeight, 26);
                break;
            case ListBox listBox:
                listBox.Font = BaseFont;
                listBox.ItemHeight = Math.Max(listBox.ItemHeight, 26);
                break;
            case TreeView treeView:
                treeView.Font = BaseFont;
                treeView.ItemHeight = Math.Max(treeView.ItemHeight, 26);
                treeView.Indent = Math.Max(12, Math.Min(treeView.Indent, 16));
                break;
            case SplitContainer splitContainer:
                splitContainer.Dock = DockStyle.Fill;
                splitContainer.Panel1MinSize = Math.Max(splitContainer.Panel1MinSize, 120);
                splitContainer.Panel2MinSize = Math.Max(splitContainer.Panel2MinSize, 120);
                splitContainer.SplitterWidth = Math.Max(splitContainer.SplitterWidth, 6);
                break;
            case TableLayoutPanel tableLayout:
                tableLayout.Margin = new Padding(
                    Math.Max(tableLayout.Margin.Left, 2),
                    Math.Max(tableLayout.Margin.Top, 2),
                    Math.Max(tableLayout.Margin.Right, 2),
                    Math.Max(tableLayout.Margin.Bottom, 2));
                break;
            case FlowLayoutPanel flowPanel:
                flowPanel.Padding = new Padding(
                    Math.Max(flowPanel.Padding.Left, 2),
                    Math.Max(flowPanel.Padding.Top, 2),
                    Math.Max(flowPanel.Padding.Right, 2),
                    Math.Max(flowPanel.Padding.Bottom, 2));
                var buttonCount = flowPanel.Controls.OfType<Button>().Count();
                if (buttonCount >= 3)
                {
                    flowPanel.WrapContents = true;
                    flowPanel.AutoScroll = false;
                    flowPanel.Height = Math.Max(flowPanel.Height, buttonCount >= 5 ? 104 : 80);
                }
                break;
        }
    }

    private static ContentAlignment ConvertToRightAlignedTextAlign(ContentAlignment alignment)
    {
        return alignment switch
        {
            ContentAlignment.TopLeft => ContentAlignment.TopRight,
            ContentAlignment.MiddleLeft => ContentAlignment.MiddleRight,
            ContentAlignment.BottomLeft => ContentAlignment.BottomRight,
            _ => alignment
        };
    }

    private static void ApplyToolStripMetrics(ToolStrip strip)
    {
        strip.Font = BaseFont;
        strip.Padding = new Padding(Math.Max(strip.Padding.Left, 8), Math.Max(strip.Padding.Top, 9), Math.Max(strip.Padding.Right, 8), Math.Max(strip.Padding.Bottom, 9));
        if (strip.AutoSize)
        {
            return;
        }

        strip.Height = Math.Max(strip.Height, 44);
        foreach (ToolStripItem item in strip.Items)
        {
            item.RightToLeft = RightToLeft.Yes;
            if (item is ToolStripButton button)
            {
                button.AutoSize = false;
                button.Height = Math.Max(button.Height, 30);
                button.Width = Math.Max(button.Width, MeasureButtonWidth(button.Text ?? string.Empty, button.Font));
                button.Margin = new Padding(2, 2, 2, 2);
            }
            else if (item is ToolStripComboBox comboBox)
            {
                comboBox.AutoSize = false;
                comboBox.Height = Math.Max(comboBox.Height, 30);
            }
            else if (item is ToolStripMenuItem menuItem)
            {
                menuItem.AutoSize = false;
                menuItem.Height = Math.Max(menuItem.Height, 36);
                menuItem.Width = Math.Max(menuItem.Width, MeasureButtonWidth(menuItem.Text ?? string.Empty, menuItem.Font, 36));
            }
        }
    }
}
