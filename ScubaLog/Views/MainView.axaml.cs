using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ScubaLog.Core.Importers;
using ScubaLog.Core.Models;
using ScubaLog.ViewModels;

namespace ScubaLog.Views;

public partial class MainView : UserControl
{
    private bool _isDragging;
    private Point _dragStart;
    private Thickness _detailStartMargin;

    // idle timer for auto-fade
    private readonly DispatcherTimer _fadeTimer;

    public MainView()
    {
        InitializeComponent();

        _fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)  // hide after 2s of no movement
        };
        _fadeTimer.Tick += (_, _) =>
        {
            _fadeTimer.Stop();
            HideDetail();
        };
    }

    // ------------- fade helpers -------------

    private void ShowDetail()
    {
        DetailPanel.Opacity = 1;   // transition does the fade
        _fadeTimer.Stop();
        _fadeTimer.Start();
    }

    private void HideDetail()
    {
        DetailPanel.Opacity = 0;   // fades out
    }

    private void ResetFadeTimer()
    {
        _fadeTimer.Stop();
        _fadeTimer.Start();
    }

    // ------------- graph pointer events -------------

    private void DepthProfile_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        ShowDetail();
    }

    private void DepthProfile_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        // Keep popup visible while moving over the graph
        ShowDetail();

        // Compute scrub position → HoverSample
        if (DataContext is not MainViewModel vm)
            return;

        var samples = DepthProfile.Samples;
        if (samples is null || samples.Count == 0 || vm.SelectedDive is null)
            return;

        var pos = e.GetPosition(DepthProfile);
        var width = DepthProfile.Bounds.Width;
        if (width <= 0)
            return;

        var tNorm = Math.Clamp(pos.X / width, 0.0, 1.0);

        var maxTimeMinutes = samples[^1].Time.TotalMinutes;
        if (maxTimeMinutes <= 0)
            maxTimeMinutes = 1;

        var hoverTime = TimeSpan.FromMinutes(tNorm * maxTimeMinutes);

        // NEW: tell the profile to draw the vertical line here
        DepthProfile.HoverTime = hoverTime;

        // Find nearest sample to this hover time (your existing logic)
        DiveSample? nearest = null;
        var bestDiff = double.MaxValue;

        foreach (var s in samples)
        {
            var diff = Math.Abs((s.Time - hoverTime).TotalSeconds);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                nearest = s;
            }
        }

        vm.HoverSample = nearest;
    }
    
    

    private void DepthProfile_OnPointerExited(object? sender, PointerEventArgs e)
    {
        _fadeTimer.Stop();
        HideDetail();
        DepthProfile.HoverTime = null;
    }

    // ------------- dragging the panel -------------

    private void DetailPanel_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        _dragStart = e.GetPosition(GraphHost);
        _detailStartMargin = DetailPanel.Margin;
        e.Pointer.Capture(DetailPanel);

        ShowDetail();       // make sure it’s visible while you drag
    }

    private void DetailPanel_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;

        var pos = e.GetPosition(GraphHost);
        var delta = pos - _dragStart;

        DetailPanel.Margin = new Thickness(
            _detailStartMargin.Left + delta.X,
            _detailStartMargin.Top + delta.Y,
            _detailStartMargin.Right,
            _detailStartMargin.Bottom);

        ResetFadeTimer();   // don’t fade while dragging
    }

    private void DetailPanel_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        e.Pointer.Capture(null);
        ResetFadeTimer();
    }
    
    
    
    private async void OnImportUddfClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        var storage  = topLevel?.StorageProvider;
        if (storage is null)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title         = "Import UDDF file",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("UDDF / XML")
                {
                    Patterns = new List<string> { "*.uddf", "*.xml" }
                }
            }
        });

        var file = files?.FirstOrDefault();
        if (file is null)
            return;

        var localPath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
            return;

        try
        {
            vm.ImportUddfFromPath(localPath);
            SettingsButton.Flyout?.Hide();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to import UDDF: {ex}");
        }
    }
    
    
    
    private void CloseSettings_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SettingsButton.Flyout?.Hide();
    }

    private void OnDiveDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedDive is null)
            return;

        var detail = new DiveDetailWindow
        {
            DataContext = new DiveDetailViewModel(vm.SelectedDive, vm.SelectedUnitSystem)
        };

        if (TopLevel.GetTopLevel(this) is Window owner)
            detail.ShowDialog(owner);
        else
            detail.Show();
    }

    private async void OnImportComputerClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        var computers = new[]
        {
            new DiveComputerModel("Shearwater", "Perdix"),
            new DiveComputerModel("Shearwater", "Perdix AI"),
            new DiveComputerModel("Shearwater", "Petrel 3"),
            new DiveComputerModel("Shearwater", "Teric"),
            new DiveComputerModel("Oceanic", "DG03"),
            new DiveComputerModel("Oceanic", "i300"),
            new DiveComputerModel("Oceanic", "SHEARWATER"),
            new DiveComputerModel("Hollis", "DG03")
        };

        var importer = new LibDiveComputerImporter();
        var vmImport = new DiveComputerImportViewModel(importer, computers)
        {
            SelectedManufacturer = computers.First().Manufacturer,
            SelectedModel = computers.First().Model
        };

        var dialog = new DiveComputerImportWindow
        {
            DataContext = vmImport
        };

        if (TopLevel.GetTopLevel(this) is Window owner)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();

        if (dialog.ImportedDives is { Count: > 0 })
        {
            vm.ReplaceDives(dialog.ImportedDives);
        }
    }
}
