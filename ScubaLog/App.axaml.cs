using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using ScubaLog.Core.Importers;
using ScubaLog.Core.Services;
using ScubaLog.ViewModels;
using ScubaLog.Views;

namespace ScubaLog;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Create the core service
        var diveLogService = new DiveLogService();

        // TODO: change this to your actual MacDiveBase.sqlite path
        const string dbPath = "/Users/mcarr/workspace/decompile/MacDive/MacDive.sqlite";

        // If a MacDive database exists, import real dives instead of demo data
        if (File.Exists(dbPath))
        {
            var importer      = new MacDiveImporter(dbPath);
            var importedDives = importer.ImportAllDives();

            diveLogService.Dives.Clear();
            diveLogService.Dives.AddRange(importedDives);
        }

        var mainVm = new MainViewModel(diveLogService);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Remove Avalonia default data validation (CommunityToolkit handles it)
            BindingPlugins.DataValidators.RemoveAt(0);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = mainVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}