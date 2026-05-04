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
        if (ContentArea == null) return; // Safety guard
        ContentArea.Content = null;
        object? vm = viewName switch
        {
            "SearchDefendant" => App.Services.GetService(typeof(DefendantSearchViewModel)),
            "NewApplication" => App.Services.GetService(typeof(NewApplicationViewModel)),
            "EditAttorney" => App.Services.GetService(typeof(AttorneyListViewModel)),
            "AddAttorney" => App.Services.GetService(typeof(AttorneyListViewModel)),
            "SearchVoucher" => App.Services.GetService(typeof(VoucherSearchViewModel)),
            "DefendantAZ" => new Views.DefendantAZView(),
            _ => null
        };

        if (vm is FrameworkElement fe)
        {
            ContentArea.Content = fe;
        }
    }
}
