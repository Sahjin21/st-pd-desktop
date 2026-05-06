using System.Windows;
using System.Windows.Controls;

namespace PdTracker.Desktop.Views;

public partial class ApplicationMenuView : UserControl
{
    public event EventHandler<string>? NavigationRequested;

    public ApplicationMenuView()
    {
        InitializeComponent();
    }

    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string route)
            NavigationRequested?.Invoke(this, route);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        NavigationRequested?.Invoke(this, "BackToMain");
    }
}
