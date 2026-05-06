using System.Windows;
using PdTracker.Desktop.ViewModels;

namespace PdTracker.Desktop.Views;

public partial class MainWindow : Window
{
    private FrameworkElement? _currentView;

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

        // Unsubscribe from previous view's NavigationRequested
        if (_currentView is Views.ApplicationMenuView prevMenu)
            prevMenu.NavigationRequested -= OnSubViewNavigationRequested;

        try
        {
            _currentView = viewName switch
            {
                "SearchDefendant" => (FrameworkElement)App.Services.GetService(typeof(DefendantSearchView))!,
                "NewApplication" => (FrameworkElement)App.Services.GetService(typeof(NewApplicationView))!,
                "EditAttorney" => (FrameworkElement)App.Services.GetService(typeof(AttorneyListView))!,
                "AddAttorney" => (FrameworkElement)SetViewParameterAndNavigate("AddAttorney"),
                "SearchVoucher" => (FrameworkElement)App.Services.GetService(typeof(VoucherSearchView))!,
                "DefendantAZ" => (FrameworkElement)App.Services.GetService(typeof(DefendantAZView))!,
                "ApplicationMenu" => (FrameworkElement)App.Services.GetService(typeof(ApplicationMenuView))!,

                // Routes from Application Menu sub-buttons
                "NoAction" => MakeStub("No Action", "Mark an application as having no further action taken."),
                "AddAction" => MakeStub("Add Action", "Record a new action taken on an application."),
                "EditAction" => MakeStub("Edit Action", "Edit an existing action on an application."),
                "AppointAdult" => MakeStub("Appoint Adult", "Create an adult appointment voucher (NEWVOUCHER_STEP1)."),
                "AddEditEIAResult" => MakeStub("Add/Edit EIA Result", "Add or edit EIA eligibility and plea information."),
                "EIAPleaSheet" => MakeStub("EIA Plea Sheet", "Print the EIA Plea Sheet report."),
                "SearchPaidVoucher" => MakeStub("Search Paid Voucher", "Search for paid/submitted vouchers (Voucher_Paid form)."),
                "SearchAKA" => MakeStub("Search AKA", "Search defendant aliases (search_Alias form)."),
                "AddPaidVoucher" => MakeStub("Add Paid Voucher", "Add a paid voucher entry (Voucher Payable Add form)."),
                "AddVisitation" => MakeStub("Add Visitation", "Record a visitation entry."),
                "CDNumberReverse" => MakeStub("CD Number Reverse Order", "View applications sorted by CD number (Qualify) in descending order."),

                // Left nav stubs / mode-setting routes
                "EditApplication" => SetSearchViewMode("Edit"),
                "SearchByBooking" => SetSearchViewMode("ReadOnly"),
                "SearchChild" => SetSearchViewMode("Juvenile"),

                // Right nav category stubs
                "DocumentMenu" => MakeStub("Document Menu", "Generate and manage legal documents."),
                "Professional" => MakeStub("Professional", "Professional fee and voucher management."),
                "ReportsMenu" => MakeStub("Reports Menu", "Run reports against the database."),
                "AdministratorMenu" => MakeStub("Administrator Menu", "Administrative settings and user management."),

                _ => null
            };

            if (_currentView == null)
            {
                ContentArea.Content = null;
                return;
            }

            ContentArea.Content = _currentView;

            // Subscribe to NavigationRequested if it's an ApplicationMenuView
            if (_currentView is Views.ApplicationMenuView menuView)
                menuView.NavigationRequested += OnSubViewNavigationRequested;
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

    /// <summary>
    /// Handles navigation events bubbling up from sub-views (e.g. ApplicationMenuView).
    /// "BackToMain" returns to the main SearchDefendant view.
    /// </summary>
    private void OnSubViewNavigationRequested(object? sender, string route)
    {
        if (route == "BackToMain")
        {
            OnNavigationRequested(this, "SearchDefendant");
        }
        else
        {
            OnNavigationRequested(this, route);
        }
    }

    private object SetViewParameterAndNavigate(string key)
    {
        App.ViewParameters[key] = "true";
        return App.Services.GetService(typeof(AttorneyListView))!;
    }

    /// <summary>
    /// Returns DefendantSearchView with a mode flag set (Edit/ReadOnly/Juvenile).
    /// </summary>
    private FrameworkElement SetSearchViewMode(string mode)
    {
        App.ViewParameters["DefendantSearchMode"] = mode;
        return (FrameworkElement)App.Services.GetService(typeof(DefendantSearchView))!;
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

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "PD Tracker v1.0\nGeorgia State Public Defender's Office\n\nMigrated from legacy Microsoft Access.",
            "About PD Tracker",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
