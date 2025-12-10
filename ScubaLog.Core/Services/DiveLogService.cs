using System;
using System.Collections.Generic;
using ScubaLog.Core.Models;

namespace ScubaLog.Core.Services;

public class DiveLogService
{
    public List<Dive> Dives { get; } = new();

    public DiveLogService()
    {
        var dive1 = new Dive
        {
            Number         = 1,
            StartTime      = DateTime.Now.AddDays(-1),
            Duration       = TimeSpan.FromMinutes(46),
            MaxDepthMeters = 21.3,
            AvgDepthMeters = 14.8,
            Notes          = "La Jolla Shores – skills and scooter play"
        };
        dive1.Samples = GenerateSimpleProfile(dive1);

        var dive2 = new Dive
        {
            Number         = 2,
            StartTime      = DateTime.Now.AddDays(-7),
            Duration       = TimeSpan.FromMinutes(54),
            MaxDepthMeters = 32,
            AvgDepthMeters = 20.1,
            Notes          = "Yukon – nice viz, mild current"
        };
        dive2.Samples = GenerateSimpleProfile(dive2);

        Dives.Add(dive1);
        Dives.Add(dive2);
    }

    private List<DiveSample> GenerateSimpleProfile(Dive dive)
    {
        // Very rough, but “dive-computer-ish”:
        // - depth: down / flat / up with small random + sinusoidal variation
        // - RMV/SAC: increases with depth + noise
        // - tank pressure: derived from SAC and ambient pressure
        // - temperature: cooler at depth
        // - PPO2: FO2 * ambient pressure (assume EAN32)
        // - NDL/TTS: simple nitrogen-load toy model (NOT for real diving!)

        var samples = new List<DiveSample>();

        // Deterministic random per-dive so graphs are stable between runs
        var totalMinutes = Math.Max(10, dive.Duration.TotalMinutes);
        var steps = (int)Math.Max(40, totalMinutes * 2); // ~30s resolution
        var minutesPerStep = totalMinutes / steps;

        // Deterministic random per-dive so graphs are stable between runs
        int seed = (int)dive.Number * 1337 + (int)Math.Round(totalMinutes);
        var rand = new Random(seed);

        // Tank / gas assumptions (AL80 at 3000 psi)
        const double startPressurePsi = 3000;
        const double tankVolumeCuFt = 80;
        var psiPerCuFt = startPressurePsi / tankVolumeCuFt;

        var pressure = startPressurePsi;

        // Simple nitrogen loading model
        double nitrogenLoad = 0;
        const double nitrogenThreshold = 1000; // arbitrary units

        // Temperature assumptions
        const double surfaceTempC = 20;
        var bottomTempC = surfaceTempC - 8; // ~8°C colder at depth

        // FO2 for PPO2 calculation (EAN32)
        const double fo2 = 0.32;

        for (int i = 0; i <= steps; i++)
        {
            var t = i / (double)steps; // 0..1 along the dive
            var time = TimeSpan.FromMinutes(i * minutesPerStep);

            // --- depth profile: down, flat, up with slight “wiggle” + noise ---
            double depth;
            if (t < 0.15)
            {
                // descent
                depth = t / 0.15 * dive.MaxDepthMeters;
            }
            else if (t > 0.8)
            {
                // ascent
                depth = (1 - t) / 0.2 * dive.MaxDepthMeters;
            }
            else
            {
                // flat-ish bottom
                depth = dive.MaxDepthMeters;
            }

            // Add a small sinusoidal and random wobble to depth
            depth *= 0.97 + 0.03 * Math.Sin(t * Math.PI * 4);
            depth += (rand.NextDouble() - 0.5); // ±0.5 m noise
            depth = Math.Clamp(depth, 0, dive.MaxDepthMeters + 1);

            // Ambient pressure in ATA (roughly: 1 atm at surface, +1 every 10 m)
            var ambientAta = 1.0 + depth / 10.0;

            // --- SAC / RMV (surface-equivalent consumption) ---
            // Base SAC 0.4 cf/min at surface, rising with workload (depth)
            var depthFactor = depth / Math.Max(1.0, dive.MaxDepthMeters);
            var sac = 0.4 + 0.6 * depthFactor; // 0.4–1.0 cf/min typical range
            // Add some random variation
            sac *= 0.9 + 0.2 * rand.NextDouble(); // ±10 %

            // Gas actually used at depth = SAC * ambient * time
            var gasUsedCuFt = sac * ambientAta * minutesPerStep;
            pressure -= gasUsedCuFt * psiPerCuFt;
            if (pressure < 0) pressure = 0;

            // --- Temperature: cooler at depth, plus a bit of noise ---
            var tempBlend = depth / Math.Max(1.0, dive.MaxDepthMeters);
            var tempC = surfaceTempC * (1 - tempBlend) + bottomTempC * tempBlend;
            tempC += (rand.NextDouble() - 0.5) * 0.3; // ±0.3°C

            // --- PPO2 from FO2 * ambient pressure (NOT a full CCR model) ---
            var ppo2 = fo2 * ambientAta;

            // --- Toy NDL / TTS model ---
            // Deeper + longer => more “nitrogenLoad”.
            // This is intentionally simple and *not* for real-world planning.
            var depthForLoad = Math.Max(0, depth - 5); // ignore first 5 m a bit
            var loadRate = Math.Pow(depthForLoad + 1, 1.3); // depth-weighted
            nitrogenLoad += loadRate * minutesPerStep;

            double ndl;
            if (depth < 6)
            {
                // basically unlimited in the shallows
                ndl = 999;
            }
            else
            {
                var remaining = nitrogenThreshold - nitrogenLoad;
                ndl = remaining <= 0 ? 0 : remaining / loadRate;
            }

            // Ascent time with simple 9 m/min + 3 min safety stop if > 9 m
            var ascentTime = depth / 9.0 + (depth > 9 ? 3.0 : 0.0);
            double tts = ndl > 0 ? ascentTime : ascentTime + Math.Min(20, -ndl);

            samples.Add(new DiveSample
            {
                Time         = time,
                DepthMeters  = depth,

                // Surface-equivalent breathing rate (cf/min)
                Rmv          = sac,

                // Extra series for the graph
                TemperatureC   = tempC,
                Ppo2           = ppo2,
                TankPressurePsi = (int)Math.Round(pressure),

                // Simple simulated deco info (in minutes)
                NdlMinutes    = ndl,
                TtsMinutes    = tts
            });
        }

        return samples;
    }
}