using System.Data.SqlClient;
using System.Windows;

namespace PdTracker.Desktop.Views;

public partial class ConnectionSetupWindow : Window
{
    public string ConnectionString { get; private set; } = string.Empty;
    public string ServerName { get; set; } = "localhost\\SQLEXPRESS";
    public bool UseTrustedConnection { get; set; } = true;

    public ConnectionSetupWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        var cs = BuildConnectionString();
        var btn = (Button)sender;
        btn.IsEnabled = false;
        btn.Content = "Testing...";

        try
        {
            using var conn = new SqlConnection(cs);
            conn.Open();
            MessageBox.Show($"Connection successful! Database: {conn.Database}",
                "Test Result", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Connection failed:\n{ex.Message}",
                "Test Result", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            btn.IsEnabled = true;
            btn.Content = "Test Connection";
        }
    }

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        ConnectionString = BuildConnectionString();
        DialogResult = true;
        Close();
    }

    private string BuildConnectionString()
    {
        var sb = new SqlConnectionStringBuilder
        {
            DataSource = ServerName,
            InitialCatalog = "PdTracker",
            IntegratedSecurity = UseTrustedConnection,
            TrustServerCertificate = true,
            MultipleActiveResultSets = true
        };
        return sb.ConnectionString;
    }
}
