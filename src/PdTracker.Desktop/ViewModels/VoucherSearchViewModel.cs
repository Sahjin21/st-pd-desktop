using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdTracker.Core.Entities;
using PdTracker.Data.DbContext;

namespace PdTracker.Desktop.ViewModels;

public partial class VoucherSearchViewModel : ObservableObject
{
    private readonly IDbContextFactory<PdTrackerDbContext> _dbFactory;

    [ObservableProperty] string _voucherNumber = string.Empty;
    [ObservableProperty] Voucher? _selectedVoucher;
    [ObservableProperty] bool _hasSearched;
    [ObservableProperty] bool _hasResults;
    [ObservableProperty] bool _showNoResults;

    public ObservableCollection<Voucher> Results { get; } = new();
    public ObservableCollection<string> VoucherNumberSuggestions { get; } = new();

    public VoucherSearchViewModel(IDbContextFactory<PdTrackerDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        _ = LoadSuggestionsAsync();
    }

    private async Task LoadSuggestionsAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var numbers = await db.Vouchers
                .Where(v => !string.IsNullOrWhiteSpace(v.VoucherNumber))
                .Select(v => v.VoucherNumber!)
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync();

            VoucherNumberSuggestions.Clear();
            foreach (var n in numbers)
                VoucherNumberSuggestions.Add(n);
        }
        catch { /* non-fatal */ }
    }

    [RelayCommand]
    async Task SearchAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var term = VoucherNumber.Trim();

        var all = await db.Vouchers
            .Include(v => v.Defendant)
            .Include(v => v.Attorney)
            .OrderByDescending(v => v.DateVchrPaid)
            .ToListAsync();

        IEnumerable<Voucher> query = all;

        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(v =>
                (!string.IsNullOrEmpty(v.VoucherNumber) &&
                 v.VoucherNumber.StartsWith(term, StringComparison.OrdinalIgnoreCase)) ||
                (v.ApplicationNumber?.ToString().StartsWith(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (!string.IsNullOrEmpty(v.Defendant?.LastName) &&
                 v.Defendant.LastName.StartsWith(term, StringComparison.OrdinalIgnoreCase)));
        }

        Results.Clear();
        foreach (var v in query.Take(100))
            Results.Add(v);

        HasSearched = true;
        HasResults = Results.Count > 0;
        ShowNoResults = HasSearched && !HasResults;
        SelectedVoucher = Results.FirstOrDefault();
    }
}
