using AvaGodots.Models;
using AvaGodots.ViewModels.Pages;
using Avalonia.Controls;
using Avalonia.Input;

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
}
