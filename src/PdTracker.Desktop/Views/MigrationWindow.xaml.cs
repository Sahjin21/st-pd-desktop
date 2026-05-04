using System.Windows;

namespace PdTracker.Desktop.Views;

public partial class MigrationWindow : Window
{
    public MigrationWindow()
    {
        InitializeComponent();
    }

    public void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogText.Text += message + "\n";
        });
    }

    public void Done()
    {
        Dispatcher.Invoke(() =>
        {
            CloseButton.IsEnabled = true;
            CloseButton.Content = "Continue";
            AppendLog("\nImport complete!");
        });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
