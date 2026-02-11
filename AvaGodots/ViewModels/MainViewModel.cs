using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaGodots.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Welcome to Avalonia!";
}
