using System.Collections.Generic;
using System.Windows.Input;
using AvaGodots.Models;
using AvaGodots.ViewModels.Pages;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AvaGodots.Views.Pages;

/// <summary>
/// 编辑器管理页面
/// </summary>
public partial class EditorsPage : UserControl
{
    public EditorsPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 双击编辑器项 → 运行编辑器
    /// </summary>
    private void OnEditorDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Grid grid) return;
        if (grid.DataContext is not GodotEditor editor) return;
        if (DataContext is not EditorsPageViewModel vm) return;

        vm.RunEditorCommand.Execute(editor);
    }

    /// <summary>
    /// 右键菜单打开时通过 Tag 匹配设置 Command 和 CommandParameter
    /// </summary>
    private void OnEditorContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        if (DataContext is not EditorsPageViewModel vm) return;

        // ContextMenu 在 DataTemplate 内，尝试多个来源获取数据项
        var editor = menu.DataContext as GodotEditor
            ?? menu.PlacementTarget?.DataContext as GodotEditor;

        // 如果两者都为 null，沿着 PlacementTarget 向上遍历
        if (editor == null)
        {
            var target = menu.PlacementTarget?.Parent as Control;
            while (target != null)
            {
                if (target.DataContext is GodotEditor found) { editor = found; break; }
                target = target.Parent as Control;
            }
        }

        if (editor == null)
        {
            Services.LoggerService.Instance.Warning("EditorsPage",
                $"ContextMenu Opened: could not find GodotEditor. menu.DC={menu.DataContext?.GetType().Name}, PT.DC={menu.PlacementTarget?.DataContext?.GetType().Name}");
            return;
        }

        Services.LoggerService.Instance.Debug("EditorsPage", $"ContextMenu opened for editor: {editor.Name}");

        var commands = new Dictionary<string, ICommand>
        {
            ["RunEditor"] = vm.RunEditorCommand,
            ["ShowRenameDialog"] = vm.ShowRenameDialogCommand,
            ["ShowExtraArgsDialog"] = vm.ShowExtraArgsDialogCommand,
            ["ShowTagDialog"] = vm.ShowTagDialogCommand,
            ["ShowReferences"] = vm.ShowReferencesCommand,
            ["ShowInFileManager"] = vm.ShowInFileManagerCommand,
            ["CopyPath"] = vm.CopyPathCommand,
            ["ShowDeleteConfirm"] = vm.ShowDeleteConfirmCommand,
            ["DownloadExportTemplate"] = vm.DownloadExportTemplateCommand,
        };

        var matched = 0;
        foreach (var item in menu.Items)
        {
            if (item is MenuItem mi && mi.Tag is string tag && commands.TryGetValue(tag, out var cmd))
            {
                mi.Command = cmd;
                mi.CommandParameter = editor;
                matched++;
            }
        }
        Services.LoggerService.Instance.Debug("EditorsPage", $"Wired {matched} menu commands for '{editor.Name}'");
    }
}
