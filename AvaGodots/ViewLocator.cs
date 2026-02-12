using AvaGodots.ViewModels;
using AvaGodots.ViewModels.Pages;
using AvaGodots.Views;
using AvaGodots.Views.Pages;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace AvaGodots;

/// <summary>
/// Explicit ViewModel → View mapping to avoid name-based reflection lookups.
/// Register mappings here so the application can create views for known viewmodels.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param) => param switch
    {
        null => null,
        MainViewModel => new MainView(),
        ProjectsPageViewModel => new ProjectsPage(),
        AssetLibPageViewModel => new AssetLibPage(),
        EditorsPageViewModel => new EditorsPage(),
        SettingsPageViewModel => new SettingsPage(),
        _ => new TextBlock { Text = $"View not implemented for: {param.GetType().FullName}" }
    };

    public bool Match(object? data) => data is ViewModelBase;
}
