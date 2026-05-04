using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdTracker.Data.DbContext;
using PdTracker.Desktop.Services;
using PdTracker.Desktop.ViewModels;
using PdTracker.Desktop.Views;

namespace PdTracker.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static readonly string SettingsPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PdTracker", "settings.json");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Global exception handlers
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

        // Load settings
        var settings = LoadSettings();
        string sqlitePath = settings.SqlitePath ?? "";

        // If sqlite file doesn't exist, need to set up (either first run or file deleted/moved)
        if (!File.Exists(sqlitePath))
        {
            // Always show file picker — user picks either .accdb (to migrate) or .sqlite (existing db)
            var picker = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select PD Tracker Database — pick the .accdb to migrate, or an existing .sqlite file",
                Filter =
                    "Access Database (*.accdb;*.mdb)|*.accdb;*.mdb|" +
                    "SQLite Database (*.sqlite;*.sqlite3)|*.sqlite;*.sqlite3|" +
                    "All Files (*.*)|*.*",
            };

            if (picker.ShowDialog() != true)
            {
                Current.Shutdown();
                return;
            }

            sqlitePath = picker.FileName;

            // If user picked an .accdb / .mdb → one-time migration to .sqlite
            var ext = Path.GetExtension(sqlitePath).ToLowerInvariant();
            string? accdbPath = null;
            if (ext == ".accdb" || ext == ".mdb")
            {
                accdbPath = sqlitePath; // remember the source before it gets overwritten
                // Always produce a .sqlite file next to the .accdb
                var sqliteFile = Path.ChangeExtension(sqlitePath, ".sqlite");
                // Remove any existing stale sqlite so migration runs fresh
                if (File.Exists(sqliteFile)) File.Delete(sqliteFile);

                var migrateWindow = new MigrationWindow();
                migrateWindow.Show();

                var migrator = new AccdbToSqliteMigrationService(sqlitePath, sqliteFile);
                migrator.OnProgress += msg => migrateWindow.AppendLog(msg);

                try
                {
                    migrator.TestAccessConnection();
                    migrator.Run();
                    sqlitePath = sqliteFile;

                    // Persist the source accdb path so we know where the data came from
                    settings.AccdbPath = accdbPath;
                    migrateWindow.AppendLog("Done! You can now delete your old .accdb file.");
                    migrateWindow.Done();
                }
                catch (Exception ex)
                {
                    migrateWindow.AppendLog($"ERROR: {ex.Message}");
                    ShowError("Migration Failed", ex);
                    migrateWindow.Close();
                    Current.Shutdown();
                    return;
                }
            }
        }

        // Persist
        settings.SqlitePath = sqlitePath;
        SaveSettings(settings);

        // Configure EF Core + DI
        ConfigureServices(sqlitePath);

        // Validate
        try
        {
            using var scope = Services.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PdTrackerDbContext>>();
            using var db = dbFactory.CreateDbContext();
            if (!db.Database.CanConnect())
                throw new Exception("Cannot connect to the database.");
        }
        catch (Exception ex)
        {
            ShowError("Database Error",
                new Exception($"Could not open '{sqlitePath}'.\n\n{ex.Message}", ex));
            Current.Shutdown();
            return;
        }

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    private static void ConfigureServices(string sqlitePath)
    {
        var services = new ServiceCollection();
        var connString = $"Data Source={sqlitePath}";

        services.AddDbContextFactory<PdTrackerDbContext>(options =>
            options.UseSqlite(connString));

        services.AddTransient<MainViewModel>();
        services.AddTransient<DefendantSearchViewModel>();
        services.AddTransient<NewApplicationViewModel>();
        services.AddTransient<AttorneyListViewModel>();
        services.AddTransient<VoucherSearchViewModel>();
        services.AddTransient<DefendantAZView>();

        // Register all Views (transient, each navigation gets a fresh instance)
        services.AddTransient<DefendantSearchView>();
        services.AddTransient<NewApplicationView>();
        services.AddTransient<AttorneyListView>();
        services.AddTransient<VoucherSearchView>();
        services.AddTransient<DefendantAZView>();

        Services = services.BuildServiceProvider();
    }

    private static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    private static void SaveSettings(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    private static void ShowError(string title, Exception? ex)
    {
        var msg = ex?.Message ?? "Unknown error";
        MessageBox.Show($"{msg}\n\nFull details:\n{ex}", title,
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

public class AppSettings
{
    public string? SqlitePath { get; set; }
    public string? AccdbPath { get; set; }
}
