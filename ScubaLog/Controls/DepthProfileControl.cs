
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using ScubaLog.Core.Models;
using ScubaLog.Core.Units;

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

    public static readonly StyledProperty<UnitSystem> UnitSystemProperty =
        AvaloniaProperty.Register<DepthProfileControl, UnitSystem>(nameof(UnitSystem), UnitSystem.Metric);

    static DepthProfileControl()
    {
        AffectsRender<DepthProfileControl>(
            SamplesProperty,
            HoverTimeProperty,
            ShowRmvProperty,
            ShowTempProperty,
            ShowPpo2Property,
            ShowAirProperty,
            UnitSystemProperty);
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

    public UnitSystem UnitSystem
    {
        get => GetValue(UnitSystemProperty);
        set => SetValue(UnitSystemProperty, value);
    }

    private string DepthUnitLabel => UnitSystem == UnitSystem.Imperial ? "ft" : "m";

    private string TemperatureUnitLabel => UnitSystem == UnitSystem.Imperial ? "°F" : "°C";

    private string PressureUnitLabel => UnitSystem switch
    {
        UnitSystem.Imperial => "psi",
        UnitSystem.Canadian => "psi",
        _ => "bar"
    };

    private double DepthToDisplay(double meters) => UnitSystem == UnitSystem.Imperial ? meters * 3.28084 : meters;

    private double? TempToDisplay(double? celsius) => celsius is null
        ? null
        : UnitSystem == UnitSystem.Imperial
            ? (celsius.Value * 9.0 / 5.0) + 32.0
            : celsius.Value;

    // Canonical storage for sample TankPressure is bar. Convert for display.
    private double? PressureToDisplay(double? bar) => bar is null
        ? null
        : UnitSystem switch
        {
            UnitSystem.Imperial => bar.Value * 14.5037738,
            UnitSystem.Canadian => bar.Value * 14.5037738,
            _ => bar.Value
        };

    private static double ChooseTickIntervalMinutes(double totalMinutes)
    {
        if (totalMinutes <= 15) return 5;
        if (totalMinutes <= 45) return 10;
        if (totalMinutes <= 90) return 15;
        return 30;
    }

    private static double ChooseDepthInterval(double maxDepthMeters)
    {
        if (maxDepthMeters <= 15) return 5;
        if (maxDepthMeters <= 40) return 10;
        if (maxDepthMeters <= 70) return 15;
        return 20;
    }

    private static void DrawSeries(
        DrawingContext context,
        IList<DiveSample> samples,
        Func<DiveSample, double?> selector,
        IBrush brush,
        double maxTimeMinutes,
        Rect plotRect)
    {
        // collect only samples that actually have a value
        var withValues = samples
            .Select(s => new { Sample = s, Value = selector(s) })
            .Where(x => x.Value.HasValue)
            .ToList();

        if (withValues.Count < 2)
            return;

        var maxVal = withValues.Max(x => x.Value!.Value);
        var minVal = withValues.Min(x => x.Value!.Value);

        // protect against flatline
        if (Math.Abs(maxVal - minVal) < 1e-9)
            return;

        var pen = new Pen(brush, 1.5);

        var geometry = new StreamGeometry();
        using (var gctx = geometry.Open())
        {
            var first = true;
            foreach (var x in withValues)
            {
                var tNorm = maxTimeMinutes <= 0 ? 0 : x.Sample.Time.TotalMinutes / maxTimeMinutes;
                tNorm = Math.Clamp(tNorm, 0, 1);

                // scale series values into plotRect (top = max, bottom = min)
                var v = x.Value!.Value;
                var vNorm = (v - minVal) / (maxVal - minVal);
                var xPos = plotRect.Left + tNorm * plotRect.Width;
                var yPos = plotRect.Bottom - vNorm * plotRect.Height;

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
        var padding = new Thickness(50, 20, 20, 55);
        var plotRect = rect.Deflate(padding);

        // Background of plot area
        context.FillRectangle(Brushes.LightBlue, plotRect);

        var samples = Samples;
        if (samples is null || samples.Count == 0)
        {
            var layout = new TextLayout(
                text: "No profile data",
                typeface: Typeface.Default,
                fontSize: 14,
                foreground: Brushes.Gray);

            var pos = new Point(
                plotRect.Center.X - layout.Width / 2,
                plotRect.Center.Y - layout.Height / 2);

            layout.Draw(context, pos);
            return;
        }

        // Compute ranges
        var maxTimeMinutes = samples[^1].Time.TotalMinutes;
        if (maxTimeMinutes <= 0) maxTimeMinutes = 1;

        var maxDepthMeters = Math.Max(1.0, samples.Max(s => s.DepthMeters));

        // Axes
        var axisPen = new Pen(Brushes.Gray, 1);
        context.DrawLine(axisPen, new Point(plotRect.Left, plotRect.Top), new Point(plotRect.Left, plotRect.Bottom));
        context.DrawLine(axisPen, new Point(plotRect.Left, plotRect.Bottom), new Point(plotRect.Right, plotRect.Bottom));

        // ===== Time axis ticks, labels, and vertical grid lines =====
        var tickInterval = ChooseTickIntervalMinutes(maxTimeMinutes);
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 128, 128, 160)), 1);
        var tickPen = new Pen(Brushes.Gray, 1);

        for (double minute = 0; minute <= maxTimeMinutes + 0.01; minute += tickInterval)
        {
            var tNorm = minute / maxTimeMinutes;
            var x = plotRect.Left + tNorm * plotRect.Width;

            context.DrawLine(gridPen, new Point(x, plotRect.Top), new Point(x, plotRect.Bottom));

            var tickTop = plotRect.Bottom;
            var tickBottom = plotRect.Bottom + 4;
            context.DrawLine(tickPen, new Point(x, tickTop), new Point(x, tickBottom));

            var ts = TimeSpan.FromMinutes(minute);
            var labelText = ts.ToString(@"m\:ss");

            var timeLabel = new TextLayout(
                text: labelText,
                typeface: Typeface.Default,
                fontSize: 11,
                foreground: Brushes.SlateBlue);

            timeLabel.Draw(context, new Point(x - timeLabel.Width / 2, tickBottom + 2));
        }

        var axisTitle = new TextLayout(
            text: "Time (Minutes)",
            typeface: Typeface.Default,
            fontSize: 12,
            foreground: Brushes.SlateBlue);

        axisTitle.Draw(context, new Point(
            plotRect.Left + (plotRect.Width - axisTitle.Width) / 2,
            plotRect.Bottom + 24));

        // ===== Depth axis tick marks + horizontal grid lines (depth is canonical meters, labels converted) =====
        var depthIntervalMeters = ChooseDepthInterval(maxDepthMeters);
        var depthGridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 128, 128, 160)), 1);
        var depthTickPen = new Pen(Brushes.Gray, 1);

        for (double depthMeters = 0; depthMeters <= maxDepthMeters + 0.01; depthMeters += depthIntervalMeters)
        {
            var dNorm = depthMeters / maxDepthMeters;
            var y = plotRect.Top + dNorm * plotRect.Height;

            context.DrawLine(depthGridPen, new Point(plotRect.Left, y), new Point(plotRect.Right, y));

            var tickLeft = plotRect.Left - 4;
            context.DrawLine(depthTickPen, new Point(tickLeft, y), new Point(plotRect.Left, y));

            var depthDisplay = DepthToDisplay(depthMeters);
            var depthLabel = new TextLayout(
                text: $"{depthDisplay:0} {DepthUnitLabel}",
                typeface: Typeface.Default,
                fontSize: 11,
                foreground: Brushes.SlateBlue);

            depthLabel.Draw(context, new Point(plotRect.Left - depthLabel.Width - 8, y - depthLabel.Height / 2));
        }

        var depthTitle = new TextLayout(
            text: $"Depth ({DepthUnitLabel})",
            typeface: Typeface.Default,
            fontSize: 12,
            foreground: Brushes.SlateBlue);

        // Draw depth label vertically to avoid truncation
        var origin = new Point(plotRect.Left - 16, plotRect.Top + plotRect.Height / 2);
        var translate = new Point(origin.X - depthTitle.Width / 2, origin.Y - depthTitle.Height / 2);
        using (context.PushPostTransform(Matrix.CreateRotation(-Math.PI / 2, origin)))
        {
            depthTitle.Draw(context, translate);
        }

        // ===== Depth curve geometry (depth increases downward) =====
        var depthPen = new Pen(Brushes.DarkBlue, 2);
        var fillBrush = new SolidColorBrush(Color.FromArgb(80, 120, 160, 220));

        var geometry = new StreamGeometry();
        using (var gctx = geometry.Open())
        {
            var first = true;
            foreach (var s in samples)
            {
                var tNorm = s.Time.TotalMinutes / maxTimeMinutes;
                tNorm = Math.Clamp(tNorm, 0, 1);

                var dNorm = s.DepthMeters / maxDepthMeters;
                dNorm = Math.Clamp(dNorm, 0, 1);

                var x = plotRect.Left + tNorm * plotRect.Width;
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

            // Close to bottom axis for fill
            gctx.LineTo(new Point(plotRect.Right, plotRect.Bottom));
            gctx.LineTo(new Point(plotRect.Left, plotRect.Bottom));
            gctx.EndFigure(isClosed: true);
        }

        context.DrawGeometry(fillBrush, depthPen, geometry);

        // ===== Extra curves (MacDive-ish overlays) =====
        // These are normalized to their own min/max in the same plotRect.
        if (ShowRmv)
        {
            DrawSeries(context, samples, s => s.Rmv, Brushes.Orange, maxTimeMinutes, plotRect);
        }

        if (ShowTemp)
        {
            // Use canonical TemperatureC for storage, convert for display
            DrawSeries(context, samples, s => TempToDisplay(s.TemperatureC), Brushes.LimeGreen, maxTimeMinutes, plotRect);
        }

        if (ShowPpo2)
        {
            DrawSeries(context, samples, s => s.Ppo2, Brushes.MediumPurple, maxTimeMinutes, plotRect);
        }

        if (ShowAir)
        {
            // Use canonical TankPressureBar for storage, convert for display
            DrawSeries(context, samples, s => PressureToDisplay(s.TankPressureBar), Brushes.Red, maxTimeMinutes, plotRect);
        }

        // ===== Hover / scrubber line =====
        if (HoverTime is { } hover)
        {
            var hoverMinutes = Math.Clamp(hover.TotalMinutes, 0, maxTimeMinutes);
            var tNormHover = hoverMinutes / maxTimeMinutes;
            var xHover = plotRect.Left + tNormHover * plotRect.Width;

            var hoverPen = new Pen(Brushes.White, 1);
            context.DrawLine(hoverPen, new Point(xHover, plotRect.Top), new Point(xHover, plotRect.Bottom));
        }

        // Small legend (optional but helps validate units quickly)
        var legend = new TextLayout(
            text: $"Temp: {TemperatureUnitLabel}   Pressure: {PressureUnitLabel}",
            typeface: Typeface.Default,
            fontSize: 11,
            foreground: Brushes.Gray);

        legend.Draw(context, new Point(plotRect.Right - legend.Width, plotRect.Top - legend.Height - 2));
    }
}
