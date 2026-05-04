using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdTracker.Core.Entities;
using PdTracker.Data.DbContext;

namespace PdTracker.Desktop.ViewModels;

// 8-tab new application wizard
public partial class NewApplicationViewModel : ObservableObject
{
    private readonly IDbContextFactory<PdTrackerDbContext> _dbFactory;

    [ObservableProperty] int _currentStep;
    [ObservableProperty] string _nextLabel = "Next →";
    [ObservableProperty] bool _canGoBack;
    [ObservableProperty] bool _isSaving;

    // Step data
    [ObservableProperty] Defendant _defendant = new();
    [ObservableProperty] ObservableCollection<DefAddress> _addresses = new();
    [ObservableProperty] ObservableCollection<DefPhone> _phones = new();
    [ObservableProperty] ObservableCollection<Charge> _charges = new();
    [ObservableProperty] ObservableCollection<FinEmployer> _employers = new();
    [ObservableProperty] DefSpouse _spouse = new();
    [ObservableProperty] ObservableCollection<FinUnemployed> _unemployment = new();
    [ObservableProperty] string _comments = string.Empty;
    [ObservableProperty] string _courtComments = string.Empty;
    [ObservableProperty] bool _military;
    [ObservableProperty] ObservableCollection<Dependent> _juvenileInfo = new();

    public string[] StepTitles => new[]
    {
        "Personal", "Address / Phone", "Charges", "Financial",
        "Spouse", "Other Financial", "Comments / Court", "Juvenile"
    };

    public NewApplicationViewModel(IDbContextFactory<PdTrackerDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        Addresses.Add(new DefAddress { AddressFlag = AddressFlag.Current });
        Phones.Add(new DefPhone());
        Charges.Add(new Charge());
        Employers.Add(new FinEmployer());
        JuvenileInfo.Add(new Dependent());
    }

    [RelayCommand]
    void Next()
    {
        if (CurrentStep < 7)
        {
            CurrentStep++;
            UpdateState();
        }
        else
        {
            _ = SaveAsync();
        }
    }

    [RelayCommand]
    void Back()
    {
        if (CurrentStep > 0)
        {
            CurrentStep--;
            UpdateState();
        }
    }

    void UpdateState()
    {
        CanGoBack = CurrentStep > 0;
        NextLabel = CurrentStep == 7 ? "Save ✓" : "Next →";
    }

    [RelayCommand]
    async Task SaveAsync()
    {
        IsSaving = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Set ApplicationNumber from sequence (SQL Server auto-increment)
            var lastApp = await db.Defendants.MaxAsync(d => (int?)d.ApplicationNumber) ?? 0;
            Defendant.ApplicationNumber = lastApp + 1;

            db.Defendants.Add(Defendant);
            await db.SaveChangesAsync();

            // Save satellites
            foreach (var addr in Addresses) { addr.DefendantId = Defendant.DefendantId; db.DefAddresses.Add(addr); }
            foreach (var ph in Phones) { ph.DefendantId = Defendant.DefendantId; db.DefPhones.Add(ph); }
            foreach (var ch in Charges) { ch.ApplicationNumber = Defendant.ApplicationNumber; db.Charges.Add(ch); }
            foreach (var emp in Employers) { emp.DefendantId = Defendant.DefendantId; db.FinEmployers.Add(emp); }

            Spouse.DefendantId = Defendant.DefendantId;
            db.DefSpouses.Add(Spouse);

            foreach (var unemp in Unemployment) { unemp.DefendantId = Defendant.DefendantId; db.FinUnemployeds.Add(unemp); }
            foreach (var dep in JuvenileInfo) { dep.DefendantId = Defendant.DefendantId; db.Dependents.Add(dep); }

            var qualify = new Qualify
            {
                ApplicationNumber = Defendant.ApplicationNumber,
                DefendantId = Defendant.DefendantId,
                Military = Military,
                Comment = Comments,
                CourtInformation = CourtComments,
                Date = DateTime.Now,
                EntryDate = DateTime.Now
            };
            db.Qualifies.Add(qualify);

            await db.SaveChangesAsync();
            IsSaving = false;
        }
        catch (Exception ex)
        {
            IsSaving = false;
            System.Windows.MessageBox.Show($"Save failed: {ex.Message}", "Error");
        }
    }

    // Add/Remove rows
    [RelayCommand] void AddAddress() => Addresses.Add(new DefAddress { AddressFlag = AddressFlag.Current });
    [RelayCommand] void RemoveAddress(DefAddress addr) => Addresses.Remove(addr);
    [RelayCommand] void AddPhone() => Phones.Add(new DefPhone());
    [RelayCommand] void RemovePhone(DefPhone ph) => Phones.Remove(ph);
    [RelayCommand] void AddCharge() => Charges.Add(new Charge());
    [RelayCommand] void RemoveCharge(Charge ch) => Charges.Remove(ch);
    [RelayCommand] void AddEmployer() => Employers.Add(new FinEmployer());
    [RelayCommand] void RemoveEmployer(FinEmployer emp) => Employers.Remove(emp);
    [RelayCommand] void AddJuvenile() => JuvenileInfo.Add(new Dependent());
    [RelayCommand] void RemoveJuvenile(Dependent dep) => JuvenileInfo.Remove(dep);
}
