using System;

namespace ScubaLog.Core.Units;

public static class UnitConverter
{
    public static double MetersToFeet(double m) => m * 3.280839895;
    public static double FeetToMeters(double ft) => ft / 3.280839895;

    public static double CToF(double c) => (c * 9.0 / 5.0) + 32.0;
    public static double FToC(double f) => (f - 32.0) * 5.0 / 9.0;

    public static double BarToPsi(double bar) => bar * 14.5037738;
    public static double PsiToBar(double psi) => psi / 14.5037738;

    public static double LitersToCuFt(double l) => l * 0.0353146667;
    public static double CuFtToLiters(double cuft) => cuft / 0.0353146667;

    public static double KgToLb(double kg) => kg * 2.2046226218;
    public static double LbToKg(double lb) => lb / 2.2046226218;

    public static double DisplayDepth(double meters, UnitPreferences u) =>
        u.UseFeet ? MetersToFeet(meters) : meters;

    public static string DepthUnit(UnitPreferences u) => u.UseFeet ? "ft" : "m";

    public static double DisplayTemp(double c, UnitPreferences u) =>
        u.UseFahrenheit ? CToF(c) : c;

    public static string TempUnit(UnitPreferences u) => u.UseFahrenheit ? "°F" : "°C";

    public static double DisplayPressureFromBar(double bar, UnitPreferences u) =>
        u.UsePsi ? BarToPsi(bar) : bar;

    public static string PressureUnit(UnitPreferences u) => u.UsePsi ? "psi" : "bar";

    public static double DisplayWeight(double kg, UnitPreferences u) =>
        u.UsePounds ? KgToLb(kg) : kg;

    public static string WeightUnit(UnitPreferences u) => u.UsePounds ? "lb" : "kg";
}