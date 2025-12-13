using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ScubaLog.Core.Models;

namespace ScubaLog.Core.Services;

public class DiveLogService
{
    public List<Dive> Dives { get; } = new();

    public bool HasImported { get; private set; }

    public DiveLogService()
    {
        SeedDemoDivesIfEmpty();
    }

    private void SeedDemoDivesIfEmpty()
    {
        if (Dives.Count > 0)
            return;

        // Only used if nothing imported – demo dives
        var dive1 = new Dive
        {
            Number         = 1,
            StartTime      = DateTime.Now.AddDays(-1),
            Duration       = TimeSpan.FromMinutes(46),
            MaxDepthMeters = 21.3,
            AvgDepthMeters = 14.8,
            Notes          = "La Jolla Shores – skills and scooter play"
        };
        dive1.Samples = GenerateSyntheticProfile(dive1);

        var dive2 = new Dive
        {
            Number         = 2,
            StartTime      = DateTime.Now.AddDays(-7),
            Duration       = TimeSpan.FromMinutes(54),
            MaxDepthMeters = 32,
            AvgDepthMeters = 20.1,
            Notes          = "Yukon – nice viz, mild current"
        };
        dive2.Samples = GenerateSyntheticProfile(dive2);

        Dives.Add(dive1);
        Dives.Add(dive2);
    }

    /// <summary>
    /// Import UDDF (XML) dives from disk and replace the current dive list.
    /// </summary>
    public void ImportUddfFromPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("UDDF file not found.", filePath);

        var xml  = File.ReadAllText(filePath);
        var dives = ParseUddf(xml);

        if (dives.Count == 0)
            throw new InvalidOperationException("No <dive> elements were found in the UDDF file.");

        Dives.Clear();
        Dives.AddRange(dives);
        HasImported = true;
    }

    /// <summary>
    /// Generate a fake but dive-computer-ish profile from summary stats.
    /// Used for demo dives and for imported dives until we parse real samples.
    /// </summary>
    public static List<DiveSample> GenerateSyntheticProfile(Dive dive)
    {
        var samples = new List<DiveSample>();

        // Very rough, but “dive-computer-ish”:
        var totalMinutes   = Math.Max(10, dive.Duration.TotalMinutes);
        var steps          = (int)Math.Max(40, totalMinutes * 2); // ~30s
        var minutesPerStep = totalMinutes / steps;

        // Deterministic random per-dive so graphs are stable between runs
        var seed = dive.Number * 1337 + (int)Math.Round(totalMinutes);
        var rand = new Random(seed);

        // Tank / gas assumptions (AL80 at 3000 psi)
        const double startPressurePsi = 3000;
        const double tankVolumeCuFt   = 80;
        var psiPerCuFt = startPressurePsi / tankVolumeCuFt;

        var pressurePsi = startPressurePsi;

        // Simple nitrogen loading model
        double nitrogenLoad           = 0;
        const double nitrogenThreshold = 1000; // arbitrary units

        // Temperature assumptions
        const double surfaceTempC = 20;
        var bottomTempC           = surfaceTempC - 8; // ~8°C colder at depth

        // FO2 for PPO2 calculation (EAN32)
        const double fo2 = 0.32;

        for (int i = 0; i <= steps; i++)
        {
            var t    = i / (double)steps; // 0..1 along the dive
            var time = TimeSpan.FromMinutes(i * minutesPerStep);

            // depth profile: down, flat, up with slight “wiggle” + noise
            double depthMeters;
            if (t < 0.15)
                depthMeters = t / 0.15 * dive.MaxDepthMeters;
            else if (t > 0.8)
                depthMeters = (1 - t) / 0.2 * dive.MaxDepthMeters;
            else
                depthMeters = dive.MaxDepthMeters;

            depthMeters *= 0.97 + 0.03 * Math.Sin(t * Math.PI * 4);
            depthMeters += (rand.NextDouble() - 0.5);      // ±0.5 m
            depthMeters  = Math.Clamp(depthMeters, 0, dive.MaxDepthMeters + 1);

            // Ambient pressure in ATA
            var ambientAta = 1.0 + depthMeters / 10.0;

            // SAC (cuft/min) and RMV (L/min)
            var depthFactor = depthMeters / Math.Max(1.0, dive.MaxDepthMeters);
            var sacCuFtMin  = 0.4 + 0.6 * depthFactor;       // 0.4–1.0 cf/min
            sacCuFtMin     *= 0.9 + 0.2 * rand.NextDouble(); // ±10%

            var rmvLpm = sacCuFtMin * 28.3168466; // cuft/min -> L/min

            // Gas actually used at depth = SAC * ambient * time
            var gasUsedCuFt = sacCuFtMin * ambientAta * minutesPerStep;
            pressurePsi    -= gasUsedCuFt * psiPerCuFt;
            if (pressurePsi < 0) pressurePsi = 0;

            var tankPressureBar = pressurePsi / 14.5037738;

            // Temperature
            var tempBlend = depthMeters / Math.Max(1.0, dive.MaxDepthMeters);
            var tempC     = surfaceTempC * (1 - tempBlend) + bottomTempC * tempBlend;
            tempC        += (rand.NextDouble() - 0.5) * 0.3; // ±0.3°C

            // PPO2
            var ppo2 = fo2 * ambientAta;

            // Toy NDL / TTS
            var depthForLoad = Math.Max(0, depthMeters - 5);
            var loadRate     = Math.Pow(depthForLoad + 1, 1.3);
            nitrogenLoad    += loadRate * minutesPerStep;

            double ndlMinutes;
            if (depthMeters < 6)
            {
                ndlMinutes = 999;
            }
            else
            {
                var remaining = nitrogenThreshold - nitrogenLoad;
                ndlMinutes = remaining <= 0 ? 0 : remaining / loadRate;
            }

            var ascentTimeMin = depthMeters / 9.0 + (depthMeters > 9 ? 3.0 : 0.0);
            var ttsMinutes    = ndlMinutes > 0 ? ascentTimeMin : ascentTimeMin + Math.Min(20, -ndlMinutes);

            var sample = new DiveSample
            {
                Time       = time,
                Ppo2       = ppo2,
                NdlMinutes = ndlMinutes,
                TtsMinutes = ttsMinutes,
            };

            // Prefer the newer properties if your model has them.
            // (Old properties may still exist but are marked obsolete.)
            TrySet(sample, "DepthMeters", depthMeters);
            TrySet(sample, "TemperatureC", tempC);
            TrySet(sample, "TankPressureBar", tankPressureBar);
            TrySet(sample, "RmvLitersPerMinute", rmvLpm);

            // Also set legacy properties for backwards compatibility if present.
            TrySet(sample, "Depth", depthMeters);
            TrySet(sample, "Temperature", tempC);
            TrySet(sample, "TankPressure", (int)Math.Round(pressurePsi));
            TrySet(sample, "Rmv", sacCuFtMin);

            samples.Add(sample);
        }

        return samples;
    }

    // ---------------- UDDF parsing (namespace-agnostic, best-effort) ----------------

    private static List<Dive> ParseUddf(string uddfXml)
    {
        var doc = XDocument.Parse(uddfXml);

        var diveNodes = doc
            .Descendants()
            .Where(e => e.Name.LocalName.Equals("dive", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var dives = new List<Dive>();
        var diveNumber = 1;

        foreach (var d in diveNodes)
        {
            var dive = new Dive
            {
                Number    = TryReadInt(FirstDesc(d, "divenumber")) ?? diveNumber,
                StartTime = TryReadDateTime(d) ?? DateTime.Now,
                Notes     = FirstDesc(d, "notes")?.Value?.Trim()
            };

            // Samples
            var mixLookup = BuildMixLookup(d);
            var samples = ReadSamples(d, mixLookup);
            if (samples.Count > 0)
            {
                dive.Samples       = samples;
                dive.Duration      = samples.Last().Time;
                dive.MaxDepthMeters = samples.Max(s => GetDouble(s, "DepthMeters") ?? GetDouble(s, "Depth") ?? 0);
                dive.AvgDepthMeters = samples.Average(s => GetDouble(s, "DepthMeters") ?? GetDouble(s, "Depth") ?? 0);

                PopulateTanks(d, dive, mixLookup);
            }
            else
            {
                // No samples found – keep a synthetic profile so UI still works
                dive.Duration       = TimeSpan.FromMinutes(30);
                dive.MaxDepthMeters = 18;
                dive.AvgDepthMeters = 10;
                dive.Samples        = GenerateSyntheticProfile(dive);
            }

            dives.Add(dive);
            diveNumber++;
        }

        return dives;
    }

    private sealed record MixInfo(string Name, double? Fo2);

    private static List<DiveSample> ReadSamples(XElement dive, Dictionary<string, MixInfo> mixLookup)
    {
        var fallbackMix = mixLookup.Values.FirstOrDefault();
        var currentGas = fallbackMix?.Name ?? string.Empty;
        double? currentFo2 = fallbackMix?.Fo2;

        var samples = new List<DiveSample>();

        // Heuristic: any element that contains BOTH depth and time-ish values.
        var pointNodes = dive
            .Descendants()
            .Where(e =>
            {
                var hasDepth = e.Descendants().Any(x => x.Name.LocalName.Equals("depth", StringComparison.OrdinalIgnoreCase));
                var hasTime  = e.Descendants().Any(x =>
                    x.Name.LocalName.Equals("time", StringComparison.OrdinalIgnoreCase) ||
                    x.Name.LocalName.Equals("divetime", StringComparison.OrdinalIgnoreCase) ||
                    x.Name.LocalName.Equals("duration", StringComparison.OrdinalIgnoreCase));
                return hasDepth && hasTime;
            })
            .ToList();

        var idx = 0;

        foreach (var p in pointNodes)
        {
            var depthEl = FirstDesc(p, "depth");
            var timeEl  = FirstDesc(p, "time") ?? FirstDesc(p, "divetime") ?? FirstDesc(p, "duration");

            if (depthEl is null || timeEl is null)
                continue;

            var depthVal = TryReadDouble(depthEl.Value);
            if (depthVal is null)
                continue;

            var time = ParseTimeSpan(timeEl.Value, timeEl);
            if (time is null)
                continue;

            var depthMeters = ConvertDepthToMeters(depthVal.Value, depthEl);

            var sample = new DiveSample
            {
                Time = time.Value,
            };

            TrySet(sample, "Index", idx);
            idx++;

            TrySet(sample, "DepthMeters", depthMeters);
            TrySet(sample, "Depth", depthMeters);

            // Optional extras (best-effort)
            var tempC = ReadTemperatureC(p);
            if (tempC is not null)
            {
                TrySet(sample, "TemperatureC", tempC.Value);
                TrySet(sample, "Temperature", tempC.Value);
            }

            var pressureBar = ReadPressureBar(p);
            if (pressureBar is not null)
            {
                TrySet(sample, "TankPressureBar", pressureBar.Value);
                TrySet(sample, "TankPressure", (int)Math.Round(pressureBar.Value * 14.5037738));
            }

            var ppo2 = TryReadDouble(FirstDesc(p, "ppo2")?.Value);
            if (ppo2 is not null)
                TrySet(sample, "Ppo2", ppo2.Value);

            var ndl = TryReadDouble(FirstDesc(p, "ndt")?.Value) ?? TryReadDouble(FirstDesc(p, "ndl")?.Value);
            if (ndl is not null)
                TrySet(sample, "NdlMinutes", ndl.Value);

            var tts = TryReadDouble(FirstDesc(p, "tts")?.Value);
            if (tts is not null)
                TrySet(sample, "TtsMinutes", tts.Value);

            var rmvLpm = ReadRmvLpm(p);
            if (rmvLpm is not null)
            {
                TrySet(sample, "RmvLitersPerMinute", rmvLpm.Value);
                // Legacy RMV (cuft/min) if present
                TrySet(sample, "Rmv", rmvLpm.Value / 28.3168466);
            }

            // Gas tracking via switchmix refs (per UDDF spec)
            var switchRef = FirstDesc(p, "switchmix")?.Attribute("ref")?.Value;
            if (!string.IsNullOrWhiteSpace(switchRef) && mixLookup.TryGetValue(switchRef, out var mix))
            {
                currentGas = mix.Name;
                currentFo2 = mix.Fo2;
            }

            var gas = FirstDesc(p, "gas")?.Value?.Trim()
                      ?? FirstDesc(p, "gasname")?.Value?.Trim()
                      ?? FirstDesc(p, "mix")?.Value?.Trim()
                      ?? currentGas;
            if (!string.IsNullOrWhiteSpace(gas))
                TrySet(sample, "Gas", gas);

            // Derive PPO2 if we have FO2 and depth
            if (sample.Ppo2 is null && currentFo2 is not null)
            {
                var ambientAta = 1.0 + depthMeters / 10.0;
                sample.Ppo2 = currentFo2.Value * ambientAta;
            }

            samples.Add(sample);
        }

        // Sort/unique by time
        return samples
            .GroupBy(s => s.Time)
            .Select(g => g.First())
            .OrderBy(s => s.Time)
            .ToList();
    }

    private static void PopulateTanks(XElement diveNode, Dive dive, Dictionary<string, MixInfo> mixLookup)
    {
        var tankVolume = FirstDesc(diveNode, "tankvolume")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(tankVolume))
            return;

        if (!double.TryParse(tankVolume, NumberStyles.Float, CultureInfo.InvariantCulture, out var vol))
            return;

        double liters = vol <= 5 ? vol * 1000.0 : vol;

        var mix = mixLookup.Values.FirstOrDefault();

        var tank = new TankUsage
        {
            SortOrder = 0,
            TankSizeLiters = liters,
            TankName = "Tank 1",
            O2Percent = mix?.Fo2 is null ? null : mix.Fo2 * 100.0,
            HePercent = null
        };

        dive.Tanks.Add(tank);
    }

    private static XElement? FirstDesc(XElement root, string localName) =>
        root.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    private static double? TryReadDouble(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out v)) return v;
        return null;
    }

    private static int? TryReadInt(XElement? el)
    {
        if (el is null) return null;
        if (int.TryParse(el.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
        return null;
    }

    private static DateTime? TryReadDateTime(XElement dive)
    {
        var dt = FirstDesc(dive, "datetime")?.Value?.Trim()
                 ?? FirstDesc(dive, "date")?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(dt))
            return null;

        if (DateTime.TryParse(dt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d1))
            return d1;

        if (DateTime.TryParse(dt, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var d2))
            return d2;

        return null;
    }

    private static TimeSpan? ParseTimeSpan(string raw, XElement timeEl)
    {
        raw = raw.Trim();

        // hh:mm:ss or mm:ss
        if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var ts))
            return ts;

        var val = TryReadDouble(raw);
        if (val is null) return null;

        var unit = timeEl.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

        return unit?.ToLowerInvariant() switch
        {
            "s" or "sec" or "secs" or "second" or "seconds" => TimeSpan.FromSeconds(val.Value),
            "min" or "mins" or "minute" or "minutes"        => TimeSpan.FromMinutes(val.Value),
            _ => TimeSpan.FromSeconds(val.Value),
        };
    }

    private static double ConvertDepthToMeters(double value, XElement depthEl)
    {
        var unit = depthEl.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
        return unit?.ToLowerInvariant() switch
        {
            "ft" or "feet" => value / 3.28084,
            "m" or "meter" or "meters" => value,
            "cm" or "centimeter" or "centimeters" => value / 100.0,
            _ => value, // assume meters if unspecified
        };
    }

    private static double? ReadTemperatureC(XElement p)
    {
        var t = FirstDesc(p, "temperature") ?? FirstDesc(p, "temp");
        if (t is null) return null;

        var v = TryReadDouble(t.Value);
        if (v is null) return null;

        var unit = t.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
        var value = v.Value;

        if (unit is null && value > 150) // likely Kelvin
            return value - 273.15;

        return unit?.ToLowerInvariant() switch
        {
            "f" or "°f" => (value - 32.0) * 5.0 / 9.0,
            "k" or "kelvin" or "°k" => value - 273.15,
            _ => value
        };
    }

    private static double? ReadPressureBar(XElement p)
    {
        var pr = FirstDesc(p, "pressure") ?? FirstDesc(p, "tankpressure");
        if (pr is null) return null;

        var v = TryReadDouble(pr.Value);
        if (v is null) return null;

        var unit = pr.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
        var value = v.Value;
        return unit?.ToLowerInvariant() switch
        {
            "psi" => value / 14.5037738,
            "kpa" => value / 100.0,
            "pa" or "pascal" or "pascals" => value / 100_000.0,
            "mbar" => value / 1_000.0,
            "bar" or "bars" or "bara" => value,
            _ => NormalizeUnknownPressure(value)
        };
    }

    private static double NormalizeUnknownPressure(double value)
    {
        // Heuristic: MacDive UDDF often omits units. Typical ranges:
        //  - Pa: tens of millions (23_000_000 for ~230 bar)
        //  - psi: ~500–4000
        //  - bar: ~0–300
        if (value > 50_000) return value / 100_000.0;   // assume Pa -> bar
        if (value > 200 && value < 6_000) return value / 14.5037738; // assume psi -> bar
        return value; // assume bar
    }

    private static Dictionary<string, MixInfo> BuildMixLookup(XElement dive)
    {
        var dict = new Dictionary<string, MixInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var mix in dive.Descendants().Where(e => e.Name.LocalName.Equals("mix", StringComparison.OrdinalIgnoreCase)))
        {
            var id = mix.Attribute("id")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var name = FirstDesc(mix, "name")?.Value?.Trim();
            var o2   = TryReadDouble(FirstDesc(mix, "o2")?.Value);
            var he   = TryReadDouble(FirstDesc(mix, "he")?.Value);
            var n2   = TryReadDouble(FirstDesc(mix, "n2")?.Value);

            if (string.IsNullOrWhiteSpace(name))
            {
                if (o2 is not null || he is not null || n2 is not null)
                {
                    var o2p = o2 is null ? null : $"{o2.Value * 100:0.#}% O2";
                    var hep = he is null ? null : $"{he.Value * 100:0.#}% He";
                    var n2p = n2 is null ? null : $"{n2.Value * 100:0.#}% N2";
                    name = string.Join(" / ", new[] { o2p, hep, n2p }.Where(s => !string.IsNullOrWhiteSpace(s)));
                }
            }

            var label = string.IsNullOrWhiteSpace(name) ? id : name;
            dict[id] = new MixInfo(label, o2);
        }

        return dict;
    }

    private static double? ReadRmvLpm(XElement p)
    {
        var rmv = FirstDesc(p, "rmv");
        if (rmv is null) return null;

        var v = TryReadDouble(rmv.Value);
        if (v is null) return null;

        var unit = rmv.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
        return unit?.ToLowerInvariant() switch
        {
            "cfm" => v.Value * 28.3168466,
            _ => v.Value
        };
    }

    // Reflection helpers so this compiles even if some properties differ across your model versions
    private static void TrySet(object target, string propertyName, object? value)
    {
        var prop = target.GetType().GetProperty(propertyName);
        if (prop is null || !prop.CanWrite) return;

        try
        {
            if (value is null)
            {
                prop.SetValue(target, null);
                return;
            }

            var t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            var converted = t.IsInstanceOfType(value) ? value : Convert.ChangeType(value, t, CultureInfo.InvariantCulture);
            prop.SetValue(target, converted);
        }
        catch
        {
            // ignore (best-effort)
        }
    }

    private static double? GetDouble(object target, string propertyName)
    {
        var prop = target.GetType().GetProperty(propertyName);
        if (prop is null) return null;

        var val = prop.GetValue(target);
        if (val is null) return null;

        try
        {
            return Convert.ToDouble(val, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }
}
