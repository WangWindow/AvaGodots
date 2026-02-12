using AvaGodots.Models;
using AvaGodots.ViewModels.Pages;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AvaGodots.Views.Pages;

/// <summary>
/// 项目管理页面
/// </summary>
public partial class ProjectsPage : UserControl
{
    public ProjectsPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 双击项目列表项 → 用编辑器打开
    /// </summary>
    private void OnProjectDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Grid grid) return;
        if (grid.DataContext is not GodotProject project) return;
        if (DataContext is not ProjectsPageViewModel vm) return;

        vm.EditProjectCommand.Execute(project);
    }
}
