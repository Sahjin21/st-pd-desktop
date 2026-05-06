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

    [ObservableProperty] string _lastNameSearch = string.Empty;
    [ObservableProperty] string _firstNameSearch = string.Empty;
    [ObservableProperty] string _soidSearch = string.Empty;
    [ObservableProperty] Defendant? _selectedDefendant;
    [ObservableProperty] int _selectedTabIndex;
    [ObservableProperty] bool _isReadOnly;
    [ObservableProperty] bool _showSoidField;

    /// <summary>Current search mode: Edit, ReadOnly, Juvenile. Defaults to Edit.</summary>
    public string SearchMode { get; private set; } = "Edit";

    public ObservableCollection<Defendant> Results { get; } = new();

    // Full autocomplete suggestion sources — loaded once from DB
    public ObservableCollection<string> LastNameSuggestions { get; } = new();
    public ObservableCollection<string> FirstNameSuggestions { get; } = new();
    public ObservableCollection<string> SoidSuggestions { get; } = new();

    // Filtered sources — cascade: first name filtered by chosen last name, and vice versa
    public ObservableCollection<string> FilteredFirstNameSuggestions { get; } = new();
    public ObservableCollection<string> FilteredLastNameSuggestions { get; } = new();

    // All defendants cached for cascade filtering (loaded once)
    private List<Defendant> _allDefendants = new();

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
        _ = LoadSuggestionsAsync();
    }

    public void SetSearchMode(string mode)
    {
        SearchMode = mode;
        IsReadOnly = mode == "ReadOnly";
        // SOID field only for "Search by Booking#" (ReadOnly). Juvenile is same as name search.
        ShowSoidField = mode == "ReadOnly";
    }

    private async Task LoadSuggestionsAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var defendants = await db.Defendants
                .Where(d => !string.IsNullOrWhiteSpace(d.LastName))
                .Take(1000)
                .ToListAsync();
            _allDefendants = defendants;

            var lastNames = defendants
                .Where(d => !string.IsNullOrWhiteSpace(d.LastName))
                .Select(d => d.LastName!)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            var firstNames = defendants
                .Where(d => !string.IsNullOrWhiteSpace(d.FirstName))
                .Select(d => d.FirstName!)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            var soids = defendants
                .Where(d => !string.IsNullOrWhiteSpace(d.SOID))
                .Select(d => d.SOID!)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            LastNameSuggestions.Clear();
            FirstNameSuggestions.Clear();
            SoidSuggestions.Clear();
            foreach (var n in lastNames) LastNameSuggestions.Add(n);
            foreach (var n in firstNames) FirstNameSuggestions.Add(n);
            foreach (var s in soids) SoidSuggestions.Add(s);

            // Seed filtered collections with all names initially (before user types anything)
            FilteredFirstNameSuggestions.Clear();
            FilteredLastNameSuggestions.Clear();
            foreach (var fn in firstNames) FilteredFirstNameSuggestions.Add(fn);
            foreach (var ln in lastNames) FilteredLastNameSuggestions.Add(ln);
        }
        catch { /* non-fatal */ }
    }

    partial void OnLastNameSearchChanged(string value)
    {
        // When a last name is chosen from the dropdown, filter first names to only
        // show first names that share that last name in the DB.
        FilterFirstNameSuggestions(FirstNameSearch.Trim(), value.Trim());
    }

    partial void OnFirstNameSearchChanged(string value)
    {
        // When a first name is chosen from the dropdown, filter last names to only
        // show last names that share that first name in the DB.
        FilterLastNameSuggestions(LastNameSearch.Trim(), value.Trim());
    }

    private void FilterFirstNameSuggestions(string typedFirst, string chosenLast)
    {
        FilteredFirstNameSuggestions.Clear();

        // Use _allDefendants if available, otherwise fall back to LastNameSuggestions for the name list
        IEnumerable<Defendant> source = _allDefendants.Count > 0
            ? _allDefendants
            : LastNameSuggestions.Select(ln => new Defendant { LastName = ln }); // dummy source with last names

        if (string.IsNullOrEmpty(chosenLast))
        {
            // No last name chosen — show all first names that match the typed fragment
            foreach (var fn in FirstNameSuggestions
                .Where(f => string.IsNullOrEmpty(typedFirst) ||
                            f.StartsWith(typedFirst, StringComparison.OrdinalIgnoreCase) ||
                            f.Contains(typedFirst, StringComparison.OrdinalIgnoreCase)))
                FilteredFirstNameSuggestions.Add(fn);
        }
        else
        {
            // Last name is chosen — only show first names that appear with that last name
            var validFirstNames = _allDefendants.Count > 0
                ? _allDefendants
                    .Where(d => d.LastName != null &&
                                d.LastName.Equals(chosenLast, StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrEmpty(d.FirstName))
                    .Select(d => d.FirstName!)
                    .Distinct()
                    .OrderBy(f => f)
                : Enumerable.Empty<string>();

            foreach (var fn in validFirstNames
                .Where(f => string.IsNullOrEmpty(typedFirst) ||
                            f.StartsWith(typedFirst, StringComparison.OrdinalIgnoreCase) ||
                            f.Contains(typedFirst, StringComparison.OrdinalIgnoreCase)))
                FilteredFirstNameSuggestions.Add(fn);
        }
    }

    private void FilterLastNameSuggestions(string typedLast, string chosenFirst)
    {
        FilteredLastNameSuggestions.Clear();

        if (string.IsNullOrEmpty(chosenFirst))
        {
            // No first name filter — show all last names matching the typed fragment
            foreach (var ln in LastNameSuggestions
                .Where(f => string.IsNullOrEmpty(typedLast) ||
                            f.StartsWith(typedLast, StringComparison.OrdinalIgnoreCase) ||
                            f.Contains(typedLast, StringComparison.OrdinalIgnoreCase)))
                FilteredLastNameSuggestions.Add(ln);
        }
        else
        {
            // First name is chosen — only show last names that appear with that first name
            var validLastNames = _allDefendants.Count > 0
                ? _allDefendants
                    .Where(d => d.FirstName != null &&
                                d.FirstName.Equals(chosenFirst, StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrEmpty(d.LastName))
                    .Select(d => d.LastName!)
                    .Distinct()
                    .OrderBy(l => l)
                : Enumerable.Empty<string>();

            foreach (var ln in validLastNames
                .Where(f => string.IsNullOrEmpty(typedLast) ||
                            f.StartsWith(typedLast, StringComparison.OrdinalIgnoreCase) ||
                            f.Contains(typedLast, StringComparison.OrdinalIgnoreCase)))
                FilteredLastNameSuggestions.Add(ln);
        }
    }

    [RelayCommand]
    async Task SearchAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var ln = LastNameSearch.Trim();
            var fn = FirstNameSearch.Trim();
            var soid = SoidSearch.Trim();

            // Load all — data is small enough; filter client-side for reliability
            var all = await db.Defendants
                .Include(d => d.Qualify)
                .Take(500)
                .ToListAsync();

            var query = all.AsEnumerable();

            if (!string.IsNullOrEmpty(soid))
                query = query.Where(d =>
                    d.SOID != null &&
                    d.SOID.Contains(soid, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(ln))
                query = query.Where(d =>
                    d.LastName != null &&
                    d.LastName.Contains(ln, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(fn))
                query = query.Where(d =>
                    d.FirstName != null &&
                    d.FirstName.Contains(fn, StringComparison.OrdinalIgnoreCase));

            Results.Clear();
            foreach (var d in query.Take(50)) Results.Add(d);
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
