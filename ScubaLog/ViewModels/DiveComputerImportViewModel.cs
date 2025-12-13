using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScubaLog.Core.Importers;
using ScubaLog.Core.Models;

namespace ScubaLog.ViewModels;

public partial class DiveComputerImportViewModel : ViewModelBase
{
    private readonly IDiveComputerImporter _importer;
    private readonly IList<DiveComputerModel> _computers;

    public ObservableCollection<string> Manufacturers { get; }
    public ObservableCollection<string> Models { get; }
    public ObservableCollection<ImportScope> Scopes { get; } =
        new(new[] { ImportScope.AllDives, ImportScope.NotYetImported, ImportScope.NewOnly });

    [ObservableProperty] private string? selectedManufacturer;
    [ObservableProperty] private string? selectedModel;
    [ObservableProperty] private ImportScope selectedScope = ImportScope.AllDives;

    public DiveComputerImportViewModel(IDiveComputerImporter importer, IEnumerable<DiveComputerModel> computers)
    {
        _importer = importer;
        _computers = computers.ToList();

        Manufacturers = new ObservableCollection<string>(_computers.Select(c => c.Manufacturer).Distinct());
        Models = new ObservableCollection<string>();
    }

    partial void OnSelectedManufacturerChanged(string? value)
    {
        Models.Clear();
        if (string.IsNullOrWhiteSpace(value)) return;
        foreach (var m in _computers.Where(c => c.Manufacturer == value).Select(c => c.Model).Distinct())
            Models.Add(m);

        SelectedModel = Models.FirstOrDefault();
    }

    [RelayCommand]
    public async Task<List<Dive>> ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedManufacturer) || string.IsNullOrWhiteSpace(SelectedModel))
            return new List<Dive>();

        var comp = _computers.First(c => c.Manufacturer == SelectedManufacturer && c.Model == SelectedModel);
        return await _importer.ImportAsync(comp, SelectedScope);
    }
}
