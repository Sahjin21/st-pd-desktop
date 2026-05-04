using Microsoft.EntityFrameworkCore;
using PdTracker.Core.Entities;

namespace PdTracker.Data.DbContext;

public class PdTrackerDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public PdTrackerDbContext(DbContextOptions<PdTrackerDbContext> options) : base(options) { }

    // Core
    public DbSet<Defendant> Defendants => Set<Defendant>();
    public DbSet<Qualify> Qualifies => Set<Qualify>();
    public DbSet<AttorneyList> AttorneyLists => Set<AttorneyList>();

    // Defendant satellites
    public DbSet<DefAddress> DefAddresses => Set<DefAddress>();
    public DbSet<DefPhone> DefPhones => Set<DefPhone>();
    public DbSet<DefSpouse> DefSpouses => Set<DefSpouse>();
    public DbSet<DefAlias> DefAliases => Set<DefAlias>();
    public DbSet<Dependent> Dependents => Set<Dependent>();

    // Financial
    public DbSet<FinEmployer> FinEmployers => Set<FinEmployer>();
    public DbSet<FinSpEmployer> FinSpEmployers => Set<FinSpEmployer>();
    public DbSet<FinUnemployed> FinUnemployeds => Set<FinUnemployed>();
    public DbSet<FinSpUnemploy> FinSpUnemploys => Set<FinSpUnemploy>();
    public DbSet<FinAuto> FinAutos => Set<FinAuto>();
    public DbSet<FinBank> FinBanks => Set<FinBank>();
    public DbSet<FinHome> FinHomes => Set<FinHome>();
    public DbSet<FinRent> FinRents => Set<FinRent>();
    public DbSet<FinOther> FinOthers => Set<FinOther>();

    // Case management
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<Warrant> Warrants => Set<Warrant>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<EIA> EIAs => Set<EIA>();

    // Lookups
    public DbSet<ChargeId> ChargeIds => Set<ChargeId>();
    public DbSet<DenialCode> DenialCodes => Set<DenialCode>();
    public DbSet<RemovalCode> RemovalCodes => Set<RemovalCode>();
    public DbSet<Jurisdiction> Jurisdictions => Set<Jurisdiction>();
    public DbSet<Judge> Judges => Set<Judge>();
    public DbSet<IncomeSource> IncomeSources => Set<IncomeSource>();
    public DbSet<Core.Entities.Type> Types => Set<Core.Entities.Type>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Defendant — map to legacy table/column names
        modelBuilder.Entity<Defendant>(e =>
        {
            e.ToTable("DEFENDANT");
            e.HasKey(x => x.DefendantId);
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.ApplicationNumber).HasColumnName("ApplicationNumber");
            e.Property(x => x.SOID).HasColumnName("SOID").HasMaxLength(20);
            e.Property(x => x.FirstName).HasColumnName("FirstName").HasMaxLength(50);
            e.Property(x => x.MiddleName).HasColumnName("MiddleName").HasMaxLength(50);
            e.Property(x => x.LastName).HasColumnName("LastName").HasMaxLength(50);
            e.Property(x => x.DOB).HasColumnName("DOB");
            e.Property(x => x.Education).HasColumnName("Education").HasMaxLength(50);
            e.Property(x => x.Race).HasColumnName("Race").HasMaxLength(20);
            e.Property(x => x.Sex).HasColumnName("Sex").HasMaxLength(10);
            e.Property(x => x.Reference1).HasColumnName("Reference1").HasMaxLength(100);
            e.Property(x => x.Reference2).HasColumnName("Reference2").HasMaxLength(100);
            e.Property(x => x.DefPhoto).HasColumnName("defphoto");
            e.Property(x => x.DefSignature).HasColumnName("defsignature");
            e.Property(x => x.Dependants).HasColumnName("dependants");
            e.Property(x => x.DepDescription).HasColumnName("dep_description");
            e.Property(x => x.DepOther).HasColumnName("dep_other");
            e.Property(x => x.DateAdded).HasColumnName("DateAdded");

            e.HasIndex(x => new { x.LastName, x.FirstName });
            e.HasIndex(x => x.ApplicationNumber).IsUnique();

            e.HasOne(x => x.Qualify)
                .WithOne(x => x.Defendant)
                .HasForeignKey<Qualify>(x => x.DefendantId);
        });

        // Qualify
        modelBuilder.Entity<Qualify>(e =>
        {
            e.ToTable("QUALIFY");
            e.HasKey(x => x.ApplicationNumber);
            e.Property(x => x.Date).HasColumnName("Date");
            e.Property(x => x.NoAction).HasColumnName("NoAction");
            e.Property(x => x.Comment).HasColumnName("Comment").HasMaxLength(255);
            e.Property(x => x.CourtInformation).HasColumnName("CourtInformation").HasMaxLength(255);
            e.Property(x => x.Military).HasColumnName("Military");
            e.Property(x => x.EntryDate).HasColumnName("Entrydate");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
        });

        // AttorneyList
        modelBuilder.Entity<AttorneyList>(e =>
        {
            e.ToTable("ATTORNEY_LIST");
            e.HasKey(x => x.AttyCode);
            e.Property(x => x.AttyCode).HasColumnName("AttyCode").HasMaxLength(10);
            e.Property(x => x.FirstName).HasColumnName("FirstName").HasMaxLength(50);
            e.Property(x => x.MiddleName).HasColumnName("MiddleName").HasMaxLength(50);
            e.Property(x => x.LastName).HasColumnName("LastName").HasMaxLength(50);
            e.Property(x => x.Street).HasColumnName("Street").HasMaxLength(100);
            e.Property(x => x.Suite).HasColumnName("Suite").HasMaxLength(20);
            e.Property(x => x.City).HasColumnName("City").HasMaxLength(50);
            e.Property(x => x.State).HasColumnName("State").HasMaxLength(2);
            e.Property(x => x.ZipCode).HasColumnName("ZipCode").HasMaxLength(10);
            e.Property(x => x.Email).HasColumnName("Email").HasMaxLength(100);
            e.Property(x => x.OfficeNumber).HasColumnName("OfficeNumber").HasMaxLength(20);
            e.Property(x => x.FaxNumber).HasColumnName("FaxNumber").HasMaxLength(20);
            e.Property(x => x.HomeNumber).HasColumnName("HomeNumber").HasMaxLength(20);
            e.Property(x => x.PagerNumber).HasColumnName("PagerNumber").HasMaxLength(20);
            e.Property(x => x.MobileNumber).HasColumnName("MobileNumber").HasMaxLength(20);
            e.Property(x => x.OtherNumber).HasColumnName("OtherNumber").HasMaxLength(20);
            e.Property(x => x.PhoneType).HasColumnName("PhoneType").HasMaxLength(20);
            e.Property(x => x.Date).HasColumnName("Date");
            e.Property(x => x.Status).HasColumnName("Status").HasConversion<string>();
            e.Property(x => x.DeathPenalty).HasColumnName("Deathpenalty");
            e.Property(x => x.Murder).HasColumnName("Murder");
            e.Property(x => x.Felony).HasColumnName("Felony");
            e.Property(x => x.Misd).HasColumnName("Misd");
            e.Property(x => x.Appeal).HasColumnName("Appeal");
            e.Property(x => x.Juvenile).HasColumnName("Juvenile");
            e.Property(x => x.GAL).HasColumnName("GAL");
            e.Property(x => x.VendorNumber).HasColumnName("VenderNumber").HasMaxLength(20);
            e.Property(x => x.Notes).HasColumnName("Notes").HasMaxLength(255);

            e.HasIndex(x => new { x.LastName, x.FirstName });
        });

        // DefAddress
        modelBuilder.Entity<DefAddress>(e =>
        {
            e.ToTable("DEF_ADDRESS");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.Street).HasColumnName("Street").HasMaxLength(100);
            e.Property(x => x.City).HasColumnName("City").HasMaxLength(50);
            e.Property(x => x.State).HasColumnName("State").HasMaxLength(2);
            e.Property(x => x.ZipCode).HasColumnName("ZipCode").HasMaxLength(10);
            e.Property(x => x.AddressFlag).HasColumnName("AddressFlag").HasConversion<string>();

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.Addresses)
                .HasForeignKey(x => x.DefendantId);
        });

        // DefPhone
        modelBuilder.Entity<DefPhone>(e =>
        {
            e.ToTable("DEF_PHONE");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.PhoneNumber).HasColumnName("PhoneNumber").HasMaxLength(20);
            e.Property(x => x.PhoneType).HasColumnName("PhoneType").HasMaxLength(20);

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.Phones)
                .HasForeignKey(x => x.DefendantId);
        });

        // DefSpouse
        modelBuilder.Entity<DefSpouse>(e =>
        {
            e.ToTable("DEF_SPOUSE");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.FirstName).HasColumnName("FirstName").HasMaxLength(50);
            e.Property(x => x.MiddleName).HasColumnName("MiddleName").HasMaxLength(50);
            e.Property(x => x.LastName).HasColumnName("LastName").HasMaxLength(50);
            e.Property(x => x.Employed).HasColumnName("Employed");
            e.Property(x => x.SpouseCounter).HasColumnName("SpouseCounter");

            e.HasOne(x => x.Defendant)
                .WithOne(d => d.Spouse)
                .HasForeignKey<DefSpouse>(x => x.DefendantId);
        });

        // DefAlias
        modelBuilder.Entity<DefAlias>(e =>
        {
            e.ToTable("DEF_ALIAS");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.FirstName).HasColumnName("FirstName").HasMaxLength(50);
            e.Property(x => x.MiddleName).HasColumnName("MiddleName").HasMaxLength(50);
            e.Property(x => x.LastName).HasColumnName("LastName").HasMaxLength(50);

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.Aliases)
                .HasForeignKey(x => x.DefendantId);
        });

        // Dependent
        modelBuilder.Entity<Dependent>(e =>
        {
            e.ToTable("DEPENDENT");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.FirstName).HasColumnName("FirstName").HasMaxLength(50);
            e.Property(x => x.MiddleName).HasColumnName("MiddleName").HasMaxLength(50);
            e.Property(x => x.LastName).HasColumnName("LastName").HasMaxLength(50);

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.Dependents)
                .HasForeignKey(x => x.DefendantId);
        });

        // Financial entities
        modelBuilder.Entity<FinEmployer>(e =>
        {
            e.ToTable("FIN_EMPLOYER");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.EmployerCounter).HasColumnName("EmployerCounter");
            e.Property(x => x.EmployerName).HasColumnName("EmployerName").HasMaxLength(100);
            e.Property(x => x.City).HasColumnName("City").HasMaxLength(50);
            e.Property(x => x.Phone).HasColumnName("Phone").HasMaxLength(20);
            e.Property(x => x.PayAmt).HasColumnName("PayAmt").HasPrecision(10, 2);
            e.Property(x => x.PayPeriod).HasColumnName("PayPeriod").HasMaxLength(20);
            e.Property(x => x.NetOrGross).HasColumnName("NetOrGross").HasMaxLength(10);
            e.Property(x => x.TimeEmployed).HasColumnName("TimeEmployed").HasMaxLength(50);

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.Employers)
                .HasForeignKey(x => x.DefendantId);
        });

        modelBuilder.Entity<FinSpEmployer>(e =>
        {
            e.ToTable("FIN_SPEMPLOYER");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.SpEmployerCounter).HasColumnName("SpunemployCounter");
            e.Property(x => x.EmployerName).HasColumnName("EmployerName").HasMaxLength(100);
            e.Property(x => x.City).HasColumnName("City").HasMaxLength(50);
            e.Property(x => x.Phone).HasColumnName("Phone").HasMaxLength(20);
            e.Property(x => x.PayAmt).HasColumnName("PayAmt").HasPrecision(10, 2);
            e.Property(x => x.PayPeriod).HasColumnName("PayPeriod").HasMaxLength(20);

            e.HasOne(x => x.Defendant)
                .WithMany()
                .HasForeignKey(x => x.DefendantId);
        });

        modelBuilder.Entity<FinUnemployed>(e =>
        {
            e.ToTable("FIN_UNEMPLOYED");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.UnemployCounter).HasColumnName("UnemployCounter");
            e.Property(x => x.IncomeSource).HasColumnName("IncomeSource").HasConversion<string>();
            e.Property(x => x.Description).HasColumnName("Description").HasMaxLength(255);
            e.Property(x => x.TimeUnemployed).HasColumnName("TimeUnemployed").HasMaxLength(50);
            e.Property(x => x.PayPeriod).HasColumnName("PayPeriod").HasMaxLength(20);
            e.Property(x => x.PayAmt).HasColumnName("PayAmt").HasPrecision(10, 2);

            e.HasOne(x => x.Defendant)
                .WithMany()
                .HasForeignKey(x => x.DefendantId);
        });

        modelBuilder.Entity<FinSpUnemploy>(e =>
        {
            e.ToTable("FIN_SPUNEMPLOY");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.SpUnemployCounter).HasColumnName("SpunemployCounter");
            e.Property(x => x.IncomeSource).HasColumnName("IncomeSource").HasConversion<string>();
            e.Property(x => x.Description).HasColumnName("Description").HasMaxLength(255);
            e.Property(x => x.TimeUnemployed).HasColumnName("TimeUnemployed").HasMaxLength(50);
            e.Property(x => x.PayPeriod).HasColumnName("PayPeriod").HasMaxLength(20);
            e.Property(x => x.PayAmt).HasColumnName("PayAmt").HasPrecision(10, 2);

            e.HasOne(x => x.Defendant)
                .WithMany()
                .HasForeignKey(x => x.DefendantId);
        });

        modelBuilder.Entity<FinAuto>(e =>
        {
            e.ToTable("FINANCE_AUTO");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.AutoCounter).HasColumnName("AutoCounter");
            e.Property(x => x.Model).HasColumnName("Model").HasMaxLength(50);
            e.Property(x => x.Year).HasColumnName("Year").HasMaxLength(4);
            e.Property(x => x.Balance).HasColumnName("Balance").HasPrecision(10, 2);

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.FinAutos)
                .HasForeignKey(x => x.DefendantId);
        });

        modelBuilder.Entity<FinBank>(e =>
        {
            e.ToTable("FINANCE_BANK");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.BankCounter).HasColumnName("BankCounter");
            e.Property(x => x.BankName).HasColumnName("BankName").HasMaxLength(100);
            e.Property(x => x.AccountType).HasColumnName("AccountType").HasMaxLength(50);
            e.Property(x => x.Balance).HasColumnName("Balance").HasPrecision(10, 2);

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.FinBanks)
                .HasForeignKey(x => x.DefendantId);
        });

        modelBuilder.Entity<FinHome>(e =>
        {
            e.ToTable("FINANCE_HOME");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.HomeCounter).HasColumnName("HomeCounter");
            e.Property(x => x.MortgagePay).HasColumnName("MortgagePay").HasPrecision(10, 2);
            e.Property(x => x.HomeValue).HasColumnName("HomeValue").HasPrecision(10, 2);
            e.Property(x => x.MortgageBalance).HasColumnName("MortgageBalance").HasPrecision(10, 2);

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.FinHomes)
                .HasForeignKey(x => x.DefendantId);
        });

        modelBuilder.Entity<FinRent>(e =>
        {
            e.ToTable("FINANCE_RENT");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.RentCounter).HasColumnName("RentCounter");
            e.Property(x => x.MonthlyRent).HasColumnName("MonthlyRent").HasPrecision(10, 2);

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.FinRents)
                .HasForeignKey(x => x.DefendantId);
        });

        modelBuilder.Entity<FinOther>(e =>
        {
            e.ToTable("FINANCE_OTHER");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.DefendantId).HasColumnName("DefendantID").HasMaxLength(9);
            e.Property(x => x.OtherCounter).HasColumnName("OtherCounter");
            e.Property(x => x.Type).HasColumnName("Type").HasMaxLength(50);
            e.Property(x => x.Description).HasColumnName("Description").HasMaxLength(255);
            e.Property(x => x.MonthlyAmount).HasColumnName("MonthlyAmount").HasPrecision(10, 2);
            e.Property(x => x.TotalAmount).HasColumnName("TotalAmount").HasPrecision(10, 2);

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.FinOthers)
                .HasForeignKey(x => x.DefendantId);
        });

        // Case management
        modelBuilder.Entity<Charge>(e =>
        {
            e.ToTable("CHARGE");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.ApplicationNumber).HasColumnName("ApplicationNumber");
            e.Property(x => x.ChargeNumber).HasColumnName("ChargeNumber");
            e.Property(x => x.ChargeType).HasColumnName("ChargeType").HasMaxLength(50);
            e.Property(x => x.CaseNumber).HasColumnName("CaseNumber").HasMaxLength(20);
            e.Property(x => x.ChargeDate).HasColumnName("ChargeDate");
            e.Property(x => x.AddCharge).HasColumnName("AddCharge").HasMaxLength(255);
            e.Property(x => x.WarrantNumber).HasColumnName("WarrantNumber").HasMaxLength(20);
            e.Property(x => x.ChargeId).HasColumnName("ChargeID").HasMaxLength(10);
            e.Property(x => x.Description).HasColumnName("Description").HasMaxLength(255);

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.Charges)
                .HasForeignKey(x => x.ApplicationNumber);
        });

        modelBuilder.Entity<Warrant>(e =>
        {
            e.ToTable("WARRANT");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.ApplicationNumber).HasColumnName("ApplicationNumber");
            e.Property(x => x.WarrantNumber).HasColumnName("WarrantNumber").HasMaxLength(20);
            e.Property(x => x.CaseNumber).HasColumnName("CaseNumber").HasMaxLength(20);
            e.Property(x => x.Date).HasColumnName("Date");
            e.Property(x => x.ArrestDate).HasColumnName("ArrestDate");
            e.Property(x => x.JurisdictionCode).HasColumnName("JurisdictionCode").HasConversion<string>();
            e.Property(x => x.BondType).HasColumnName("BondType").HasMaxLength(20);
            e.Property(x => x.BondAmt).HasColumnName("BondAmt").HasPrecision(10, 2);
            e.Property(x => x.Jail).HasColumnName("Jail");
            e.Property(x => x.AddOnCase).HasColumnName("AddOnCase").HasMaxLength(255);

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.Warrants)
                .HasForeignKey(x => x.ApplicationNumber);
        });

        modelBuilder.Entity<Appointment>(e =>
        {
            e.ToTable("APPOINTMENT");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.ApplicationNumber).HasColumnName("ApplicationNumber");
            e.Property(x => x.AttyCode).HasColumnName("AttyCode").HasMaxLength(10);
            e.Property(x => x.Date).HasColumnName("Date");
            e.Property(x => x.Action).HasColumnName("Action").HasMaxLength(5);
            e.Property(x => x.DateSigned).HasColumnName("DateSigned");
            e.Property(x => x.DenyCode).HasColumnName("DenyCode").HasMaxLength(10);
            e.Property(x => x.RemovalCode).HasColumnName("RemovalCode").HasMaxLength(10);
            e.Property(x => x.Bonded).HasColumnName("Bonded");
            e.Property(x => x.GAL).HasColumnName("GAL");
            e.Property(x => x.VoucherNumber).HasColumnName("VoucherNumber").HasMaxLength(20);
            e.Property(x => x.VoucherLetter).HasColumnName("VoucherLetter").HasMaxLength(5);
            e.Property(x => x.ContractCase).HasColumnName("ContractCase");
            e.Property(x => x.DUICourt).HasColumnName("DUICourt");
            e.Property(x => x.JuvenileSubstType).HasColumnName("JuvenileSubstType").HasMaxLength(50);

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.Appointments)
                .HasForeignKey(x => x.ApplicationNumber);

            e.HasOne(x => x.Attorney)
                .WithMany(a => a.Appointments)
                .HasForeignKey(x => x.AttyCode);
        });

        modelBuilder.Entity<Voucher>(e =>
        {
            e.ToTable("VOUCHER");
            e.HasKey(x => x.VoucherNumber);
            e.Property(x => x.VoucherNumber).HasColumnName("VoucherNumber").HasMaxLength(20);
            e.Property(x => x.VoucherLetter).HasColumnName("VoucherLetter").HasMaxLength(5);
            e.Property(x => x.ApplicationNumber).HasColumnName("ApplicationNumber");
            e.Property(x => x.AttyCode).HasColumnName("AttyCode").HasMaxLength(10);
            e.Property(x => x.DateVchrPaid).HasColumnName("DateVchrPaid");
            e.Property(x => x.DateCaseCompleted).HasColumnName("DateCaseCompleted");
            e.Property(x => x.InCourtHours).HasColumnName("InCourtHours").HasPrecision(5, 2);
            e.Property(x => x.OutCourtHours).HasColumnName("OutCourtHours").HasPrecision(5, 2);
            e.Property(x => x.CourtOrderedReimburse).HasColumnName("CourtOrderedReimburse").HasPrecision(10, 2);
            e.Property(x => x.TotalVoucherAmt).HasColumnName("TotalVoucherAmt").HasPrecision(10, 2);
            e.Property(x => x.TotalAmountPaid).HasColumnName("TotalAmountPaid").HasPrecision(10, 2);
            e.Property(x => x.Outcome).HasColumnName("Outcome").HasConversion<string>();
            e.Property(x => x.OutcomeOther).HasColumnName("OutcomeOther").HasMaxLength(255);

            // ... remaining voucher properties map similarly

            e.HasOne(x => x.Defendant)
                .WithMany(d => d.Vouchers)
                .HasForeignKey(x => x.ApplicationNumber);

            e.HasOne(x => x.Attorney)
                .WithMany(a => a.Vouchers)
                .HasForeignKey(x => x.AttyCode);
        });

        modelBuilder.Entity<EIA>(e =>
        {
            e.ToTable("EIA");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.ApplicationNumber).HasColumnName("ApplicationNumber");
            e.Property(x => x.Judge).HasColumnName("Judge").HasMaxLength(50);
            e.Property(x => x.EIAResult).HasColumnName("EIAResult").HasMaxLength(50);
            e.Property(x => x.Jail).HasColumnName("jail").HasMaxLength(50);
            e.Property(x => x.Probation).HasColumnName("probation").HasMaxLength(50);
            e.Property(x => x.Reimbursement).HasColumnName("reimbursement").HasPrecision(10, 2);
            e.Property(x => x.Bond).HasColumnName("bond").HasPrecision(10, 2);

            e.HasOne(x => x.Defendant)
                .WithOne(d => d.EIA)
                .HasForeignKey<EIA>(x => x.ApplicationNumber);
        });

        // Lookups
        modelBuilder.Entity<ChargeId>(e =>
        {
            e.ToTable("CHARGE_ID");
            e.HasKey(x => x.ChargeIdCode);
            e.Property(x => x.ChargeIdCode).HasColumnName("ChargeID").HasMaxLength(10);
            e.Property(x => x.Description).HasColumnName("Description").HasMaxLength(255);
            e.Property(x => x.ChargeSelect).HasColumnName("CHARGESELECT").HasMaxLength(50);
        });

        modelBuilder.Entity<DenialCode>(e =>
        {
            e.ToTable("DENIAL_CODE");
            e.HasKey(x => x.DenyCode);
            e.Property(x => x.DenyCode).HasColumnName("DenyCode").HasMaxLength(10);
            e.Property(x => x.Description).HasColumnName("Description").HasMaxLength(255);
            e.Property(x => x.LongText).HasColumnName("Long text").HasMaxLength(100);
        });

        modelBuilder.Entity<RemovalCode>(e =>
        {
            e.ToTable("REMOVAL_CODE");
            e.HasKey(x => x.RemovalCodeValue);
            e.Property(x => x.RemovalCodeValue).HasColumnName("RemovalCode").HasMaxLength(10);
            e.Property(x => x.Description).HasColumnName("Description").HasMaxLength(255);
            e.Property(x => x.Statement).HasColumnName("Statement").HasMaxLength(100);
        });

        modelBuilder.Entity<Jurisdiction>(e =>
        {
            e.ToTable("JURISDICTION");
            e.HasKey(x => x.JurisdictionCode);
            e.Property(x => x.JurisdictionCode).HasColumnName("JurisdictionCode").HasMaxLength(5);
            e.Property(x => x.Description).HasColumnName("Description").HasMaxLength(50);
        });

        modelBuilder.Entity<Judge>(e =>
        {
            e.ToTable("JUDGE");
            e.HasKey(x => x.JudgeCode);
            e.Property(x => x.JudgeCode).HasColumnName("JudgeCode").HasMaxLength(10);
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(50);
        });

        modelBuilder.Entity<IncomeSource>(e =>
        {
            e.ToTable("INCOME_SOURCE");
            e.HasKey(x => x.IncomeSourceCode);
            e.Property(x => x.IncomeSourceCode).HasColumnName("IncomeSource").HasMaxLength(4);
            e.Property(x => x.Description).HasColumnName("Description").HasMaxLength(20);
        });

        modelBuilder.Entity<Core.Entities.Type>(e =>
        {
            e.ToTable("TYPE");
            e.HasKey(x => x.TypeCode);
            e.Property(x => x.TypeCode).HasColumnName("TYPE").HasMaxLength(2);
            e.Property(x => x.TypeDescription).HasColumnName("TYPEDES").HasMaxLength(50);
        });
    }
}
