using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdTracker.Data.DbContext;
using PdTracker.Desktop.ViewModels;
using PdTracker.Desktop.Views;

namespace PdTracker.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        base.OnStartup(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Connection string — update for your SQL Server Express instance
        var connectionString =
            "Server=YOUR_SERVER\\SQLEXPRESS;Database=PdTracker;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        services.AddDbContext<PdTrackerDbContext>(options =>
            options.UseSqlServer(connectionString));

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DefendantSearchViewModel>();
        services.AddTransient<NewApplicationViewModel>();
        services.AddTransient<AttorneyListViewModel>();
        services.AddTransient<VoucherSearchViewModel>();
    }
}
