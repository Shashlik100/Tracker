using System.Drawing.Drawing2D;

namespace TrackerApp;

public sealed class StatusViewControl : UserControl
{
    private readonly Label _totalItemsValue = new();
    private readonly Label _dueTodayValue = new();
    private readonly Label _completedTodayValue = new();
    private readonly Label _retentionValue = new();
    private readonly Label _masteredValue = new();
    private readonly ClassicChartControl _itemsChart = new("כמות פריטים לפי ספריה", ClassicChartKind.Bar);
    private readonly ClassicChartControl _dueChart = new("לביצוע לפי ספריה", ClassicChartKind.Bar);
    private readonly ClassicChartControl _masteredChart = new("נלמד היטב לפי ספריה", ClassicChartKind.Bar);
    private readonly ClassicChartControl _timelineChart = new("מגמת חזרות", ClassicChartKind.Line);
    private readonly HeatmapControl _heatmap = new();

    public StatusViewControl()
    {
        BackColor = ClassicPalette.PanelBackground;
        RightToLeft = RightToLeft.Yes;
        BuildLayout();
        UiLayoutHelper.ApplyRecursive(this);
    }

    public void Bind(DashboardStats stats)
    {
        _totalItemsValue.Text = stats.TotalItems.ToString();
        _dueTodayValue.Text = stats.DueToday.ToString();
        _completedTodayValue.Text = stats.CompletedToday.ToString();
        _retentionValue.Text = $"{stats.RetentionRate:0.#}%";
        _masteredValue.Text = stats.MasteredItems.ToString();
        _itemsChart.SetBarData(stats.ItemsBySubject.Select(TranslateCategory).ToList());
        _dueChart.SetBarData(stats.DueBySubject.Select(TranslateCategory).ToList());
        _masteredChart.SetBarData(stats.MasteredByCategory.Select(TranslateCategory).ToList());
        _timelineChart.SetLineData(stats.ReviewTimeline);
        _heatmap.SetData(stats.Heatmap);
    }

    private void BuildLayout()
    {
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(8)
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 122F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 178F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));

        var summaryPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(4)
        };

        for (var index = 0; index < 5; index++)
        {
            summaryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        }

        summaryPanel.Controls.Add(CreateSummaryBox("סה\"כ", _totalItemsValue), 0, 0);
        summaryPanel.Controls.Add(CreateSummaryBox("לביצוע היום", _dueTodayValue), 1, 0);
        summaryPanel.Controls.Add(CreateSummaryBox("חזרות היום", _completedTodayValue), 2, 0);
        summaryPanel.Controls.Add(CreateSummaryBox("שימור", _retentionValue), 3, 0);
        summaryPanel.Controls.Add(CreateSummaryBox("נלמד היטב", _masteredValue), 4, 0);

        rootLayout.Controls.Add(summaryPanel, 0, 0);
        rootLayout.SetColumnSpan(summaryPanel, 2);
        rootLayout.Controls.Add(_heatmap, 0, 1);
        rootLayout.SetColumnSpan(_heatmap, 2);
        rootLayout.Controls.Add(_itemsChart, 0, 2);
        rootLayout.Controls.Add(_dueChart, 1, 2);
        rootLayout.Controls.Add(_masteredChart, 0, 3);
        rootLayout.Controls.Add(_timelineChart, 1, 3);

        Controls.Add(rootLayout);
    }

    private static GroupBox CreateSummaryBox(string title, Label valueLabel)
    {
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Font = new Font("Microsoft Sans Serif", 14F, FontStyle.Bold);
        valueLabel.TextAlign = ContentAlignment.MiddleCenter;
        valueLabel.RightToLeft = RightToLeft.Yes;

        var box = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            BackColor = ClassicPalette.CardBackground
        };
        box.Controls.Add(valueLabel);
        return box;
    }

    private static SubjectCountModel TranslateCategory(SubjectCountModel model)
    {
        return new SubjectCountModel
        {
            Name = model.Name switch
            {
                "Talmud" => "תלמוד",
                "Mishnah" => "משנה",
                "Shulchan Aruch" => "שולחן ערוך",
                "Rashi" => "רש\"י",
                "Book of Isaiah" => "ספר ישעיהו",
                _ => model.Name
            },
            Count = model.Count
        };
    }
}

internal enum ClassicChartKind
{
    Bar,
    Line
}

internal sealed class ClassicChartControl : Control
{
    private readonly ClassicChartKind _kind;
    private IReadOnlyList<SubjectCountModel> _barData = Array.Empty<SubjectCountModel>();
    private IReadOnlyList<ReviewTimelinePoint> _lineData = Array.Empty<ReviewTimelinePoint>();

    public ClassicChartControl(string title, ClassicChartKind kind)
    {
        _kind = kind;
        Title = title;
        BackColor = ClassicPalette.CardBackground;
        DoubleBuffered = true;
        Margin = new Padding(6);
    }

    public string Title { get; }

    public void SetBarData(IReadOnlyList<SubjectCountModel> data)
    {
        _barData = data;
        Invalidate();
    }

    public void SetLineData(IReadOnlyList<ReviewTimelinePoint> data)
    {
        _lineData = data;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        graphics.Clear(BackColor);

        using var borderPen = new Pen(ClassicPalette.Grid);
        graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        var titleBounds = new Rectangle(0, 0, Width, 28);
        TextRenderer.DrawText(graphics, Title, Font, titleBounds, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        var chartBounds = new Rectangle(46, 34, Math.Max(80, Width - 62), Math.Max(70, Height - 72));
        DrawGrid(graphics, chartBounds);

        if (_kind == ClassicChartKind.Bar)
        {
            DrawBars(graphics, chartBounds);
            return;
        }

        DrawLine(graphics, chartBounds);
    }

    private void DrawGrid(Graphics graphics, Rectangle chartBounds)
    {
        using var gridPen = new Pen(ClassicPalette.Grid) { DashStyle = DashStyle.Dot };
        using var axisPen = new Pen(ClassicPalette.TealDark, 1.4F);

        for (var index = 0; index <= 4; index++)
        {
            var y = chartBounds.Top + (chartBounds.Height * index / 4);
            graphics.DrawLine(gridPen, chartBounds.Left, y, chartBounds.Right, y);
        }

        graphics.DrawLine(axisPen, chartBounds.Left, chartBounds.Bottom, chartBounds.Right, chartBounds.Bottom);
        graphics.DrawLine(axisPen, chartBounds.Left, chartBounds.Top, chartBounds.Left, chartBounds.Bottom);
    }

    private void DrawBars(Graphics graphics, Rectangle chartBounds)
    {
        if (_barData.Count == 0)
        {
            DrawEmptyState(graphics, chartBounds);
            return;
        }

        var maxValue = Math.Max(1, _barData.Max(point => point.Count));
        var slotWidth = chartBounds.Width / Math.Max(1, _barData.Count);
        var barWidth = Math.Max(18, slotWidth - 16);
        using var brush = new SolidBrush(ClassicPalette.Teal);

        for (var index = 0; index < _barData.Count; index++)
        {
            var point = _barData[index];
            var ratio = point.Count / (double)maxValue;
            var height = Math.Max(4, (int)((chartBounds.Height - 26) * ratio));
            var x = chartBounds.Left + (slotWidth * index) + Math.Max(4, (slotWidth - barWidth) / 2);
            var y = chartBounds.Bottom - height;

            graphics.FillRectangle(brush, x, y, barWidth, height);
            graphics.DrawRectangle(Pens.Black, x, y, barWidth, height);

            var countRect = new Rectangle(x - 6, y - 18, barWidth + 12, 16);
            TextRenderer.DrawText(graphics, point.Count.ToString(), Font, countRect, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            var labelRect = new Rectangle(x - 8, chartBounds.Bottom + 4, barWidth + 16, 34);
            TextRenderer.DrawText(graphics, Shorten(point.Name, 14), Font, labelRect, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
        }
    }

    private void DrawLine(Graphics graphics, Rectangle chartBounds)
    {
        if (_lineData.Count == 0)
        {
            DrawEmptyState(graphics, chartBounds);
            return;
        }

        var maxValue = Math.Max(1D, Math.Ceiling(_lineData.Max(point => point.AverageScore)));
        using var linePen = new Pen(ClassicPalette.TealDark, 2F);
        using var pointBrush = new SolidBrush(ClassicPalette.Teal);

        PointF? previousPoint = null;
        for (var index = 0; index < _lineData.Count; index++)
        {
            var point = _lineData[index];
            var x = chartBounds.Left + (_lineData.Count == 1 ? chartBounds.Width / 2F : chartBounds.Width * index / (float)(_lineData.Count - 1));
            var y = chartBounds.Bottom - (float)((chartBounds.Height - 20) * (point.AverageScore / maxValue));
            var currentPoint = new PointF(x, y);

            if (previousPoint.HasValue)
            {
                graphics.DrawLine(linePen, previousPoint.Value, currentPoint);
            }

            graphics.FillEllipse(pointBrush, x - 4, y - 4, 8, 8);
            previousPoint = currentPoint;
        }
    }

    private void DrawEmptyState(Graphics graphics, Rectangle chartBounds)
    {
        TextRenderer.DrawText(
            graphics,
            "אין נתונים להצגה",
            Font,
            chartBounds,
            ClassicPalette.EmptyChartText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static string Shorten(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..(maxLength - 1)]}...";
    }
}

internal sealed class HeatmapControl : Control
{
    private IReadOnlyList<HeatmapDayModel> _data = Array.Empty<HeatmapDayModel>();

    public HeatmapControl()
    {
        BackColor = ClassicPalette.CardBackground;
        DoubleBuffered = true;
        Margin = new Padding(6);
    }

    public void SetData(IReadOnlyList<HeatmapDayModel> data)
    {
        _data = data;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        using var borderPen = new Pen(ClassicPalette.Grid);
        graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(graphics, "מפת חום של חזרות", Font, new Rectangle(0, 0, Width, 24), Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        if (_data.Count == 0)
        {
            TextRenderer.DrawText(graphics, "אין עדיין פעילות חזרות", Font, new Rectangle(0, 24, Width, Height - 24), ClassicPalette.EmptyChartText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var maxCount = Math.Max(1, _data.Max(point => point.ReviewCount));
        var cellSize = 14;
        var gap = 3;
        var startX = 18;
        var startY = 36;

        for (var index = 0; index < _data.Count; index++)
        {
            var item = _data[index];
            var column = index / 7;
            var row = index % 7;
            var x = startX + column * (cellSize + gap);
            var y = startY + row * (cellSize + gap);
            var color = GetHeatColor(item.ReviewCount, maxCount);
            using var brush = new SolidBrush(color);
            graphics.FillRectangle(brush, x, y, cellSize, cellSize);
            graphics.DrawRectangle(Pens.Gray, x, y, cellSize, cellSize);
        }
    }

    private static Color GetHeatColor(int count, int maxCount)
    {
        if (count <= 0)
        {
            return Color.FromArgb(230, 236, 235);
        }

        var ratio = count / (double)maxCount;
        if (ratio < 0.34)
        {
            return Color.FromArgb(183, 220, 213);
        }

        if (ratio < 0.67)
        {
            return Color.FromArgb(101, 182, 171);
        }

        return Color.FromArgb(0, 116, 116);
    }
}
