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

    /// <summary>
    /// The currently active SQLite database path.
    /// </summary>
    public static string CurrentSqlitePath { get; private set; } = null!;

    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdTracker");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

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

        // Check for --db argument (passed when restarting after DB change)
        string? overrideDb = null;
        for (int i = 0; i < e.Args.Length - 1; i++)
        {
            if (e.Args[i] == "--db")
            {
                overrideDb = e.Args[i + 1];
                break;
            }
        }

        // Load settings or detect portable mode
        string sqlitePath;
        string accdbPath;

        if (!string.IsNullOrEmpty(overrideDb))
        {
            // Restarted with new db — overrideDb is the sqlite filename, find it relative to exe
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            sqlitePath = Path.Combine(exeDir, overrideDb);
            accdbPath = "";
        }
        else if (File.Exists(SettingsPath))
        {
            var settings = LoadSettings();
            sqlitePath = settings.SqlitePath ?? "";
            accdbPath = settings.AccdbPath ?? "";
        }
        else
        {
            // Portable mode: check for .sqlite next to the .exe
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var portableSqlite = DetectPortableSqlite(exeDir);

            if (portableSqlite != null)
            {
                sqlitePath = portableSqlite;
                accdbPath = "";
            }
            else
            {
                // No settings, no portable sqlite — show file picker
                var picked = ShowFilePicker();
                if (picked == null) { Shutdown(); return; }
                sqlitePath = picked.Value.sqlitePath;
                accdbPath = picked.Value.accdbPath;
            }
        }

        // Validate / migrate if needed
        var result = EnsureDatabase(sqlitePath, accdbPath);
        if (result == null) { Shutdown(); return; }
        sqlitePath = result.Value.sqlitePath;
        accdbPath = result.Value.accdbPath;

        CurrentSqlitePath = sqlitePath;
        SaveSettings(sqlitePath, accdbPath);
        ConfigureServices(sqlitePath);

        // Validate connection
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
            Shutdown();
            return;
        }

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    /// <summary>
    /// Detects a .sqlite file sitting next to the running .exe (portable mode).
    /// Returns the full path, or null if none found.
    /// </summary>
    private static string? DetectPortableSqlite(string exeDir)
    {
        try
        {
            return Directory.GetFiles(exeDir, "*.sqlite")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .FirstOrDefault();
        }
        catch { return null; }
    }

    /// <summary>
    /// Shows the file picker. Returns null if user cancelled.
    /// </summary>
    private static (string sqlitePath, string accdbPath)? ShowFilePicker()
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select PD Tracker Database — pick the .accdb to migrate, or an existing .sqlite file",
            Filter =
                "Access Database (*.accdb;*.mdb)|*.accdb;*.mdb|" +
                "SQLite Database (*.sqlite;*.sqlite3)|*.sqlite;*.sqlite3|" +
                "All Files (*.*)|*.*",
        };

        if (picker.ShowDialog() != true) return null;

        var pickedPath = picker.FileName;
        var ext = Path.GetExtension(pickedPath).ToLowerInvariant();

        if (ext == ".accdb" || ext == ".mdb")
        {
            var sqlitePath = Path.ChangeExtension(pickedPath, ".sqlite") ?? pickedPath;
            if (File.Exists(sqlitePath)) File.Delete(sqlitePath);

            var migrateWindow = new MigrationWindow();
            migrateWindow.Show();

            var migrator = new AccdbToSqliteMigrationService(pickedPath, sqlitePath);
            migrator.OnProgress += msg => migrateWindow.AppendLog(msg);

            try
            {
                migrator.Run();
                migrateWindow.AppendLog("Done! You can now delete your old .accdb file.");
                migrateWindow.Done();
            }
            catch (Exception ex)
            {
                migrateWindow.AppendLog($"ERROR: {ex.Message}");
                ShowError("Migration Failed", ex);
                migrateWindow.Close();
                return null;
            }

            return (sqlitePath, pickedPath);
        }
        else
        {
            return (pickedPath, "");
        }
    }

    /// <summary>
    /// Returns (sqlitePath, accdbPath) after ensuring the sqlite is valid.
    /// Handles missing files and stale sqlite.
    /// </summary>
    private static (string sqlitePath, string accdbPath)? EnsureDatabase(string sqlitePath, string accdbPath)
    {
        bool needPicker = false;

        if (string.IsNullOrEmpty(sqlitePath) || !File.Exists(sqlitePath))
        {
            needPicker = true;
        }
        else if (!string.IsNullOrEmpty(accdbPath) && File.Exists(accdbPath))
        {
            // Sqlite exists but accdb is newer/different — check if sqlite matches accdb
            var expectedSqlite = Path.ChangeExtension(accdbPath, ".sqlite")
                ?? accdbPath + ".sqlite";
            if (!string.Equals(sqlitePath, expectedSqlite, StringComparison.OrdinalIgnoreCase))
                needPicker = true;
        }
        else if (!string.IsNullOrEmpty(sqlitePath) && string.IsNullOrEmpty(accdbPath))
        {
            // Sqlite exists with no accdb — verify it has data
            try
            {
                using var testDb = new PdTrackerDbContext(
                    new DbContextOptionsBuilder<PdTrackerDbContext>()
                        .UseSqlite($"Data Source={sqlitePath}").Options);
                if (!testDb.Database.CanConnect())
                    needPicker = true;
            }
            catch { needPicker = true; }
        }

        if (needPicker)
        {
            var result = ShowFilePicker();
            if (result == null) return null;
            sqlitePath = result.Value.sqlitePath;
            accdbPath = result.Value.accdbPath;
        }

        return (sqlitePath, accdbPath);
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

    private static void SaveSettings(string sqlitePath, string accdbPath)
    {
        try
        {
            if (!Directory.Exists(SettingsDir))
                Directory.CreateDirectory(SettingsDir);
            var settings = new AppSettings
            {
                SqlitePath = sqlitePath,
                AccdbPath = accdbPath
            };
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
