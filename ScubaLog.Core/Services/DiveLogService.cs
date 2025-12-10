using System;
using System.Collections.Generic;
using ScubaLog.Core.Models;

namespace ScubaLog.Core.Services;

public class DiveLogService
{
    public List<Dive> Dives { get; } = new();

    public DiveLogService()
    {
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

        var pressure = startPressurePsi;

        // Simple nitrogen loading model
        double nitrogenLoad          = 0;
        const double nitrogenThreshold = 1000; // arbitrary units

        // Temperature assumptions
        const double surfaceTempC = 20;
        var bottomTempC          = surfaceTempC - 8; // ~8°C colder at depth

        // FO2 for PPO2 calculation (EAN32)
        const double fo2 = 0.32;

        for (int i = 0; i <= steps; i++)
        {
            var t    = i / (double)steps; // 0..1 along the dive
            var time = TimeSpan.FromMinutes(i * minutesPerStep);

            // depth profile: down, flat, up with slight “wiggle” + noise
            double depth;
            if (t < 0.15)
                depth = t / 0.15 * dive.MaxDepthMeters;
            else if (t > 0.8)
                depth = (1 - t) / 0.2 * dive.MaxDepthMeters;
            else
                depth = dive.MaxDepthMeters;

            depth *= 0.97 + 0.03 * Math.Sin(t * Math.PI * 4);
            depth += (rand.NextDouble() - 0.5);      // ±0.5 m
            depth  = Math.Clamp(depth, 0, dive.MaxDepthMeters + 1);

            // Ambient pressure in ATA
            var ambientAta = 1.0 + depth / 10.0;

            // SAC / RMV
            var depthFactor = depth / Math.Max(1.0, dive.MaxDepthMeters);
            var sac         = 0.4 + 0.6 * depthFactor;     // 0.4–1.0 cf/min
            sac            *= 0.9 + 0.2 * rand.NextDouble(); // ±10%

            // Gas actually used at depth = SAC * ambient * time
            var gasUsedCuFt = sac * ambientAta * minutesPerStep;
            pressure       -= gasUsedCuFt * psiPerCuFt;
            if (pressure < 0) pressure = 0;

            // Temperature
            var tempBlend = depth / Math.Max(1.0, dive.MaxDepthMeters);
            var tempC     = surfaceTempC * (1 - tempBlend) + bottomTempC * tempBlend;
            tempC        += (rand.NextDouble() - 0.5) * 0.3; // ±0.3°C

            // PPO2
            var ppo2 = fo2 * ambientAta;

            // Toy NDL / TTS
            var depthForLoad = Math.Max(0, depth - 5);
            var loadRate     = Math.Pow(depthForLoad + 1, 1.3);
            nitrogenLoad    += loadRate * minutesPerStep;

            double ndl;
            if (depth < 6)
            {
                ndl = 999;
            }
            else
            {
                var remaining = nitrogenThreshold - nitrogenLoad;
                ndl = remaining <= 0 ? 0 : remaining / loadRate;
            }

            var ascentTime = depth / 9.0 + (depth > 9 ? 3.0 : 0.0);
            double tts     = ndl > 0 ? ascentTime : ascentTime + Math.Min(20, -ndl);

            samples.Add(new DiveSample
            {
                Time           = time,
                DepthMeters    = depth,
                Rmv            = sac,
                TemperatureC   = tempC,
                Ppo2           = ppo2,
                TankPressurePsi = (int)Math.Round(pressure),
                NdlMinutes     = ndl,
                TtsMinutes     = tts
            });
        }

        return samples;
    }
}