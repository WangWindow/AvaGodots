using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace AvaGodots.Views;

public partial class AssetDetailWindow : Window
{
    public AssetDetailWindow()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
