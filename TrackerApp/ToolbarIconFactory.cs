using System.Drawing.Drawing2D;

namespace TrackerApp;

internal static class ToolbarIconFactory
{
    public static Bitmap CreatePlusIcon() => CreateBitmap(graphics =>
    {
        using var brush = new SolidBrush(ClassicPalette.TealDark);
        graphics.FillRectangle(brush, 6, 2, 4, 12);
        graphics.FillRectangle(brush, 2, 6, 12, 4);
    });

    public static Bitmap CreateRefreshIcon() => CreateBitmap(graphics =>
    {
        using var pen = new Pen(ClassicPalette.TealDark, 2F);
        graphics.DrawArc(pen, 2, 2, 10, 10, 25, 270);
        graphics.DrawLine(pen, 9, 2, 14, 2);
        graphics.DrawLine(pen, 14, 2, 14, 7);
    });

    public static Bitmap CreateChartIcon() => CreateBitmap(graphics =>
    {
        using var brush = new SolidBrush(ClassicPalette.TealDark);
        graphics.FillRectangle(brush, 2, 9, 3, 5);
        graphics.FillRectangle(brush, 7, 5, 3, 9);
        graphics.FillRectangle(brush, 12, 2, 3, 12);
    });

    public static Bitmap CreateTreeIcon() => CreateBitmap(graphics =>
    {
        using var pen = new Pen(ClassicPalette.TealDark, 1.6F);
        graphics.DrawRectangle(pen, 2, 2, 4, 4);
        graphics.DrawRectangle(pen, 10, 2, 4, 4);
        graphics.DrawRectangle(pen, 10, 10, 4, 4);
        graphics.DrawLine(pen, 6, 4, 10, 4);
        graphics.DrawLine(pen, 8, 4, 8, 12);
        graphics.DrawLine(pen, 8, 12, 10, 12);
    });

    public static Bitmap CreateQuestionIcon() => CreateBitmap(graphics =>
    {
        using var pen = new Pen(ClassicPalette.TealDark, 2F);
        graphics.DrawArc(pen, 3, 2, 9, 7, 180, 220);
        graphics.DrawLine(pen, 9, 8, 9, 10);
        graphics.FillEllipse(new SolidBrush(ClassicPalette.TealDark), 8, 12, 2, 2);
    });

    public static Bitmap CreateDoneIcon() => CreateBitmap(graphics =>
    {
        using var pen = new Pen(ClassicPalette.TealDark, 2F);
        graphics.DrawLine(pen, 2, 8, 6, 12);
        graphics.DrawLine(pen, 6, 12, 14, 3);
    });

    public static Bitmap CreateHelpIcon() => CreateBitmap(graphics =>
    {
        using var pen = new Pen(ClassicPalette.TealDark, 2F);
        graphics.DrawEllipse(pen, 2, 2, 11, 11);
        graphics.DrawLine(pen, 7, 5, 7, 9);
        graphics.FillEllipse(new SolidBrush(ClassicPalette.TealDark), 6, 11, 2, 2);
    });

    private static Bitmap CreateBitmap(Action<Graphics> painter)
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.FromArgb(224, 237, 235));
        using var borderPen = new Pen(ClassicPalette.Grid);
        graphics.DrawRectangle(borderPen, 0, 0, 15, 15);
        painter(graphics);
        return bitmap;
    }
}
