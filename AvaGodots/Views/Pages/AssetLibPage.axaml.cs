using System;
using AvaGodots.Models;
using AvaGodots.ViewModels;
using AvaGodots.ViewModels.Pages;
using Avalonia.Controls;

namespace AvaGodots.Views.Pages;

public partial class AssetLibPage : UserControl
{
    public AssetLibPage()
    {
        InitializeComponent();
        DataContextChanged += AssetLibPage_DataContextChanged;
    }

    private void AssetLibPage_DataContextChanged(object? sender, EventArgs e)
    {
        if (sender is not AssetLibPage pag) return;

        if (pag.DataContext is AssetLibPageViewModel vm)
        {
            vm.ShowDetailWindowRequested += OnShowDetailWindow;
            _ = vm.InitializeCommand.ExecuteAsync(null);
        }
    }

    private void OnShowDetailWindow(AssetLibItem item)
    {
        if (DataContext is not AssetLibPageViewModel vm) return;

        var detailVm = new AssetDetailViewModel(vm, item);
        var window = new AssetDetailWindow
        {
            DataContext = detailVm
        };

        detailVm.RequestClose += () => window.Close();

        var topLevel = TopLevel.GetTopLevel(this) as Window;
        if (topLevel != null)
            window.ShowDialog(topLevel);
        else
            window.Show();
    }
}
