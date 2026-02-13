using System;
using System.Threading.Tasks;
using AsyncImageLoader;
using AvaGodots.Models;
using AvaGodots.Services;
using AvaGodots.ViewModels.Pages;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AvaGodots.Views.Pages;

public partial class AssetLibPage : UserControl
{
    public AssetLibPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is AssetLibPageViewModel vm)
            {
                vm.ShowDetailWindowRequested += OnShowDetailWindow;
                await vm.InitializeCommand.ExecuteAsync(null);
            }
        };

        DismissToastButton.Click += (_, _) => DownloadToast.IsVisible = false;
    }

    private void OnShowDetailWindow(AssetLibItem item)
    {
        if (DataContext is not AssetLibPageViewModel vm) return;

        var window = new AssetDetailWindow();
        var topLevel = TopLevel.GetTopLevel(this) as Window;
        if (topLevel != null)
        {
            window.ShowDialog(topLevel);
        }
        else
        {
            window.Show();
        }
        window.Initialize(vm, item);
    }

    /// <summary>
    /// 显示下载 toast 通知（仿 Godots 底部左下角通知栏）
    /// </summary>
    public void ShowDownloadToast(AssetLibItem item, IProgress<(double percent, string status)> progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ToastTitle.Text = item.Title;
            ToastStatus.Text = LocalizationService.GetString("AssetLib.Status.Downloading", "Downloading...");
            ToastProgress.Value = 0;
            ToastInstallButton.IsVisible = false;
            DownloadToast.IsVisible = true;

            // 加载图标
            _ = LoadToastIcon(item.IconUrl);
        });
    }

    public void UpdateToastProgress(double percent, string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ToastProgress.Value = percent;
            ToastStatus.Text = status;
        });
    }

    public void ShowToastReadyToInstall()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ToastStatus.Text = LocalizationService.GetString("AssetLib.Toast.ReadyToInstall", "Ready to install");
            ToastProgress.Value = 100;
            ToastInstallButton.IsVisible = true;
        });
    }

    public void ShowToastInstalled()
    {
        Dispatcher.UIThread.Post(async () =>
        {
            ToastStatus.Text = LocalizationService.GetString("AssetLib.Status.Installed", "Installed successfully!");
            ToastInstallButton.IsVisible = false;
            await Task.Delay(3000);
            DownloadToast.IsVisible = false;
        });
    }

    private async Task LoadToastIcon(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            var bitmap = await ImageLoader.AsyncImageLoader.ProvideImageAsync(url);
            if (bitmap != null) ToastIcon.Source = bitmap;
        }
        catch { /* 忽略 */ }
    }
}
