using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PdTracker.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public event EventHandler<string>? NavigationRequested;

    [RelayCommand]
    void Navigate(string destination) => NavigationRequested?.Invoke(this, destination);

    [RelayCommand]
    void Exit() => System.Windows.Application.Current.Shutdown();

    public void OnLoaded()
    {
        Navigate("SearchDefendant");
    }
}
