using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PdTracker.Desktop.ViewModels;

namespace PdTracker.Desktop.Views;

public partial class AttorneyListView : UserControl
{
    public AttorneyListView()
    {
        InitializeComponent();
        var vm = App.Services.GetService(typeof(AttorneyListViewModel)) as AttorneyListViewModel;
        DataContext = vm;
        Loaded += async (_, _) => await vm!.LoadAllAsync();
    }
}
