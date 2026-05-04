using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdTracker.Data.DbContext;
using PdTracker.Desktop.ViewModels;
using PdTracker.Desktop.Views;

namespace PdTracker.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static string ConnectionString { get; private set; } = string.Empty;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Global exception handlers — catch everything so the window doesn't just vanish
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            ShowError("AppDomain Error", args.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, args) =>
        {
            ShowError("Unhandled Error", args.Exception);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ShowError("Background Error", args.Exception);
            args.SetObserved();
        };

        base.OnStartup(e);

        // Show connection setup first — don't even try to start without a valid connection
        var setupWindow = new ConnectionSetupWindow();
        if (setupWindow.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        ConnectionString = setupWindow.ConnectionString;
        ConfigureServices();

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    private static void ConfigureServices()
    {
        var services = new ServiceCollection();

        // AddDbContextFactory registers IDbContextFactory<PdTrackerDbContext> (what ViewModels use)
        // It also registers DbContextOptions<PdTrackerDbContext> internally
        services.AddDbContextFactory<PdTrackerDbContext>(options =>
            options.UseSqlServer(ConnectionString));

        // Register ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DefendantSearchViewModel>();
        services.AddTransient<NewApplicationViewModel>();
        services.AddTransient<AttorneyListViewModel>();
        services.AddTransient<VoucherSearchViewModel>();

        Services = services.BuildServiceProvider();

        // Validate connection before showing the main window
        try
        {
            using var scope = Services.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PdTrackerDbContext>>();
            using var db = dbFactory.CreateDbContext();
            db.Database.CanConnect().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            ShowError("Database Connection Failed",
                new Exception($"Could not connect to SQL Server with the provided connection string.\n\n{ex.Message}", ex));
            Current.Shutdown(1);
        }
    }

    private static void ShowError(string title, Exception? ex)
    {
        var msg = ex?.Message ?? "Unknown error";
        var details = ex?.ToString() ?? "";
        MessageBox.Show($"{msg}\n\nFull details:\n{details}",
            title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
