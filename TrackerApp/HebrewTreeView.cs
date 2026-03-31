namespace TrackerApp;

public sealed class HebrewTreeView : TreeView
{
    private const int WS_EX_RTLREADING = 0x00002000;
    private const int WS_EX_LAYOUTRTL = 0x00400000;
    private const int TVS_RTLREADING = 0x0040;

    public HebrewTreeView()
    {
        RightToLeft = RightToLeft.Yes;
        DrawMode = TreeViewDrawMode.OwnerDrawText;
        FullRowSelect = true;
        HideSelection = false;
        Indent = 14;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_RTLREADING | WS_EX_LAYOUTRTL;
            cp.Style |= TVS_RTLREADING;
            return cp;
        }
    }

    protected override void OnDrawNode(DrawTreeNodeEventArgs e)
    {
        if (e.Node is null)
        {
            base.OnDrawNode(e);
            return;
        }

        var selected = (e.State & TreeNodeStates.Selected) == TreeNodeStates.Selected;
        var backColor = selected ? SystemColors.Highlight : BackColor;
        var foreColor = selected ? SystemColors.HighlightText : ForeColor;

        using (var backBrush = new SolidBrush(backColor))
        {
            e.Graphics.FillRectangle(backBrush, e.Bounds);
        }

        var textBounds = new Rectangle(4, e.Bounds.Top, Math.Max(8, Width - 8), e.Bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            e.Node.Text,
            Font,
            textBounds,
            foreColor,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        if ((e.State & TreeNodeStates.Focused) == TreeNodeStates.Focused)
        {
            ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, foreColor, backColor);
        }
    }
}
