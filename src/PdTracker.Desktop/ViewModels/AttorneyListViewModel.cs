using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdTracker.Core.Entities;
using PdTracker.Data.DbContext;

namespace PdTracker.Desktop.ViewModels;

public partial class AttorneyListViewModel : ObservableObject
{
    private readonly IDbContextFactory<PdTrackerDbContext> _dbFactory;

    [ObservableProperty] string _searchText = string.Empty;
    [ObservableProperty] AttorneyList? _selectedAttorney;
    [ObservableProperty] bool _isEditMode;
    [ObservableProperty] AttorneyList _editingAttorney = new();

    public ObservableCollection<AttorneyList> Results { get; } = new();
    public ObservableCollection<AttorneyList> AllAttorneys { get; } = new();

    public AttorneyListViewModel(IDbContextFactory<PdTrackerDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task LoadAllAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var list = await db.AttorneyLists
            .OrderBy(a => a.LastName)
            .ThenBy(a => a.FirstName)
            .ToListAsync();

        AllAttorneys.Clear();
        Results.Clear();
        foreach (var a in list) { AllAttorneys.Add(a); Results.Add(a); }
    }

    [RelayCommand]
    async Task SearchAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var term = SearchText.Trim().ToLower();

        var list = await db.AttorneyLists
            .Where(a => a.LastName.ToLower().Contains(term)
                     || a.FirstName.ToLower().Contains(term)
                     || a.AttyCode.ToLower().Contains(term))
            .OrderBy(a => a.LastName)
            .ToListAsync();

        Results.Clear();
        foreach (var a in list) Results.Add(a);
    }

    [RelayCommand]
    void StartAdd()
    {
        EditingAttorney = new AttorneyList();
        IsEditMode = true;
    }

    [RelayCommand]
    void StartEdit()
    {
        if (SelectedAttorney == null) return;
        EditingAttorney = new AttorneyList
        {
            AttyCode = SelectedAttorney.AttyCode,
            FirstName = SelectedAttorney.FirstName,
            MiddleName = SelectedAttorney.MiddleName,
            LastName = SelectedAttorney.LastName,
            Street = SelectedAttorney.Street,
            Suite = SelectedAttorney.Suite,
            City = SelectedAttorney.City,
            State = SelectedAttorney.State,
            ZipCode = SelectedAttorney.ZipCode,
            Email = SelectedAttorney.Email,
            OfficeNumber = SelectedAttorney.OfficeNumber,
            MobileNumber = SelectedAttorney.MobileNumber,
            Status = SelectedAttorney.Status,
            DeathPenalty = SelectedAttorney.DeathPenalty,
            Murder = SelectedAttorney.Murder,
            Felony = SelectedAttorney.Felony,
            Misd = SelectedAttorney.Misd,
            Appeal = SelectedAttorney.Appeal,
            Juvenile = SelectedAttorney.Juvenile,
            GAL = SelectedAttorney.GAL,
            Notes = SelectedAttorney.Notes
        };
        IsEditMode = true;
    }

    [RelayCommand]
    async Task SaveAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.AttorneyLists.FindAsync(EditingAttorney.AttyCode);

        if (existing == null)
        {
            db.AttorneyLists.Add(EditingAttorney);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(EditingAttorney);
        }

        await db.SaveChangesAsync();
        IsEditMode = false;
        await LoadAllAsync();
    }

    [RelayCommand]
    void CancelEdit()
    {
        IsEditMode = false;
    }
}
