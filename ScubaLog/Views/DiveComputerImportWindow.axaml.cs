using System.Collections.Generic;
using Avalonia.Controls;
using ScubaLog.ViewModels;
using ScubaLog.Core.Models;

namespace ScubaLog.Views;

public partial class DiveComputerImportWindow : Window
{
    public DiveComputerImportWindow()
    {
        InitializeComponent();
    }

    public List<Dive>? ImportedDives { get; private set; }

    private async void OnImport(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not DiveComputerImportViewModel vm)
        {
            Close();
            return;
        }

        ImportedDives = await vm.ImportAsync();
        Close();
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ImportedDives = null;
        Close();
    }
}
