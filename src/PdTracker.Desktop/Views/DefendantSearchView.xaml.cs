using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PdTracker.Desktop.ViewModels;

namespace PdTracker.Desktop.Views;

public partial class DefendantSearchView : UserControl
{
    public DefendantSearchView()
    {
        InitializeComponent();
        DataContext = App.Services.GetService(typeof(DefendantSearchViewModel));
    }
}
