using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ScubaLog.Core.Models;
using ScubaLog.Core.Services;

namespace ScubaLog.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    
    private readonly DiveLogService _service;

    public ObservableCollection<Dive> Dives { get; }

    [ObservableProperty]
    private Dive? selectedDive;

    [ObservableProperty]
    private DiveSample? hoverSample;

    // Which extra curves are visible on the graph
    [ObservableProperty]
    private bool showRmv = true;

    [ObservableProperty]
    private bool showTemp = true;

    [ObservableProperty]
    private bool showPpo2 = true;

    [ObservableProperty]
    private bool showAir = true;
    
    public MainViewModel()
    {
        _service = new DiveLogService();
        Dives = new ObservableCollection<Dive>(_service.Dives);
        
        // Default selection
        SelectedDive = Dives.FirstOrDefault();
    }
    
}