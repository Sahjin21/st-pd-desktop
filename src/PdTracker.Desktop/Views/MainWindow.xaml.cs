using System.Windows;
using PdTracker.Desktop.ViewModels;

namespace PdTracker.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = App.Services.GetService(typeof(MainViewModel)) as MainViewModel;
        DataContext = vm;
        vm!.NavigationRequested += OnNavigationRequested;
        Loaded += (_, _) => vm.OnLoaded();
    }

    private void OnNavigationRequested(object? sender, string viewName)
    {
        if (ContentArea == null) return;
        try
        {
            ContentArea.Content = viewName switch
            {
                "SearchDefendant" => App.Services.GetService(typeof(DefendantSearchView)),
                "NewApplication" => App.Services.GetService(typeof(NewApplicationView)),
                "EditAttorney" => App.Services.GetService(typeof(AttorneyListView)),
                "AddAttorney" => App.Services.GetService(typeof(AttorneyListView)),
                "SearchVoucher" => App.Services.GetService(typeof(VoucherSearchView)),
                "DefendantAZ" => App.Services.GetService(typeof(DefendantAZView)),
                _ => null
            };
        }
        catch (Exception ex)
        {
            ContentArea.Content = new System.Windows.Controls.TextBlock
            {
                Text = $"Error loading {viewName}:\n{ex}",
                Foreground = System.Windows.Media.Brushes.Red,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                TextWrapping = System.Windows.TextWrapping.Wrap
            };
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "PD Tracker v1.0\nGeorgia State Public Defender's Office\n\nMigrated from legacy Microsoft Access.",
            "About PD Tracker",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
