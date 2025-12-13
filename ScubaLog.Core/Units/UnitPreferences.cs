using CommunityToolkit.Mvvm.ComponentModel;

namespace ScubaLog.Core.Units;

public sealed partial class UnitPreferences : ObservableObject
{
    [ObservableProperty]
    private UnitSystem system = UnitSystem.Metric;

    public bool UseFeet => System == UnitSystem.Imperial;
    public bool UseFahrenheit => System == UnitSystem.Imperial;
    public bool UsePsi => System is UnitSystem.Imperial or UnitSystem.Canadian;
    public bool UsePounds => System is UnitSystem.Imperial or UnitSystem.Canadian;

    partial void OnSystemChanged(UnitSystem value)
    {
        // Derived flags need to refresh too.
        OnPropertyChanged(nameof(UseFeet));
        OnPropertyChanged(nameof(UseFahrenheit));
        OnPropertyChanged(nameof(UsePsi));
        OnPropertyChanged(nameof(UsePounds));
    }
}