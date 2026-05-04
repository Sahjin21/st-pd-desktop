using System.Windows.Controls;
using PdTracker.Desktop.ViewModels;

namespace PdTracker.Desktop.Views;

public partial class AttorneyListView : UserControl
{
    public AttorneyListView(AttorneyListViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) =>
        {
            try { await vm.LoadAllAsync(); }
            catch { }
        };
    }
}
