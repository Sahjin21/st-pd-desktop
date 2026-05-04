using System.Windows.Controls;
using PdTracker.Desktop.ViewModels;

namespace PdTracker.Desktop.Views;

public partial class DefendantSearchView : UserControl
{
    public DefendantSearchView(DefendantSearchViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
