using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PdTracker.Desktop.ViewModels;

namespace PdTracker.Desktop.Views;

public partial class VoucherSearchView : UserControl
{
    public VoucherSearchView()
    {
        InitializeComponent();
        DataContext = App.Services.GetService(typeof(VoucherSearchViewModel));
    }
}
