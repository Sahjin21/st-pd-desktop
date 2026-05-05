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

            // If navigated via "Add Attorney", auto-open the add panel and clean up
            if (App.ViewParameters.TryRemove("AddAttorney", out _))
                vm.StartAddCommand.Execute(null);
        };
    }
}
