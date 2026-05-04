using System.Windows.Controls;
using PdTracker.Desktop.ViewModels;

namespace PdTracker.Desktop.Views;

public partial class NewApplicationView : UserControl
{
    public NewApplicationView(NewApplicationViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
