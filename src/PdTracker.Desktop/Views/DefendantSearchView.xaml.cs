using System.Windows.Controls;
using PdTracker.Desktop.ViewModels;

namespace PdTracker.Desktop.Views;

public partial class DefendantSearchView : UserControl
{
    public DefendantSearchView(DefendantSearchViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += (_, _) =>
        {
            // Handle mode passed via navigation (Edit, ReadOnly, Juvenile, etc.)
            if (App.ViewParameters.TryRemove("DefendantSearchMode", out var mode))
                vm.SetSearchMode(mode);
        };
    }
}
