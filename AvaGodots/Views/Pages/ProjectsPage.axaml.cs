using System.Collections.Generic;
using System.Windows.Input;
using AvaGodots.Models;
using AvaGodots.ViewModels.Pages;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

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

    /// <summary>
    /// 右键菜单打开时通过 Tag 匹配设置 Command 和 CommandParameter
    /// </summary>
    private void OnProjectContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        if (DataContext is not ProjectsPageViewModel vm) return;

        // ContextMenu 在 DataTemplate 内，尝试多个来源获取数据项
        var project = menu.DataContext as GodotProject
            ?? menu.PlacementTarget?.DataContext as GodotProject;

        if (project == null)
        {
            var target = menu.PlacementTarget?.Parent as Control;
            while (target != null)
            {
                if (target.DataContext is GodotProject found) { project = found; break; }
                target = target.Parent as Control;
            }
        }

        if (project == null)
        {
            Services.LoggerService.Instance.Warning("ProjectsPage",
                $"ContextMenu Opened: could not find GodotProject. menu.DC={menu.DataContext?.GetType().Name}, PT.DC={menu.PlacementTarget?.DataContext?.GetType().Name}");
            return;
        }

        Services.LoggerService.Instance.Debug("ProjectsPage", $"ContextMenu opened for project: {project.Name}");

        var commands = new Dictionary<string, ICommand>
        {
            ["EditProject"] = vm.EditProjectCommand,
            ["RunProject"] = vm.RunProjectCommand,
            ["ShowBindEditorDialog"] = vm.ShowBindEditorDialogCommand,
            ["ShowTagDialog"] = vm.ShowTagDialogCommand,
            ["ShowDuplicateDialog"] = vm.ShowDuplicateDialogCommand,
            ["ShowRenameDialog"] = vm.ShowRenameDialogCommand,
            ["ShowInFileManager"] = vm.ShowInFileManagerCommand,
            ["CopyPath"] = vm.CopyPathCommand,
            ["ShowDeleteConfirm"] = vm.ShowDeleteConfirmCommand,
        };

        var matched = 0;
        foreach (var item in menu.Items)
        {
            if (item is MenuItem mi && mi.Tag is string tag && commands.TryGetValue(tag, out var cmd))
            {
                mi.Command = cmd;
                mi.CommandParameter = project;
                matched++;
            }
        }
        Services.LoggerService.Instance.Debug("ProjectsPage", $"Wired {matched} menu commands for '{project.Name}'");
    }
}
