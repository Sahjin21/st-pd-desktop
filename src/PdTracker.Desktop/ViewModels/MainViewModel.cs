using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdTracker.Desktop.Services;
using PdTracker.Desktop.Views;

namespace PdTracker.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public event EventHandler<string>? NavigationRequested;

    [RelayCommand]
    void Navigate(string destination) => NavigationRequested?.Invoke(this, destination);

    [RelayCommand]
    void Exit() => Application.Current.Shutdown();

    [RelayCommand]
    void ChangeDatabase()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Change Database — pick an existing .sqlite file or an .accdb to migrate",
            Filter =
                "SQLite Database (*.sqlite;*.sqlite3)|*.sqlite;*.sqlite3|" +
                "Access Database (*.accdb;*.mdb)|*.accdb;*.mdb|" +
                "All Files (*.*)|*.*",
        };

        if (dialog.ShowDialog() != true) return;

        var pickedPath = dialog.FileName;
        var ext = Path.GetExtension(pickedPath).ToLowerInvariant();

        if (ext == ".accdb" || ext == ".mdb")
        {
            // Migrate from Access
            var sqlitePath = Path.ChangeExtension(pickedPath, ".sqlite") ?? pickedPath;
            if (File.Exists(sqlitePath)) File.Delete(sqlitePath);

            var migrateWindow = new MigrationWindow();
            migrateWindow.Show();

            var migrator = new AccdbToSqliteMigrationService(pickedPath, sqlitePath);
            migrator.OnProgress += msg => migrateWindow.AppendLog(msg);

            try
            {
                migrator.Run();
                migrateWindow.AppendLog("Migration complete!");
                migrateWindow.Done();
            }
            catch (Exception ex)
            {
                migrateWindow.AppendLog($"ERROR: {ex.Message}");
                migrateWindow.Close();
                MessageBox.Show($"Migration failed:\n{ex.Message}", "Migration Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SaveNewDbPath(sqlitePath, pickedPath);
        }
        else
        {
            // Direct SQLite — just switch to it
            SaveNewDbPath(pickedPath, "");
        }

        MessageBox.Show("Database changed. The app will now restart.",
            "Database Changed", MessageBoxButton.OK, MessageBoxImage.Information);

        // Restart the app with the new database
        System.Diagnostics.Process.Start(
            Environment.ProcessPath!,
            $"--db \"{Path.GetFileName(pickedPath)}\"");
        Application.Current.Shutdown();
    }

    private static void SaveNewDbPath(string sqlitePath, string accdbPath)
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PdTracker", "settings.json");

        var dir = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var settings = new AppDbSettings
        {
            SqlitePath = sqlitePath,
            AccdbPath = accdbPath
        };

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, json);
    }

    public void OnLoaded()
    {
        Navigate("SearchDefendant");
    }
}

public class AppDbSettings
{
    public string? SqlitePath { get; set; }
    public string? AccdbPath { get; set; }
}
