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
        string accdbPath = settings.AccdbPath ?? "";

        // Derive what the sqlite path *should* be for the stored accdb source
        string expectedSqlitePath = !string.IsNullOrEmpty(accdbPath)
            ? Path.ChangeExtension(accdbPath, ".sqlite") ?? ""
            : sqlitePath;

        // If sqlite file doesn't exist, OR accdb source changed since last run, need to re-set up
        bool sqliteIsStale = false;
        if (!File.Exists(sqlitePath))
        {
            // sqlite gone/missing — need to pick a source
            sqliteIsStale = true;
        }
        else if (!string.IsNullOrEmpty(accdbPath) && File.Exists(accdbPath))
        {
            // sqlite exists — check if it was built from a different accdb than stored
            if (!string.Equals(sqlitePath, expectedSqlitePath, StringComparison.OrdinalIgnoreCase))
                sqliteIsStale = true;
        }
        else if (!string.IsNullOrEmpty(sqlitePath) && string.IsNullOrEmpty(accdbPath))
        {
            // Sqlite stored but no accdb source recorded — might be stale
            sqliteIsStale = true;
        }

        if (sqliteIsStale)
        {
            // Show picker — user picks .accdb (to migrate) or .sqlite (existing db)
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

            var pickedPath = picker.FileName;
            var ext = Path.GetExtension(pickedPath).ToLowerInvariant();

            if (ext == ".accdb" || ext == ".mdb")
            {
                // Migrate from Access — always produce a .sqlite in the same folder
                accdbPath = pickedPath;
                sqlitePath = Path.ChangeExtension(pickedPath, ".sqlite") ?? pickedPath;

                // User picked an accdb — delete any existing sqlite at the target
                // so we do a clean migration without duplicate-key conflicts
                if (File.Exists(sqlitePath))
                    File.Delete(sqlitePath);

                var migrateWindow = new MigrationWindow();
                migrateWindow.Show();

                var migrator = new AccdbToSqliteMigrationService(accdbPath, sqlitePath);
                migrator.OnProgress += msg => migrateWindow.AppendLog(msg);

                try
                {
                    migrator.TestAccessConnection();
                    migrator.Run();
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
            else
            {
                // Picked a sqlite file directly — use it as-is
                sqlitePath = pickedPath;
                accdbPath = ""; // don't know the source
            }
        }

        // Persist
        settings.SqlitePath = sqlitePath;
        settings.AccdbPath = accdbPath;
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
