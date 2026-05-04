using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PdTracker.Desktop.ViewModels;

namespace PdTracker.Desktop.Views;

public partial class NewApplicationView : UserControl
{
    public NewApplicationView()
    {
        InitializeComponent();
        DataContext = App.Services.GetService(typeof(NewApplicationViewModel));
    }
}
