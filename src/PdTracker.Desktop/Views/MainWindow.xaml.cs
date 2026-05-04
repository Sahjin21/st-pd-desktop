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
}
