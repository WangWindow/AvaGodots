using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AvaGodots.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaGodots.ViewModels
{
    /// <summary>
    /// 负责 AssetDetailWindow 的数据与命令。
    /// 窗口本身只保留非常少的代码，仅用于绑定和关闭逻辑。
    /// </summary>
    public partial class AssetDetailViewModel : ViewModelBase
    {
        private readonly ViewModels.Pages.AssetLibPageViewModel _parentVm;
        private AssetLibItem _item;

        // 元信息
        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private string _category = string.Empty;
        [ObservableProperty] private string _author = string.Empty;
        [ObservableProperty] private string _license = string.Empty;
        [ObservableProperty] private string _versionString = string.Empty;
        [ObservableProperty] private string _description = string.Empty;
        [ObservableProperty] private string _iconUrl = string.Empty;

        /// <summary>
        /// True when <see cref="IconUrl"/> is null or empty, used by the view to show
        /// the placeholder icon without relying on a converter.
        /// </summary>
        public bool IconUrlIsNullOrEmpty => string.IsNullOrEmpty(IconUrl);

        partial void OnIconUrlChanged(string value)
        {
            // update the derived property
            OnPropertyChanged(nameof(IconUrlIsNullOrEmpty));
        }

        // 预览
        public ObservableCollection<AssetPreview> Previews { get; } = [];

        [ObservableProperty] private AssetPreview? _selectedPreview;

        [ObservableProperty] private string _selectedPreviewUrl = string.Empty;

        /// <summary>
        /// Indicates whether the selected preview URL is null or empty; used to display
        /// the placeholder image in the big preview area.
        /// </summary>
        public bool SelectedPreviewUrlIsNullOrEmpty => string.IsNullOrEmpty(SelectedPreviewUrl);

        partial void OnSelectedPreviewUrlChanged(string value)
        {
            OnPropertyChanged(nameof(SelectedPreviewUrlIsNullOrEmpty));
        }

        [ObservableProperty] private bool _previewIsVideo;

        // 窗口状态
        [ObservableProperty] private bool _isLoading;

        public bool IsContentVisible => !IsLoading;

        // 下载进度/按钮
        [ObservableProperty] private bool _progressVisible;

        [ObservableProperty] private double _progressValue;

        [ObservableProperty] private string _progressStatus = string.Empty;

        [ObservableProperty] private bool _downloadEnabled = true;

        // 关闭事件，由 view 层挂载
        public event Action? RequestClose;

        public AssetDetailViewModel(ViewModels.Pages.AssetLibPageViewModel parentVm, AssetLibItem item)
        {
            _parentVm = parentVm;
            _item = item;

            // copy basic info immediately so UI can show something
            Title = item.Title;
            Category = item.Category;
            Author = item.Author;
            License = item.Cost;
            VersionString = item.VersionString;
            Description = StripBbCode(item.Description ?? string.Empty);
            IconUrl = item.IconUrl;

            // kick off async load of full detail
            _ = LoadDetailAsync();
        }

        private async Task LoadDetailAsync()
        {
            IsLoading = true;
            try
            {
                var detail = await _parentVm.GetAssetDetailAsync(_item);
                if (detail != null)
                {
                    _item = detail;
                    Title = detail.Title;
                    Category = detail.Category;
                    Author = detail.Author;
                    License = detail.Cost;
                    VersionString = detail.VersionString;
                    IconUrl = detail.IconUrl;
                    Description = StripBbCode(detail.Description ?? string.Empty);
                    Previews.Clear();
                    if (detail.Previews != null)
                    {
                        foreach (var p in detail.Previews)
                            Previews.Add(p);

                        // select first non-video
                        var first = detail.Previews.Find(p => !p.IsVideo);
                        if (first != null)
                            SelectPreview(first);
                    }
                }
            }
            catch
            {
                // ignore - keep whatever data we had
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsContentVisible));
            }
        }

        [RelayCommand]
        private void SelectPreview(AssetPreview? preview)
        {
            if (preview == null)
                return;

            if (preview.IsVideo)
            {
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

            SelectedPreview = preview;
            SelectedPreviewUrl = preview.Link;
            PreviewIsVideo = false;
        }

        [RelayCommand]
        private void Close() => RequestClose?.Invoke();

        [RelayCommand]
        private void ViewInBrowser()
        {
            if (string.IsNullOrEmpty(_item.BrowseUrl)) return;
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

        [RelayCommand]
        private async Task DownloadAsync()
        {
            if (_parentVm == null) return;
            DownloadEnabled = false;
            ProgressVisible = true;

            var progress = new Progress<(double percent, string status)>(p =>
            {
                ProgressValue = p.percent;
                ProgressStatus = p.status;
                _parentVm.UpdateToastProgress(p.percent, p.status);
            });

            _parentVm.ShowDownloadToast(_item, progress);
            var success = await _parentVm.DownloadAndInstallWithProgressAsync(progress);
            if (success)
            {
                _ = _parentVm.ShowToastInstalledAsync();
                await Task.Delay(1200);
                RequestClose?.Invoke();
            }
            else
            {
                DownloadEnabled = true;
            }
        }

        private static string StripBbCode(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var result = Regex.Replace(text, @"\[url=[^\]]*\](.*?)\[/url\]", "$1", RegexOptions.Singleline);
            result = Regex.Replace(result, @"\[url\](.*?)\[/url\]", "$1", RegexOptions.Singleline);
            result = Regex.Replace(result, @"\[img\](.*?)\[/img\]", "$1", RegexOptions.Singleline);
            result = Regex.Replace(result, @"\[/?[^\]]+\]", string.Empty, RegexOptions.Singleline);
            return result.Trim();
        }
    }
}
