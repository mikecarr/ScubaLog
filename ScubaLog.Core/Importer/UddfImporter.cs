namespace ScubaLog.Core.Importer;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using ScubaLog.Core.Models;



public static class UddfImporter
{
    private sealed record MixInfo(string Name, double? Fo2);

    public static List<Dive> Import(string uddfXml)
    {
        var doc = XDocument.Parse(uddfXml, LoadOptions.PreserveWhitespace);

        // Find all <dive> nodes regardless of namespace
        var diveNodes = doc.Descendants().Where(e => e.Name.LocalName.Equals("dive", StringComparison.OrdinalIgnoreCase));

        var dives = new List<Dive>();

        foreach (var d in diveNodes)
        {
            var dive = new Dive();

            // ---- Basic fields (best-effort) ----
            // UDDF sometimes has <datetime> or <date>/<time>, sometimes under <informationbefore/after>
            dive.StartTime = TryReadDateTime(d) ?? DateTime.Now;

            // Duration sometimes exists at dive level, but we can also compute from samples.
            // We'll compute later if samples present.

            dive.Number = TryReadInt(FirstDesc(d, "divenumber")) ?? 0;
            dive.Notes  = FirstDesc(d, "notes")?.Value?.Trim();

            // ---- Samples ----
            // UDDF can express profile points as <waypoint> / <sample> / <profiledata> / etc.
            // We'll support common patterns: any element that contains BOTH depth + time.
            var mixLookup = BuildMixLookup(d);
            var samples = ReadSamples(d, mixLookup);
            if (samples.Count > 0)
            {
                dive.Samples = samples;

                // Derived fields
                dive.Duration = samples.Last().Time;
                dive.MaxDepthMeters = samples.Max(s => s.DepthMeters);
                dive.AvgDepthMeters = samples.Average(s => s.DepthMeters);

                PopulateTanks(d, dive, mixLookup);
            }

            dives.Add(dive);
        }

        return dives;
    }

    private static List<DiveSample> ReadSamples(XElement dive, Dictionary<string, MixInfo> mixLookup)
    {
        var fallbackMix = mixLookup.Values.FirstOrDefault();
        var currentGas = fallbackMix?.Name ?? string.Empty;
        double? currentFo2 = fallbackMix?.Fo2;

        var points = new List<DiveSample>();

        // Heuristic: any element that has children named depth + time (or equivalents)
        // We'll scan descendants and pick “point-like” elements.
        var pointNodes = dive.Descendants()
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

        int idx = 0;

        foreach (var p in pointNodes)
        {
            // Depth: prefer meters, fall back to unit conversion if unit attribute exists
            var depthEl = FirstDesc(p, "depth");
            if (depthEl is null) continue;

            var depthVal = TryReadDouble(depthEl.Value);
            if (depthVal is null) continue;

            var depthMeters = ConvertDepthToMeters(depthVal.Value, depthEl);

            // Time: try seconds/minutes, or mm:ss
            var timeEl = FirstDesc(p, "time") ?? FirstDesc(p, "divetime") ?? FirstDesc(p, "duration");
            if (timeEl is null) continue;

            var time = ParseTimeSpan(timeEl.Value, timeEl);
            if (time is null) continue;

            var s = new DiveSample
            {
                Index = idx++,
                Time = time.Value,
                DepthMeters = depthMeters,

                TemperatureC = ReadTemperatureC(p),
                TankPressureBar = ReadPressureBar(p),

                Ppo2 = TryReadDouble(FirstDesc(p, "ppo2")?.Value),
                NdtMinutes = TryReadDouble(FirstDesc(p, "ndt")?.Value) ?? TryReadDouble(FirstDesc(p, "ndl")?.Value),
                TtsMinutes = TryReadDouble(FirstDesc(p, "tts")?.Value),

                RmvLitersPerMinute = ReadRmvLpm(p),
                SacBarPerMinute = ReadSacBarPerMin(p),

                AscentRateMps = ReadAscentRateMps(p),

                DecoStopDepthMeters = ReadDecoStopDepthMeters(p),
                DecoStopMinutes = TryReadDouble(FirstDesc(p, "decotime")?.Value) ?? TryReadDouble(FirstDesc(p, "stopminutes")?.Value),

            };

            // Gas tracking via switchmix refs
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
            s.Gas = gas;

            if (s.Ppo2 is null && currentFo2 is not null)
            {
                var ambientAta = 1.0 + depthMeters / 10.0;
                s.Ppo2 = currentFo2.Value * ambientAta;
            }

            points.Add(s);
        }

        // De-dupe / sort by time (UDDF sometimes nests/duplicates)
        return points
            .GroupBy(s => s.Time)
            .Select(g => g.OrderByDescending(x => x.DepthMeters).First())
            .OrderBy(s => s.Time)
            .ToList();
    }

    // ---------- helpers (namespace-agnostic) ----------

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
        // Common patterns: <datetime>2025-01-01T10:00:00</datetime>
        var dt = FirstDesc(dive, "datetime")?.Value?.Trim()
                 ?? FirstDesc(dive, "date")?.Value?.Trim();

        if (dt is null) return null;

        if (DateTime.TryParse(dt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d1))
            return d1;

        return null;
    }

    private static TimeSpan? ParseTimeSpan(string raw, XElement? timeEl)
    {
        raw = raw.Trim();

        // If there is a "unit" attribute, prefer it.
        var unit = timeEl?.Attributes().FirstOrDefault(a =>
            a.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

        // mm:ss or hh:mm:ss
        if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var ts))
            return ts;

        var val = TryReadDouble(raw);
        if (val is null) return null;

        return unit?.ToLowerInvariant() switch
        {
            "s" or "sec" or "secs" or "second" or "seconds" => TimeSpan.FromSeconds(val.Value),
            "min" or "mins" or "minute" or "minutes"        => TimeSpan.FromMinutes(val.Value),
            _ => TimeSpan.FromSeconds(val.Value), // common default
        };
    }

    private static double ConvertDepthToMeters(double value, XElement depthEl)
    {
        var unit = depthEl.Attributes().FirstOrDefault(a =>
            a.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

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

        var unit = t.Attributes().FirstOrDefault(a =>
            a.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

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

        var unit = pr.Attributes().FirstOrDefault(a =>
            a.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

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
        if (value > 50_000) return value / 100_000.0;   // Pa -> bar
        if (value > 200 && value < 6_000) return value / 14.5037738; // psi -> bar
        return value; // assume bar
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

        var unit = rmv.Attributes().FirstOrDefault(a =>
            a.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

        return unit?.ToLowerInvariant() switch
        {
            "cfm" => v.Value * 28.3168466, // cuft/min -> L/min
            _ => v.Value
        };
    }

    private static double? ReadSacBarPerMin(XElement p)
    {
        var sac = FirstDesc(p, "sac");
        if (sac is null) return null;

        var v = TryReadDouble(sac.Value);
        if (v is null) return null;

        var unit = sac.Attributes().FirstOrDefault(a =>
            a.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

        return unit?.ToLowerInvariant() switch
        {
            "psi/min" or "psimin" => v.Value / 14.5037738,
            _ => v.Value
        };
    }

    private static double? ReadAscentRateMps(XElement p)
    {
        var a = FirstDesc(p, "ascent") ?? FirstDesc(p, "ascentrate");
        if (a is null) return null;

        var v = TryReadDouble(a.Value);
        if (v is null) return null;

        var unit = a.Attributes().FirstOrDefault(x => x.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

        return unit?.ToLowerInvariant() switch
        {
            "ft/s" => (v.Value / 3.28084),
            "ft/min" => (v.Value / 3.28084) / 60.0,
            "m/min" => v.Value / 60.0,
            _ => v.Value
        };
    }

    private static double? ReadDecoStopDepthMeters(XElement p)
    {
        var d = FirstDesc(p, "decodepth") ?? FirstDesc(p, "stopdepth");
        if (d is null) return null;

        var v = TryReadDouble(d.Value);
        if (v is null) return null;

        var unit = d.Attributes().FirstOrDefault(x => x.Name.LocalName.Equals("unit", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
        return unit?.ToLowerInvariant() switch
        {
            "ft" => v.Value / 3.28084,
            _ => v.Value
        };
    }
}
