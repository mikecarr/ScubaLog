using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using ScubaLog.Core.Units;

namespace ScubaLog.Converters;

public sealed class DepthToDisplayConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // Expected: [0] = depth in meters (double), [1] = UnitSystem
        if (values is null || values.Count < 2)
            return string.Empty;

        if (values[0] is not double meters)
            return string.Empty;

        var unitSystem = values[1] is UnitSystem us ? us : UnitSystem.Metric;

        return unitSystem switch
        {
            UnitSystem.Imperial => $"{meters * 3.28084:0.0}",
            _ => $"{meters:0.0}",
        };
    }

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}