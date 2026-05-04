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

    public ObservableCollection<Voucher> Results { get; } = new();

    public VoucherSearchViewModel(IDbContextFactory<PdTrackerDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    [RelayCommand]
    async Task SearchAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        List<Voucher> list;
        if (int.TryParse(VoucherNumber.Trim(), out var num))
        {
            list = await db.Vouchers
                .Include(v => v.Defendant)
                .Include(v => v.Attorney)
                .Where(v => v.VoucherNumber == num.ToString() || v.ApplicationNumber == num)
                .OrderByDescending(v => v.DateVchrPaid)
                .Take(20)
                .ToListAsync();
        }
        else
        {
            var term = VoucherNumber.Trim().ToLower();
            list = await db.Vouchers
                .Include(v => v.Defendant)
                .Include(v => v.Attorney)
                .Where(v => v.VoucherNumber.ToLower().Contains(term)
                         || (v.Defendant != null && v.Defendant.LastName.ToLower().Contains(term)))
                .OrderByDescending(v => v.DateVchrPaid)
                .Take(20)
                .ToListAsync();
        }

        Results.Clear();
        foreach (var v in list) Results.Add(v);
    }
}
