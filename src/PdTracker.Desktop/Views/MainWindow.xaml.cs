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
                "AddAttorney" => SetViewParameterAndNavigate("AddAttorney"),
                "SearchVoucher" => App.Services.GetService(typeof(VoucherSearchView)),
                "DefendantAZ" => App.Services.GetService(typeof(DefendantAZView)),
                // Stubs — wire up real views when ready
                "EditApplication" => MakeStub("Edit Application", "Open an existing application to review or update its details."),
                "SearchByBooking" => App.Services.GetService(typeof(DefendantSearchView)),
                "SearchChild" => MakeStub("Search Child", "Search for juvenile case records associated with a defendant."),
                "ApplicationMenu" => MakeStub("Application Menu", "Manage application templates and settings."),
                "DocumentMenu" => MakeStub("Document Menu", "Generate and manage legal documents."),
                "Professional" => MakeStub("Professional", "Professional fee and voucher management."),
                "ReportsMenu" => App.Services.GetService(typeof(DefendantSearchView)),
                "AdministratorMenu" => MakeStub("Administrator Menu", "Administrative settings and user management."),
                "CDNumberReverse" => MakeStub("CD Number Reverse Order", "View applications sorted by CD number in descending order."),
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

    private object SetViewParameterAndNavigate(string key)
    {
        App.ViewParameters[key] = "true";
        return App.Services.GetService(typeof(AttorneyListView))!;
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "PD Tracker v1.0\nGeorgia State Public Defender's Office\n\nMigrated from legacy Microsoft Access.",
            "About PD Tracker",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static System.Windows.Controls.TextBlock MakeStub(string title, string description)
        => new()
        {
            Text = $"{title}\n\n{description}\n\nThis feature is coming soon.",
            FontSize = 16,
            Foreground = System.Windows.Media.Brushes.Gray,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            TextWrapping = System.Windows.TextWrapping.Wrap,
            VerticalAlignment = System.Windows.VerticalAlignment.Top,
            Margin = new Thickness(0, 20, 0, 0)
        };
}
