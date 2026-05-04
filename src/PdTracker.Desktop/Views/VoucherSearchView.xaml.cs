using System.Windows.Controls;
using PdTracker.Desktop.ViewModels;

namespace PdTracker.Desktop.Views;

public partial class VoucherSearchView : UserControl
{
    public VoucherSearchView(VoucherSearchViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
