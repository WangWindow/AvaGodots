using AvaGodots.ViewModels.Pages;
using Avalonia.Controls;

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
                await vm.InitializeCommand.ExecuteAsync(null);
            }
        };
    }
}
