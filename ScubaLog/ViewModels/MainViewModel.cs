using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using ScubaLog.Core.Models;
using ScubaLog.Core.Services;
using ScubaLog.Core.Units;

namespace ScubaLog.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DiveLogService _service;
    
    public UnitPreferences Units { get; } = new();

    public ObservableCollection<Dive> Dives { get; }

    // Bind this to the settings ComboBox ItemsSource
    public IReadOnlyList<UnitSystem> UnitSystems { get; } =
        Enum.GetValues<UnitSystem>();

    public int TotalDives => Dives.Count;

    public string TotalBottomTimeDisplay
    {
        get
        {
            var totalSeconds = Dives.Sum(d => d.Duration.TotalSeconds);
            var ts           = TimeSpan.FromSeconds(totalSeconds);

            var parts = new List<string>();
            if (ts.Days > 0) parts.Add($"{ts.Days}d");
            if (ts.Hours > 0 || ts.Days > 0) parts.Add($"{ts.Hours}h");
            parts.Add($"{ts.Minutes}m");

            return string.Join(" ", parts);
        }
    }

    [ObservableProperty]
    private UnitSystem selectedUnitSystem = UnitSystem.Metric;
    
    [ObservableProperty]
    private Dive? selectedDive;

    [ObservableProperty]
    private DiveSample? hoverSample;

    
    // Which extra curves are visible on the graph
    [ObservableProperty] private bool showRmv  = true;
    [ObservableProperty] private bool showTemp = true;
    [ObservableProperty] private bool showPpo2 = true;
    [ObservableProperty] private bool showAir  = true;

    // ----------------- Unit-aware display helpers -----------------

    // Unit labels you can bind to (for headers/axis labels)
    public string DepthUnitLabel => SelectedUnitSystem switch
    {
        UnitSystem.Imperial => "ft",
        // Common convention: Canada often uses meters for depth but PSI for pressure
        UnitSystem.Canadian => "m",
        _ => "m"
    };

    public string TemperatureUnitLabel => SelectedUnitSystem switch
    {
        UnitSystem.Imperial => "°F",
        _ => "°C"
    };

    public string PressureUnitLabel => SelectedUnitSystem switch
    {
        UnitSystem.Imperial => "psi",
        UnitSystem.Canadian => "psi",
        _ => "bar"
    };

    private double ToDisplayDepth(double meters) => SelectedUnitSystem switch
    {
        UnitSystem.Imperial => meters * 3.28084,
        _ => meters
    };

    private double? ToDisplayTemp(double? celsius) => celsius is null
        ? null
        : SelectedUnitSystem == UnitSystem.Imperial
            ? (celsius.Value * 9.0 / 5.0) + 32.0
            : celsius.Value;

    private double? ToDisplayPressure(double? bar) => bar is null
        ? null
        : SelectedUnitSystem switch
        {
            UnitSystem.Imperial => bar.Value * 14.5037738,
            UnitSystem.Canadian => bar.Value * 14.5037738,
            _ => bar.Value
        };

    // ----------------- Formatted strings for UI -----------------

    public string SelectedDiveMaxDepthDisplay => SelectedDive is null
        ? string.Empty
        : $"{ToDisplayDepth(SelectedDive.MaxDepthMeters):0.0} {DepthUnitLabel}";

    public string SelectedDiveAvgDepthDisplay => SelectedDive is null
        ? string.Empty
        : $"{ToDisplayDepth(SelectedDive.AvgDepthMeters):0.0} {DepthUnitLabel}";

    public string? SelectedDiveAirTempDisplay
    {
        get
        {
            if (SelectedDive?.AirTempC is null) return null;
            var t = ToDisplayTemp(SelectedDive.AirTempC);
            return t is null ? null : $"{t.Value:0.#} {TemperatureUnitLabel}";
        }
    }

    public string? SelectedDiveWaterTempRangeDisplay
    {
        get
        {
            if (SelectedDive is null) return null;
            if (SelectedDive.WaterTempLowC is null && SelectedDive.WaterTempHighC is null) return null;

            var low = ToDisplayTemp(SelectedDive.WaterTempLowC);
            var high = ToDisplayTemp(SelectedDive.WaterTempHighC);

            if (low is not null && high is not null)
                return $"{low.Value:0.#}–{high.Value:0.#} {TemperatureUnitLabel}";

            var one = low ?? high;
            return one is null ? null : $"{one.Value:0.#} {TemperatureUnitLabel}";
        }
    }

    public string HoverDepthDisplay => HoverSample is null
        ? string.Empty
        : $"{ToDisplayDepth(HoverSample.DepthMeters):0.0} {DepthUnitLabel}";

    public string? HoverTempDisplay
    {
        get
        {
            if (HoverSample is null) return null;

            var t = ToDisplayTemp(HoverSample.TemperatureC);
            return t is null ? null : $"{t.Value:0.#} {TemperatureUnitLabel}";
        }
    }

    public string? HoverPressureDisplay
    {
        get
        {
            if (HoverSample is null) return null;

            // Canonical storage: bar. Convert for display as needed.
            var p = ToDisplayPressure(HoverSample.TankPressureBar);
            return p is null ? null : $"{p.Value:0} {PressureUnitLabel}";
        }
    }

    public string HoverTimeDisplay => HoverSample is null
        ? "--"
        : $"{HoverSample.Time:mm\\:ss}";

    public string HoverRmvDisplay
    {
        get
        {
            if (HoverSample?.RmvLitersPerMinute is null) return "--";

            var lpm = HoverSample.RmvLitersPerMinute.Value;

            if (SelectedUnitSystem == UnitSystem.Imperial)
            {
                // L/min -> ft^3/min
                var cfm = lpm / 28.3168466;
                return $"{cfm:0.00} CF/Min";
            }

            return $"{lpm:0.0} L/Min";
        }
    }

    public string HoverSacDisplay
    {
        get
        {
            // If you don’t have a SAC field on DiveSample yet, leave this as "--".
            // Wire it once the model exposes a canonical value.
            if (HoverSample is null) return "--";

            // Common patterns in dive logs: SacPsiPerMinute (imperial) or SacBarPerMinute (metric)
            // Try to use whichever exists in your model.

            // NOTE: If your DiveSample does not have these properties, change this implementation
            // to return "--" and we’ll wire it after we confirm the field name.

            double? psiPerMin = null;
            double? barPerMin = null;

            // These property names are intentionally conservative; adjust if your model differs.
            // (Keeping this code explicit avoids runtime reflection and keeps bindings fast.)
            psiPerMin = HoverSample.SacPsiPerMinute;
            barPerMin = HoverSample.SacBarPerMinute;

            if (SelectedUnitSystem == UnitSystem.Metric || SelectedUnitSystem == UnitSystem.Canadian)
            {
                if (barPerMin is not null)
                    return $"{barPerMin.Value:0.00} bar/Min";

                if (psiPerMin is not null)
                    return $"{(psiPerMin.Value / 14.5037738):0.00} bar/Min";

                return "--";
            }

            // Imperial
            if (psiPerMin is not null)
                return $"{psiPerMin.Value:0.00} PSI/Min";

            if (barPerMin is not null)
                return $"{(barPerMin.Value * 14.5037738):0.00} PSI/Min";

            return "--";
        }
    }

    public string HoverPpo2Display => HoverSample?.Ppo2 is null
        ? "--"
        : $"{HoverSample.Ppo2.Value:0.00}";

    public string HoverNdtDisplay => FormatNdtOrNdl("--");

    public string HoverDecoDisplay
    {
        get
        {
            // If your model provides stop depth + stop minutes, format that here.
            if (HoverSample is null) return "--";

            if (HoverSample.DecoStopMinutes is null || HoverSample.DecoStopDepthMeters is null)
                return "--";

            var depth = ToDisplayDepth(HoverSample.DecoStopDepthMeters.Value);
            var unit = DepthUnitLabel;
            var mins = HoverSample.DecoStopMinutes.Value;

            return $"{depth:0.#} {unit}, {mins:0} Mins";
        }
    }

    public string HoverTtsDisplay => HoverSample?.TtsMinutes is null
        ? "--"
        : $"{HoverSample.TtsMinutes.Value:0} Mins";

    public string HoverAscentDisplay
    {
        get
        {
            if (HoverSample is null || SelectedDive?.Samples is null) return "--";

            // Prefer explicit ascent rate if provided by the importer
            if (HoverSample.AscentRateMps is double rate)
            {
                if (SelectedUnitSystem == UnitSystem.Imperial)
                    return $"{rate * 3.28084:0.00} f/s";

                return $"{rate:0.00} m/s";
            }

            var samples = SelectedDive.Samples;
            var i = samples.IndexOf(HoverSample);
            if (i <= 0) return "--";

            var a = samples[i - 1];
            var b = samples[i];

            var dz = b.DepthMeters - a.DepthMeters; // meters
            var dt = (b.Time - a.Time).TotalSeconds;
            if (dt <= 0.0001) return "--";

            // Positive means ascending (getting shallower)
            var ascentMps = (-dz) / dt;

            if (SelectedUnitSystem == UnitSystem.Imperial)
                return $"{ascentMps * 3.28084:0.00} f/s";

            return $"{ascentMps:0.00} m/s";
        }
    }

    public string HoverGasDisplay => string.IsNullOrWhiteSpace(HoverSample?.Gas)
        ? "--"
        : HoverSample!.Gas;

    private void RaiseUnitDependentProperties()
    {
        OnPropertyChanged(nameof(DepthUnitLabel));
        OnPropertyChanged(nameof(TemperatureUnitLabel));
        OnPropertyChanged(nameof(PressureUnitLabel));

        OnPropertyChanged(nameof(SelectedDiveMaxDepthDisplay));
        OnPropertyChanged(nameof(SelectedDiveAvgDepthDisplay));
        OnPropertyChanged(nameof(SelectedDiveAirTempDisplay));
        OnPropertyChanged(nameof(SelectedDiveWaterTempRangeDisplay));

        OnPropertyChanged(nameof(HoverDepthDisplay));
        OnPropertyChanged(nameof(HoverTempDisplay));
        OnPropertyChanged(nameof(HoverPressureDisplay));

        OnPropertyChanged(nameof(HoverTimeDisplay));
        OnPropertyChanged(nameof(HoverRmvDisplay));
        OnPropertyChanged(nameof(HoverSacDisplay));
        OnPropertyChanged(nameof(HoverPpo2Display));
        OnPropertyChanged(nameof(HoverNdtDisplay));
        OnPropertyChanged(nameof(HoverDecoDisplay));
        OnPropertyChanged(nameof(HoverTtsDisplay));
        OnPropertyChanged(nameof(HoverAscentDisplay));
        OnPropertyChanged(nameof(HoverGasDisplay));
    }

    partial void OnSelectedUnitSystemChanged(UnitSystem value)
    {
        // If UnitPreferences supports persistence later, hook it up here.
        // For now, just refresh all unit-dependent UI text.
        RaiseUnitDependentProperties();
    }

    public MainViewModel(DiveLogService service)
    {
        _service = service;

        // Ensure UnitPreferences starts in sync with the current selection
        // Units.System = SelectedUnitSystem;  // Removed per instructions

        Dives = new ObservableCollection<Dive>(_service.Dives);
        Dives.CollectionChanged += (_, _) => RefreshAggregates();

        // Default selection
        SelectedDive = Dives.FirstOrDefault();

        RefreshAggregates();
    }

    // Optional: design-time / fallback ctor that still uses demo data
    public MainViewModel() : this(new DiveLogService())
    {
    }

    public void ReplaceDives(IEnumerable<Dive> dives)
    {
        Dives.Clear();
        foreach (var d in dives)
            Dives.Add(d);
        SelectedDive = Dives.FirstOrDefault();
        RefreshAggregates();
    }

    public void ImportUddfFromPath(string filePath)
    {
        _service.ImportUddfFromPath(filePath);
        ReloadDivesFromService();
    }

    private void ReloadDivesFromService()
    {
        Dives.Clear();
        foreach (var dive in _service.Dives)
            Dives.Add(dive);

        SelectedDive = Dives.FirstOrDefault();

        RefreshAggregates();
    }

    partial void OnSelectedDiveChanged(Dive? value)
    {
        RaiseUnitDependentProperties();
    }

    partial void OnHoverSampleChanged(DiveSample? value)
    {
        RaiseUnitDependentProperties();
    }

    private void RefreshAggregates()
    {
        OnPropertyChanged(nameof(TotalDives));
        OnPropertyChanged(nameof(TotalBottomTimeDisplay));
    }

    private string FormatNdtOrNdl(string fallback)
    {
        var ndt = HoverSample?.NdtMinutes ?? HoverSample?.NdlMinutes;
        if (ndt is null) return fallback;
        return $"{ndt.Value:0} Mins";
    }
}
