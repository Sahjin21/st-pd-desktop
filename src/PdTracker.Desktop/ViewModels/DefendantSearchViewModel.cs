using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PdTracker.Core.Entities;
using PdTracker.Data.DbContext;

namespace PdTracker.Desktop.ViewModels;

public partial class DefendantSearchViewModel : ObservableObject
{
    private readonly IDbContextFactory<PdTrackerDbContext> _dbFactory;

    [ObservableProperty] string _searchText = string.Empty;
    [ObservableProperty] Defendant? _selectedDefendant;
    [ObservableProperty] int _selectedTabIndex;

    public ObservableCollection<Defendant> Results { get; } = new();

    // Tab sub-ViewModels
    public AppointInfoViewModel AppointInfoVm { get; }
    public CourtInfoViewModel CourtInfoVm { get; }
    public CommentViewModel CommentVm { get; }

    public DefendantSearchViewModel(IDbContextFactory<PdTrackerDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        AppointInfoVm = new AppointInfoViewModel(dbFactory);
        CourtInfoVm = new CourtInfoViewModel(dbFactory);
        CommentVm = new CommentViewModel(dbFactory);
    }

    [RelayCommand]
    async Task SearchAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;

            await using var db = await _dbFactory.CreateDbContextAsync();
            var term = SearchText.Trim();

            // Try as number first (ApplicationNumber), then as string (DefendantID/SOID/Name)
            if (int.TryParse(term, out var appNum))
            {
                Results.Clear();
                var found = await db.Defendants
                    .Include(d => d.Qualify)
                    .FirstOrDefaultAsync(d => d.ApplicationNumber == appNum);
                if (found != null) Results.Add(found);
            }
            else
            {
                var pattern = $"%{term}%";
                var list = await db.Defendants
                    .Include(d => d.Qualify)
                    .Where(d => EF.Functions.Like(d.LastName, pattern)
                             || EF.Functions.Like(d.FirstName, pattern)
                             || EF.Functions.Like(d.DefendantId, pattern)
                             || (d.SOID != null && EF.Functions.Like(d.SOID, pattern)))
                    .Take(50)
                    .ToListAsync();

                Results.Clear();
                foreach (var d in list) Results.Add(d);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Search error:\n{ex.Message}", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    partial void OnSelectedDefendantChanged(Defendant? value)
    {
        if (value == null) return;
        AppointInfoVm.Load(value.DefendantId);
        CourtInfoVm.Load(value.ApplicationNumber);
        CommentVm.Load(value.DefendantId);
        SelectedTabIndex = 1; // Switch to AppointInfo tab
    }
}

public partial class AppointInfoViewModel : ObservableObject
{
    private readonly IDbContextFactory<PdTrackerDbContext> _dbFactory;
    [ObservableProperty] Appointment? _currentAppointment;
    public ObservableCollection<Appointment> Appointments { get; } = new();

    public AppointInfoViewModel(IDbContextFactory<PdTrackerDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async void Load(string defendantId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var appts = await db.Appointments
            .Include(a => a.Attorney)
            .Where(a => a.Defendant != null && a.Defendant.DefendantId == defendantId)
            .OrderByDescending(a => a.Date)
            .ToListAsync();

        Appointments.Clear();
        foreach (var a in appts) Appointments.Add(a);
        CurrentAppointment = Appointments.FirstOrDefault();
    }
}

public partial class CourtInfoViewModel : ObservableObject
{
    private readonly IDbContextFactory<PdTrackerDbContext> _dbFactory;
    [ObservableProperty] Qualify? _qualify;
    [ObservableProperty] List<Charge> _charges = new();
    [ObservableProperty] List<Warrant> _warrants = new();

    public CourtInfoViewModel(IDbContextFactory<PdTrackerDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async void Load(int applicationNumber)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        Qualify = await db.Qualifies.FirstOrDefaultAsync(q => q.ApplicationNumber == applicationNumber);
        Charges = await db.Charges.Where(c => c.ApplicationNumber == applicationNumber).ToListAsync();
        Warrants = await db.Warrants.Where(w => w.ApplicationNumber == applicationNumber).ToListAsync();
    }
}

public partial class CommentViewModel : ObservableObject
{
    private readonly IDbContextFactory<PdTrackerDbContext> _dbFactory;
    [ObservableProperty] string _comments = string.Empty;
    [ObservableProperty] string _courtInfo = string.Empty;
    [ObservableProperty] bool _military;

    public CommentViewModel(IDbContextFactory<PdTrackerDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async void Load(string defendantId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var qualify = await db.Qualifies.FirstOrDefaultAsync(q => q.DefendantId == defendantId);
        if (qualify != null)
        {
            Comments = qualify.Comment ?? string.Empty;
            CourtInfo = qualify.CourtInformation ?? string.Empty;
            Military = qualify.Military ?? false;
        }
    }
}
