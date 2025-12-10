using System;
using System.Collections.Generic;
using System.Linq; // <-- needed for Select/Where/Max
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using ScubaLog.Core.Models;

namespace ScubaLog.Controls;

public class DepthProfileControl : Control
{
    public static readonly StyledProperty<IList<DiveSample>?> SamplesProperty =
        AvaloniaProperty.Register<DepthProfileControl, IList<DiveSample>?>(nameof(Samples));

    public static readonly StyledProperty<TimeSpan?> HoverTimeProperty =
        AvaloniaProperty.Register<DepthProfileControl, TimeSpan?>(nameof(HoverTime));

    public static readonly StyledProperty<bool> ShowRmvProperty =
        AvaloniaProperty.Register<DepthProfileControl, bool>(nameof(ShowRmv), true);

    public static readonly StyledProperty<bool> ShowTempProperty =
        AvaloniaProperty.Register<DepthProfileControl, bool>(nameof(ShowTemp), true);

    public static readonly StyledProperty<bool> ShowPpo2Property =
        AvaloniaProperty.Register<DepthProfileControl, bool>(nameof(ShowPpo2), true);

    public static readonly StyledProperty<bool> ShowAirProperty =
        AvaloniaProperty.Register<DepthProfileControl, bool>(nameof(ShowAir), true);

    static DepthProfileControl()
    {
        AffectsRender<DepthProfileControl>(
            SamplesProperty,
            HoverTimeProperty,
            ShowRmvProperty,
            ShowTempProperty,
            ShowPpo2Property,
            ShowAirProperty);
    }

    public IList<DiveSample>? Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    public TimeSpan? HoverTime
    {
        get => GetValue(HoverTimeProperty);
        set => SetValue(HoverTimeProperty, value);
    }

    public bool ShowRmv
    {
        get => GetValue(ShowRmvProperty);
        set => SetValue(ShowRmvProperty, value);
    }

    public bool ShowTemp
    {
        get => GetValue(ShowTempProperty);
        set => SetValue(ShowTempProperty, value);
    }

    public bool ShowPpo2
    {
        get => GetValue(ShowPpo2Property);
        set => SetValue(ShowPpo2Property, value);
    }

    public bool ShowAir
    {
        get => GetValue(ShowAirProperty);
        set => SetValue(ShowAirProperty, value);
    }

    private static double ChooseTickIntervalMinutes(double totalMinutes)
    {
        if (totalMinutes <= 15) return 5;
        if (totalMinutes <= 45) return 10;
        if (totalMinutes <= 90) return 15;
        return 30;
    }

    private static double ChooseDepthInterval(double maxDepth)
    {
        if (maxDepth <= 15) return 5;
        if (maxDepth <= 40) return 10;
        if (maxDepth <= 70) return 15;
        return 20;
    }

    private void DrawSeries(
        DrawingContext context,
        IList<DiveSample> samples,
        Func<DiveSample, double?> selector,
        IBrush brush,
        double maxTime,
        Rect plotRect)
    {
        // collect only samples that actually have a value
        var withValues = samples
            .Select(s => new { Sample = s, Value = selector(s) })
            .Where(x => x.Value.HasValue)
            .ToList();

        if (withValues.Count < 2)
            return;

        // scale that series into the same Y-space as depth (0 at top, max at bottom)
        var maxVal = withValues.Max(x => x.Value!.Value);
        if (maxVal <= 0)
            return;

        var pen = new Pen(brush, 1.5);

        var geometry = new StreamGeometry();
        using (var gctx = geometry.Open())
        {
            var first = true;
            foreach (var x in withValues)
            {
                var tNorm = x.Sample.Time.TotalMinutes / maxTime;
                var vNorm = x.Value!.Value / maxVal;

                var xPos = plotRect.Left + tNorm * plotRect.Width;
                var yPos = plotRect.Top + vNorm * plotRect.Height;

                if (first)
                {
                    gctx.BeginFigure(new Point(xPos, yPos), isFilled: false);
                    first = false;
                }
                else
                {
                    gctx.LineTo(new Point(xPos, yPos));
                }
            }

            gctx.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var rect = new Rect(Bounds.Size);
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        // extra bottom padding so time labels + axis title fit
        var padding = new Thickness(40, 20, 20, 50); // left, top, right, bottom
        var plotRect = rect.Deflate(padding);

        // Background of plot area
        context.FillRectangle(Brushes.LightBlue, plotRect);

        var samples = Samples;
        if (samples is null || samples.Count == 0)
        {
            // Centered "No profile data" message using TextLayout
            var layout = new TextLayout(
                text: "No profile data",
                typeface: Typeface.Default,
                fontSize: 14,
                foreground: Brushes.Gray
            );

            var pos = new Point(
                plotRect.Center.X - layout.Width / 2,
                plotRect.Center.Y - layout.Height / 2
            );

            layout.Draw(context, pos);
            return;
        }

        // Compute ranges
        var maxTime = samples[^1].Time.TotalMinutes;
        if (maxTime <= 0) maxTime = 1;

        var maxDepth = 0.0;
        foreach (var s in samples)
        {
            if (s.DepthMeters > maxDepth)
                maxDepth = s.DepthMeters;
        }
        if (maxDepth <= 0) maxDepth = 1;

        // Axes
        var axisPen = new Pen(Brushes.Gray, 1);

        // Y axis (depth)
        context.DrawLine(axisPen,
            new Point(plotRect.Left, plotRect.Top),
            new Point(plotRect.Left, plotRect.Bottom));

        // X axis (time)
        context.DrawLine(axisPen,
            new Point(plotRect.Left, plotRect.Bottom),
            new Point(plotRect.Right, plotRect.Bottom));

        // ===== Time axis ticks, labels, and vertical grid lines =====
        var tickInterval = ChooseTickIntervalMinutes(maxTime);
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 128, 128, 160)), 1);
        var tickPen = new Pen(Brushes.Gray, 1);

        for (double minute = 0; minute <= maxTime + 0.01; minute += tickInterval)
        {
            var tNorm = minute / maxTime; // 0..1 along X
            var x = plotRect.Left + tNorm * plotRect.Width;

            // vertical grid line across plot area
            context.DrawLine(gridPen,
                new Point(x, plotRect.Top),
                new Point(x, plotRect.Bottom));

            // tick mark on bottom axis
            var tickTop = plotRect.Bottom;
            var tickBottom = plotRect.Bottom + 4;
            context.DrawLine(tickPen,
                new Point(x, tickTop),
                new Point(x, tickBottom));

            // time label (e.g. 0:00, 10:00, 20:00)
            var ts = TimeSpan.FromMinutes(minute);
            var labelText = ts.ToString(@"m\:ss");

            var timeLabel = new TextLayout(
                text: labelText,
                typeface: Typeface.Default,
                fontSize: 11,
                foreground: Brushes.SlateBlue
            );

            var labelPos = new Point(
                x - timeLabel.Width / 2,
                tickBottom + 2
            );

            timeLabel.Draw(context, labelPos);
        }

        // "Time (Minutes)" centered under the axis
        var axisTitle = new TextLayout(
            text: "Time (Minutes)",
            typeface: Typeface.Default,
            fontSize: 12,
            foreground: Brushes.SlateBlue
        );

        var axisTitlePos = new Point(
            plotRect.Left + (plotRect.Width - axisTitle.Width) / 2,
            plotRect.Bottom + 22
        );

        axisTitle.Draw(context, axisTitlePos);

        // ===== Depth axis tick marks + horizontal grid lines =====
        double depthInterval = ChooseDepthInterval(maxDepth);

        var depthGridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 128, 128, 160)), 1);
        var depthTickPen = new Pen(Brushes.Gray, 1);

        for (double depth = 0; depth <= maxDepth + 0.01; depth += depthInterval)
        {
            double dNorm = depth / maxDepth;
            double y = plotRect.Top + dNorm * plotRect.Height;

            // Horizontal grid line across the plot
            context.DrawLine(depthGridPen,
                new Point(plotRect.Left, y),
                new Point(plotRect.Right, y));

            // Tick mark on the left axis
            double tickLeft = plotRect.Left - 4;
            double tickRight = plotRect.Left;
            context.DrawLine(depthTickPen,
                new Point(tickLeft, y),
                new Point(tickRight, y));

            // Label (e.g., "30 m")
            var depthLabel = new TextLayout(
                text: $"{depth:0} m",
                typeface: Typeface.Default,
                fontSize: 11,
                foreground: Brushes.SlateBlue
            );

            var labelPos = new Point(
                plotRect.Left - depthLabel.Width - 8,
                y - depthLabel.Height / 2
            );

            depthLabel.Draw(context, labelPos);
        }

        // Axis title: "Depth (Meters)"
        var depthTitle = new TextLayout(
            text: "Depth (Meters)",
            typeface: Typeface.Default,
            fontSize: 12,
            foreground: Brushes.SlateBlue
        );

        // Center title vertically along left axis
        var depthTitlePos = new Point(
            plotRect.Left - depthTitle.Width - 20,
            plotRect.Top + (plotRect.Height - depthTitle.Height) / 2
        );

        depthTitle.Draw(context, depthTitlePos);

        // ===== Depth curve geometry (filled area) =====
        var depthPen = new Pen(Brushes.DarkBlue, 2);
        var fillBrush = new SolidColorBrush(Color.FromArgb(80, 120, 160, 220));

        var geometry = new StreamGeometry();
        using (var gctx = geometry.Open())
        {
            var first = true;

            foreach (var s in samples)
            {
                var tNorm = s.Time.TotalMinutes / maxTime;   // 0..1 along X
                var dNorm = s.DepthMeters / maxDepth;        // 0..1 along Y

                var x = plotRect.Left + tNorm * plotRect.Width;
                // depth increases downward
                var y = plotRect.Top + dNorm * plotRect.Height;

                if (first)
                {
                    gctx.BeginFigure(new Point(x, y), isFilled: true);
                    first = false;
                }
                else
                {
                    gctx.LineTo(new Point(x, y));
                }
            }

            // Close the shape to bottom axis for fill
            gctx.LineTo(new Point(plotRect.Right, plotRect.Bottom));
            gctx.LineTo(new Point(plotRect.Left, plotRect.Bottom));
            gctx.EndFigure(isClosed: true);
        }

        context.DrawGeometry(fillBrush, depthPen, geometry);

        // ===== Extra series (RMV, Temp, PPO2, Air) overlaid =====
        if (ShowRmv)
        {
            DrawSeries(
                context,
                samples,
                s => s.Rmv,              // adjust property name if needed
                brush: Brushes.Goldenrod,
                maxTime,
                plotRect);
        }

        if (ShowTemp)
        {
            // DrawSeries(
            //     context,
            //     samples,
            //     s => s.,            // adjust property name if needed
            //     brush: Brushes.OrangeRed,
            //     maxTime,
            //     plotRect);
        }

        if (ShowPpo2)
        {
            DrawSeries(
                context,
                samples,
                s => s.Ppo2,             // adjust property name if needed
                brush: Brushes.MediumPurple,
                maxTime,
                plotRect);
        }

        if (ShowAir)
        {
            DrawSeries(
                context,
                samples,
                s => s.TankPressurePsi,  // adjust property name if needed
                brush: Brushes.LightGreen,
                maxTime,
                plotRect);
        }

        // ===== Hover / scrubber line (drawn on top of everything) =====
        if (HoverTime is { } hover)
        {
            // clamp to [0, maxTime]
            var hoverMinutes = hover.TotalMinutes;
            if (hoverMinutes < 0) hoverMinutes = 0;
            if (hoverMinutes > maxTime) hoverMinutes = maxTime;

            var tNormHover = maxTime <= 0 ? 0 : hoverMinutes / maxTime;
            var xHover = plotRect.Left + tNormHover * plotRect.Width;

            var hoverPen = new Pen(Brushes.White, 1); // thin vertical line like MacDive
            context.DrawLine(hoverPen,
                new Point(xHover, plotRect.Top),
                new Point(xHover, plotRect.Bottom));
        }

        // ===== Depth labels (0 m at top, max at bottom) =====
        var label0 = new TextLayout(
            text: "0 m",
            typeface: Typeface.Default,
            fontSize: 12,
            foreground: Brushes.Gray
        );

        var labelMax = new TextLayout(
            text: $"{maxDepth:F0} m",
            typeface: Typeface.Default,
            fontSize: 12,
            foreground: Brushes.Gray
        );

        // 0 m near top of axis
        var label0Pos = new Point(
            plotRect.Left - label0.Width - 4,
            plotRect.Top - label0.Height / 2
        );
        label0.Draw(context, label0Pos);

        // max depth near bottom of axis
        var labelMaxPos = new Point(
            plotRect.Left - labelMax.Width - 4,
            plotRect.Bottom - labelMax.Height / 2
        );
        labelMax.Draw(context, labelMaxPos);
    }
}