using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AsyncImageLoader;
using AvaGodots.Models;
using AvaGodots.ViewModels.Pages;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AvaGodots.Views;

public partial class AssetDetailWindow : Window
{
    private AssetLibPageViewModel? _vm;
    private AssetLibItem? _item;
    private readonly List<Button> _thumbButtons = [];

    public AssetDetailWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 初始化窗口并加载素材详情（模仿 Godots AssetLibItemDetailsDialog）
    /// </summary>
    public async void Initialize(AssetLibPageViewModel vm, AssetLibItem item)
    {
        _vm = vm;
        _item = item;

        // 设置窗口标题
        Title = item.Title;

        // 基本信息
        TitleText.Text = item.Title;
        CategoryText.Text = item.Category;
        AuthorText.Text = item.Author;
        LicenseText.Text = item.Cost;
        VersionText.Text = item.VersionString;

        // 加载图标
        LoadIcon(item.IconUrl);

        LoadingPanel.IsVisible = true;
        ContentPanel.IsVisible = false;

        // 异步获取完整信息
        try
        {
            var detail = await vm.GetAssetDetailAsync(item);
            if (detail != null)
            {
                _item = detail;
                TitleText.Text = detail.Title;
                Title = detail.Title;
                VersionText.Text = detail.VersionString;
                CategoryText.Text = detail.Category;
                AuthorText.Text = detail.Author;
                LicenseText.Text = detail.Cost;
                DescriptionText.Text = StripBbCode(detail.Description ?? string.Empty);

                // 加载预览图
                LoadPreviews(detail.Previews);
            }
            else
            {
                DescriptionText.Text = StripBbCode(item.Description ?? string.Empty);
            }
        }
        catch
        {
            DescriptionText.Text = StripBbCode(item.Description ?? string.Empty);
        }
        finally
        {
            LoadingPanel.IsVisible = false;
            ContentPanel.IsVisible = true;
        }
    }

    private async void LoadIcon(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            var bitmap = await ImageLoader.AsyncImageLoader.ProvideImageAsync(url);
            if (bitmap != null)
            {
                IconImage.Source = bitmap;
                IconPlaceholder.IsVisible = false;
            }
        }
        catch { /* 忽略 */ }
    }

    /// <summary>
    /// 加载预览图缩略图列表 + 自动选中第一张图片
    /// </summary>
    private void LoadPreviews(List<AssetPreview>? previews)
    {
        if (previews == null || previews.Count == 0)
        {
            PreviewPlaceholder.IsVisible = true;
            return;
        }

        PreviewPlaceholder.IsVisible = false;
        var firstNonVideoSelected = false;

        foreach (var preview in previews)
        {
            var btn = new Button
            {
                Width = 100,
                Height = 70,
                Classes = { "previewThumb" },
                Tag = preview
            };

            // 加载缩略图
            var thumbImage = new Image
            {
                Width = 96,
                Height = 66,
                Stretch = Stretch.UniformToFill
            };
            btn.Content = thumbImage;
            LoadThumbnail(thumbImage, preview.Thumbnail);

            btn.Click += (_, _) => OnPreviewThumbClick(preview, btn);

            _thumbButtons.Add(btn);
            PreviewThumbnails.Children.Add(btn);

            // 选中第一个非视频预览
            if (!firstNonVideoSelected && !preview.IsVideo)
            {
                firstNonVideoSelected = true;
                // 延迟选中以确保控件已就绪
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    OnPreviewThumbClick(preview, btn));
            }
        }
    }

    private async void LoadThumbnail(Image img, string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            var bitmap = await ImageLoader.AsyncImageLoader.ProvideImageAsync(url);
            if (bitmap != null) img.Source = bitmap;
        }
        catch { /* 忽略 */ }
    }

    private void OnPreviewThumbClick(AssetPreview preview, Button clickedBtn)
    {
        if (preview.IsVideo)
        {
            // 视频：打开外部浏览器
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = preview.Link,
                    UseShellExecute = true
                });
            }
            catch { }
            return;
        }

        // 选中高亮
        foreach (var btn in _thumbButtons)
        {
            btn.Classes.Remove("selected");
        }
        clickedBtn.Classes.Add("selected");

        // 加载大预览图
        LoadPreviewFull(preview.Link);
    }

    private async void LoadPreviewFull(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            var bitmap = await ImageLoader.AsyncImageLoader.ProvideImageAsync(url);
            if (bitmap != null)
            {
                PreviewImage.Source = bitmap;
                PreviewPlaceholder.IsVisible = false;
            }
        }
        catch { /* 忽略 */ }
    }

    /// <summary>
    /// 简单去除 BBCode 标签（asset library description 常包含 [url][/url] 等标记）
    /// </summary>
    private static string StripBbCode(string text)
    {
        // 移除 [tag]...[/tag] 和 [tag=...] 标记
        return System.Text.RegularExpressions.Regex.Replace(text, @"\[/?[a-zA-Z_][a-zA-Z0-9_=]*\]", "").Trim();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void OnViewInBrowserClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_item == null || string.IsNullOrEmpty(_item.BrowseUrl)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _item.BrowseUrl,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private async void OnDownloadClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm == null) return;

        DownloadButton.IsEnabled = false;
        ProgressPanel.IsVisible = true;

        var progress = new Progress<(double percent, string status)>(p =>
        {
            ProgressBar.Value = p.percent;
            ProgressText.Text = p.status;
        });

        var success = await _vm.DownloadAndInstallWithProgressAsync(progress);

        if (success)
        {
            await Task.Delay(1200);
            Close();
        }
        else
        {
            DownloadButton.IsEnabled = true;
        }
    }
}
