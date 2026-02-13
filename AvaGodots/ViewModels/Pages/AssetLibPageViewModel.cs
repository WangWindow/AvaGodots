using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaGodots.Interfaces;
using AvaGodots.Models;
using AvaGodots.Services;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaGodots.ViewModels.Pages;

/// <summary>
/// 分页页码项 (1-indexed 显示值 + 0-indexed 内部索引)
/// DisplayNumber == -1 表示省略号占位符
/// </summary>
public record PageItem(int DisplayNumber, int PageIndex, bool IsCurrent);

/// <summary>
/// PageItem 的值转换工具 (用于 AXAML 中控制省略号可见性)
/// </summary>
public static class PageItemConverters
{
    public static FuncValueConverter<int, bool> IsEllipsis { get; } =
        new(v => v == -1);

    public static FuncValueConverter<int, bool> IsNotEllipsis { get; } =
        new(v => v != -1);
}

/// <summary>
/// 资源库页面视图模型
/// 对应 godots 的 AssetLib 标签页
/// 从 godotengine.org/asset-library/api 获取项目模板、示例、插件
/// </summary>
public partial class AssetLibPageViewModel : ViewModelBase
{
    private readonly AssetLibService _assetLibService;
    private readonly IEditorService? _editorService;
    private CancellationTokenSource? _searchCts;

    // ========== 搜索/筛选 ==========

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedVersion = "";

    [ObservableProperty]
    private string _selectedSort = "Recently Updated";

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private string _selectedSite = "godotengine.org (Official)";

    // ========== 分页 ==========

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private int _currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private int _totalPages;

    [ObservableProperty]
    private int _totalItems;

    public bool CanGoPrevious => CurrentPage > 0;
    public bool CanGoNext => CurrentPage < TotalPages - 1;

    // ========== 状态 ==========

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _noEditorsInstalled;

    [ObservableProperty]
    private string _statusText = string.Empty;

    // ========== 数据 ==========

    public ObservableCollection<AssetLibItem> Assets { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["All"];
    public ObservableCollection<PageItem> PageNumbers { get; } = [];

    public ObservableCollection<string> VersionOptions { get; } = [""];
    public string[] SortOptions { get; } = ["Recently Updated", "Least Recently Updated", "Name (A-Z)", "Name (Z-A)", "License (A-Z)", "License (Z-A)"];
    public string[] SiteOptions { get; } = ["godotengine.org (Official)"];

    public AssetLibPageViewModel()
    {
        _assetLibService = new AssetLibService();
    }

    public AssetLibPageViewModel(DatabaseService db, IEditorService? editorService = null)
    {
        _assetLibService = new AssetLibService(db);
        _editorService = editorService;
    }

    /// <summary>
    /// 注入数据库服务（用于缓存）
    /// </summary>
    public void SetDatabase(DatabaseService db) => _assetLibService.SetDatabase(db);

    // ========== 初始化 ==========

    [RelayCommand]
    private async Task InitializeAsync()
    {
        RefreshVersionOptions();
        await LoadCategoriesAsync();
        await SearchAsync();

        _ = Task.Run(async () =>
        {
            try { await _assetLibService.PreCacheAsync(3); }
            catch { }
        });
    }

    /// <summary>根据已安装的编辑器版本构建筛选选项</summary>
    public void RefreshVersionOptions()
    {
        VersionOptions.Clear();
        VersionOptions.Add(""); // “所有版本”

        if (_editorService is { Editors.Count: > 0 })
        {
            var versions = new HashSet<string>();
            foreach (var editor in _editorService.Editors)
            {
                var ver = ExtractMajorMinor(editor.VersionHint);
                if (!string.IsNullOrEmpty(ver)) versions.Add(ver);
            }
            foreach (var v in versions.OrderByDescending(v => v))
                VersionOptions.Add(v);
            NoEditorsInstalled = false;
        }
        else
        {
            NoEditorsInstalled = true;
        }
    }

    private static string ExtractMajorMinor(string versionHint)
    {
        var ver = versionHint.TrimStart('v').Split('-')[0];
        var parts = ver.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : ver;
    }

    private async Task LoadCategoriesAsync()
    {
        var cats = await _assetLibService.GetCategoriesAsync();
        Categories.Clear();
        Categories.Add("All");
        foreach (var cat in cats.Where(c => c.Type == "0" || c.Type == "project" || c.Type == ""))
        {
            Categories.Add(cat.Name);
        }
    }

    // ========== 搜索 ==========

    partial void OnSearchTextChanged(string value)
    {
        _ = DebouncedSearchAsync();
    }

    partial void OnSelectedVersionChanged(string value) => _ = SearchAsync();
    partial void OnSelectedSortChanged(string value) => _ = SearchAsync();
    partial void OnSelectedCategoryChanged(string value) => _ = SearchAsync();

    private async Task DebouncedSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(300, _searchCts.Token);
            await SearchAsync();
        }
        catch (TaskCanceledException) { }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsLoading = true;
        StatusText = LocalizationService.GetString("AssetLib.Status.Searching", "Searching...");

        try
        {
            var sortApi = SelectedSort switch
            {
                "Recently Updated" => "updated",
                "Least Recently Updated" => "updated",
                "Name (A-Z)" => "name",
                "Name (Z-A)" => "name",
                "License (A-Z)" => "cost",
                "License (Z-A)" => "cost",
                _ => "updated"
            };

            var categoryId = 0;
            if (SelectedCategory != "All")
            {
                var cats = await _assetLibService.GetCategoriesAsync();
                var cat = cats.FirstOrDefault(c => c.Name == SelectedCategory);
                if (cat != null && int.TryParse(cat.Id, out var id))
                    categoryId = id;
            }

            var result = await _assetLibService.SearchAssetsAsync(
                filter: SearchText,
                godotVersion: SelectedVersion,
                sort: sortApi,
                category: categoryId,
                page: CurrentPage,
                maxResults: 40
            );

            Assets.Clear();
            foreach (var item in result.Result)
            {
                Assets.Add(item);
            }

            TotalPages = result.Pages;
            TotalItems = result.TotalItems;
            UpdatePageNumbers();

            IsEmpty = Assets.Count == 0;
            var foundTemplate = LocalizationService.GetString("AssetLib.Status.Found", "Found {0} assets");
            StatusText = string.Format(foundTemplate, TotalItems);
        }
        catch (Exception ex)
        {
            var errorTemplate = LocalizationService.GetString("AssetLib.Status.Error", "Error: {0}");
            StatusText = string.Format(errorTemplate, ex.Message);
            IsEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdatePageNumbers()
    {
        PageNumbers.Clear();
        if (TotalPages <= 0) return;

        var start = Math.Max(0, CurrentPage - 2);
        var end = Math.Min(TotalPages - 1, CurrentPage + 2);

        // 始终显示第一页
        if (start > 0)
        {
            PageNumbers.Add(new PageItem(1, 0, CurrentPage == 0));
            if (start > 1)
                PageNumbers.Add(new PageItem(-1, -1, false)); // 省略号占位 (DisplayNumber = -1)
        }

        for (var i = start; i <= end; i++)
            PageNumbers.Add(new PageItem(i + 1, i, i == CurrentPage));

        // 始终显示最后一页
        if (end < TotalPages - 1)
        {
            if (end < TotalPages - 2)
                PageNumbers.Add(new PageItem(-1, -1, false)); // 省略号占位
            PageNumbers.Add(new PageItem(TotalPages, TotalPages - 1, CurrentPage == TotalPages - 1));
        }
    }

    // ========== 分页 ==========

    [RelayCommand]
    private async Task GoToPageAsync(int page)
    {
        if (page < 0 || page >= TotalPages) return;
        CurrentPage = page;
        await SearchAsync();
    }

    [RelayCommand]
    private async Task FirstPageAsync() => await GoToPageAsync(0);

    [RelayCommand]
    private async Task PreviousPageAsync() => await GoToPageAsync(CurrentPage - 1);

    [RelayCommand]
    private async Task NextPageAsync() => await GoToPageAsync(CurrentPage + 1);

    [RelayCommand]
    private async Task LastPageAsync() => await GoToPageAsync(TotalPages - 1);

    // ========== 素材操作 ==========

    [RelayCommand]
    private void OpenAssetInBrowser(AssetLibItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.BrowseUrl)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = item.BrowseUrl,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
