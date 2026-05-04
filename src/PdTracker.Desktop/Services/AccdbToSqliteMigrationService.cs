using System.Data;
using System.Data.OleDb;
using Microsoft.EntityFrameworkCore;
using PdTracker.Core.Entities;
using PdTracker.Data.DbContext;

namespace PdTracker.Desktop.Services;

public class AccdbToSqliteMigrationService
{
    private readonly string _accdbPath;
    private readonly string _sqlitePath;
    private OleDbConnection? _accdbConn;

    public event Action<string>? OnProgress;

    public AccdbToSqliteMigrationService(string accdbPath, string sqlitePath)
    {
        _accdbPath = accdbPath;
        _sqlitePath = sqlitePath;
    }

    public void TestAccessConnection()
    {
        var connString = BuildAceConnectionString(_accdbPath);
        _accdbConn = new OleDbConnection(connString);
        _accdbConn.Open();
    }

    public void Run()
    {
        if (_accdbConn == null || _accdbConn.State != ConnectionState.Open)
            TestAccessConnection();

        CreateSqliteDatabase();

        MigrateTable("DEFENDANT", MigrateDefendant);
        MigrateTable("ATTORNEY_LIST", MigrateAttorneyList);
        MigrateTable("QUALIFY", MigrateQualify);
        MigrateTable("DEF_ADDRESS", MigrateDefAddress);
        MigrateTable("DEF_PHONE", MigrateDefPhone);
        MigrateTable("DEF_SPOUSE", MigrateDefSpouse);
        MigrateTable("DEF_ALIAS", MigrateDefAlias);
        MigrateTable("DEPENDENT", MigrateDependent);
        MigrateTable("FIN_EMPLOYER", MigrateFinEmployer);
        MigrateTable("FIN_SPEMPLOYER", MigrateFinSpEmployer);
        MigrateTable("FIN_UNEMPLOYED", MigrateFinUnemployed);
        MigrateTable("FIN_SPUNEMPLOY", MigrateFinSpUnemploy);
        MigrateTable("FINANCE_AUTO", MigrateFinAuto);
        MigrateTable("FINANCE_BANK", MigrateFinBank);
        MigrateTable("FINANCE_HOME", MigrateFinHome);
        MigrateTable("FINANCE_RENT", MigrateFinRent);
        MigrateTable("FINANCE_OTHER", MigrateFinOther);
        MigrateTable("CHARGE", MigrateCharge);
        MigrateTable("WARRANT", MigrateWarrant);
        MigrateTable("APPOINTMENT", MigrateAppointment);
        MigrateTable("VOUCHER", MigrateVoucher);
        MigrateTable("EIA", MigrateEIA);
        MigrateTable("CHARGE_ID", MigrateChargeId);
        MigrateTable("DENIAL_CODE", MigrateDenialCode);
        MigrateTable("REMOVAL_CODE", MigrateRemovalCode);
        MigrateTable("JURISDICTION", MigrateJurisdiction);
        MigrateTable("JUDGE", MigrateJudge);
        MigrateTable("INCOME_SOURCE", MigrateIncomeSource);
        MigrateTable("TYPE", MigrateType);

        OnProgress?.Invoke("Migration complete.");
    }

    private void CreateSqliteDatabase()
    {
        OnProgress?.Invoke("Creating SQLite database...");

        var optionsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optionsBuilder.UseSqlite($"Data Source={_sqlitePath}");

        using var db = new PdTrackerDbContext(optionsBuilder.Options);
        db.Database.EnsureCreated();
    }

    private void MigrateTable(string tableName, Action<OleDbDataReader> migrateFn)
    {
        OnProgress?.Invoke($"  Migrating {tableName}...");
        try
        {
            using var cmd = _accdbConn!.CreateCommand();
            cmd.CommandText = $"SELECT * FROM [{tableName}]";
            using var reader = cmd.ExecuteReader();
            migrateFn(reader);
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  WARNING: {tableName} skipped — {ex.Message}");
        }
    }

    private static DbContextOptionsBuilder<PdTrackerDbContext> MakeOpts(string dbPath)
    {
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={dbPath}");
        return optsBuilder;
    }

    private void MigrateDefendant(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new Defendant
            {
                DefendantId = GetString(r, "DefendantID", 9),
                ApplicationNumber = GetInt32(r, "ApplicationNumber"),
                SOID = GetString(r, "SOID", 255),
                FirstName = GetString(r, "FirstName", 50),
                MiddleName = GetString(r, "MiddleName", 50),
                LastName = GetString(r, "LastName", 50),
                DOB = GetDateTime(r, "DOB"),
                Education = GetString(r, "Education", 50),
                Race = GetString(r, "Race", 255),
                Sex = GetString(r, "Sex", 255),
                Reference1 = GetString(r, "Reference1", 75),
                Reference2 = GetString(r, "Reference2", 75),
                Dependants = GetInt32Nullable(r, "dependants"),
                DepDescription = GetString(r, "dep_description", 25),
                DepOther = GetString(r, "dep_other", 20),
                DateAdded = GetDateTime(r, "DateAdded"),
            };
            db.Defendants.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateAttorneyList(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new AttorneyList
            {
                AttyCode = GetString(r, "AttyCode", 10),
                FirstName = GetString(r, "FirstName", 50),
                MiddleName = GetString(r, "MiddleName", 50),
                LastName = GetString(r, "LastName", 50),
                Street = GetString(r, "Street", 100),
                Suite = GetString(r, "Suite", 20),
                City = GetString(r, "City", 50),
                State = GetString(r, "State", 2),
                ZipCode = GetString(r, "ZipCode", 10),
                Email = GetString(r, "Email", 100),
                OfficeNumber = GetString(r, "OfficeNumber", 20),
                FaxNumber = GetString(r, "FaxNumber", 20),
                HomeNumber = GetString(r, "HomeNumber", 20),
                PagerNumber = GetString(r, "PagerNumber", 20),
                MobileNumber = GetString(r, "MobileNumber", 20),
                OtherNumber = GetString(r, "OtherNumber", 20),
                PhoneType = GetString(r, "PhoneType", 20),
                Date = GetDateTime(r, "Date"),
                Status = GetEnum<AttorneyStatus>(r, "Status"),
                DeathPenalty = GetBool(r, "Deathpenalty"),
                Murder = GetBool(r, "Murder"),
                Felony = GetBool(r, "Felony"),
                Misd = GetBool(r, "Misd"),
                Appeal = GetBool(r, "Appeal"),
                Juvenile = GetBool(r, "Juvenile"),
                GAL = GetBool(r, "GAL"),
                VendorNumber = GetString(r, "VenderNumber", 20),
                Notes = GetString(r, "Notes", 255),
            };
            db.AttorneyLists.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateQualify(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new Qualify
            {
                ApplicationNumber = GetInt32(r, "ApplicationNumber"),
                Date = GetDateTime(r, "Date"),
                NoAction = GetBool(r, "NoAction"),
                Comment = GetString(r, "Comment", 1000),
                CourtInformation = GetString(r, "CourtInformation", 1000),
                Military = GetBool(r, "Military"),
                EntryDate = GetDateTime(r, "Entrydate"),
                DefendantId = GetString(r, "DefendantID", 9),
            };
            db.Qualifies.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateDefAddress(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new DefAddress
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                Street = GetString(r, "Street", 100),
                City = GetString(r, "City", 50),
                State = GetString(r, "State", 2),
                ZipCode = GetString(r, "ZipCode", 10),
                AddressFlag = GetEnum<AddressFlag>(r, "AddressFlag"),
            };
            db.DefAddresses.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateDefPhone(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new DefPhone
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                PhoneNumber = GetString(r, "PhoneNumber", 20),
                PhoneType = GetString(r, "PhoneType", 20),
            };
            db.DefPhones.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateDefSpouse(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new DefSpouse
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                FirstName = GetString(r, "FirstName", 50),
                MiddleName = GetString(r, "MiddleName", 50),
                LastName = GetString(r, "LastName", 50),
                Employed = GetBool(r, "Employed"),
                SpouseCounter = GetInt32Nullable(r, "SpouseCounter"),
            };
            db.DefSpouses.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateDefAlias(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new DefAlias
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                FirstName = GetString(r, "FirstName", 50),
                MiddleName = GetString(r, "MiddleName", 50),
                LastName = GetString(r, "LastName", 50),
            };
            db.DefAliases.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateDependent(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new Dependent
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                FirstName = GetString(r, "FirstName", 50),
                MiddleName = GetString(r, "MiddleName", 50),
                LastName = GetString(r, "LastName", 50),
            };
            db.Dependents.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateFinEmployer(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new FinEmployer
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                EmployerCounter = GetInt32Nullable(r, "EmployerCounter"),
                EmployerName = GetString(r, "EmployerName", 100),
                City = GetString(r, "City", 50),
                Phone = GetString(r, "Phone", 20),
                PayAmt = GetDecimal(r, "PayAmt"),
                PayPeriod = GetString(r, "PayPeriod", 20),
                NetOrGross = GetString(r, "NetOrGross", 10),
                TimeEmployed = GetString(r, "TimeEmployed", 50),
            };
            db.FinEmployers.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateFinSpEmployer(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new FinSpEmployer
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                SpEmployerCounter = GetInt32Nullable(r, "SpemployCounter"),
                EmployerName = GetString(r, "EmployerName", 100),
                City = GetString(r, "City", 50),
                Phone = GetString(r, "Phone", 20),
                PayAmt = GetDecimal(r, "PayAmt"),
                PayPeriod = GetString(r, "PayPeriod", 20),
            };
            db.FinSpEmployers.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateFinUnemployed(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new FinUnemployed
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                UnemployCounter = GetInt32Nullable(r, "UnemployCounter"),
                IncomeSource = GetEnum<IncomeSourceType>(r, "IncomeSource"),
                Description = GetString(r, "Description", 255),
                TimeUnemployed = GetString(r, "TimeUnemployed", 50),
                PayPeriod = GetString(r, "PayPeriod", 20),
                PayAmt = GetDecimal(r, "PayAmt"),
            };
            db.FinUnemployeds.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateFinSpUnemploy(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new FinSpUnemploy
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                SpUnemployCounter = GetInt32Nullable(r, "SpunemployCounter"),
                IncomeSource = GetEnum<IncomeSourceType>(r, "IncomeSource"),
                Description = GetString(r, "Description", 255),
                TimeUnemployed = GetString(r, "TimeUnemployed", 50),
                PayPeriod = GetString(r, "PayPeriod", 20),
                PayAmt = GetDecimal(r, "PayAmt"),
            };
            db.FinSpUnemploys.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateFinAuto(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new FinAuto
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                AutoCounter = GetInt32Nullable(r, "AutoCounter"),
                Model = GetString(r, "Model", 50),
                Year = GetString(r, "Year", 4),
                Balance = GetDecimal(r, "Balance"),
            };
            db.FinAutos.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateFinBank(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new FinBank
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                BankCounter = GetInt32Nullable(r, "BankCounter"),
                BankName = GetString(r, "BankName", 100),
                AccountType = GetString(r, "AccountType", 50),
                Balance = GetDecimal(r, "Balance"),
            };
            db.FinBanks.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateFinHome(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new FinHome
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                HomeCounter = GetInt32Nullable(r, "HomeCounter"),
                MortgagePay = GetDecimal(r, "MortgagePay"),
                HomeValue = GetDecimal(r, "HomeValue"),
                MortgageBalance = GetDecimal(r, "MortgageBalance"),
            };
            db.FinHomes.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateFinRent(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new FinRent
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                RentCounter = GetInt32Nullable(r, "RentCounter"),
                MonthlyRent = GetDecimal(r, "MonthlyRent"),
            };
            db.FinRents.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateFinOther(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new FinOther
            {
                DefendantId = GetString(r, "ApplicationNumber", 9),
                OtherCounter = GetInt32Nullable(r, "OtherCounter"),
                Type = GetString(r, "Type", 50),
                Description = GetString(r, "Description", 255),
                MonthlyAmount = GetDecimal(r, "MonthlyAmount"),
                TotalAmount = GetDecimal(r, "TotalAmount"),
            };
            db.FinOthers.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateCharge(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new Charge
            {
                ApplicationNumber = GetInt32(r, "ApplicationNumber"),
                ChargeNumber = GetInt32Nullable(r, "ChargeNumber"),
                ChargeType = GetString(r, "ChargeType", 50),
                CaseNumber = GetString(r, "CaseNumber", 20),
                ChargeDate = GetDateTime(r, "ChargeDate"),
                AddCharge = GetString(r, "AddCharge", 255),
                WarrantNumber = GetString(r, "WarrantNumber", 20),
                ChargeId = GetString(r, "ChargeID", 20),
                Description = GetString(r, "Description", 255),
            };
            db.Charges.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateWarrant(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new Warrant
            {
                ApplicationNumber = GetInt32(r, "ApplicationNumber"),
                WarrantNumber = GetString(r, "WarrantNumber", 20),
                CaseNumber = GetString(r, "CaseNumber", 20),
                Date = GetDateTime(r, "Date"),
                ArrestDate = GetDateTime(r, "ArrestDate"),
                JurisdictionCode = GetEnumNullable<JurisdictionCode>(r, "JurisdictionCode"),
                BondType = GetString(r, "BondType", 20),
                BondAmt = GetDecimal(r, "BondAmt"),
                Jail = GetBoolNullable(r, "Jail"),
                AddOnCase = GetString(r, "AddOnCase", 255),
            };
            db.Warrants.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateAppointment(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new Appointment
            {
                ApplicationNumber = GetInt32(r, "ApplicationNumber"),
                AttyCode = GetString(r, "AttyCode", 10),
                Date = GetDateTime(r, "Date"),
                Action = GetString(r, "Action", 5),
                DateSigned = GetDateTime(r, "DateSigned"),
                DenyCode = GetString(r, "DenyCode", 10),
                RemovalCode = GetString(r, "RemovalCode", 10),
                Bonded = GetBoolNullable(r, "Bonded"),
                GAL = GetBoolNullable(r, "GAL"),
                VoucherNumber = GetString(r, "VoucherNumber", 20),
                VoucherLetter = GetString(r, "VoucherLetter", 5),
                ContractCase = GetBoolNullable(r, "ContractCase"),
                DUICourt = GetBoolNullable(r, "DUICourt"),
                JuvenileSubstType = GetString(r, "JuvenileSubstType", 50),
            };
            db.Appointments.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateVoucher(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new Voucher
            {
                VoucherNumber = GetString(r, "VoucherNumber", 20),
                VoucherLetter = GetString(r, "VoucherLetter", 5),
                ApplicationNumber = GetInt32Nullable(r, "ApplicationNumber") ?? 0,
                AttyCode = GetString(r, "AttyCode", 10),
                DateVchrPaid = GetDateTime(r, "DateVchrPaid"),
                DateCaseCompleted = GetDateTime(r, "DateCaseCompleted"),
                InCourtHours = GetDecimal(r, "InCourtHours"),
                OutCourtHours = GetDecimal(r, "OutCourtHours"),
                CourtOrderedReimburse = GetDecimal(r, "CourtOrderedReimburse"),
                TotalVoucherAmt = GetDecimal(r, "TotalVoucherAmt"),
                TotalAmountPaid = GetDecimal(r, "TotalAmountPaid"),
                Outcome = GetEnumNullable<VoucherOutcome>(r, "Outcome"),
                OutcomeOther = GetString(r, "OutcomeOther", 255),
            };
            db.Vouchers.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateEIA(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        // Build lookup: ApplicationNumber → DefendantId from already-migrated DEFENDANT table
        var defendantLookup = db.Defendants
            .ToDictionary(d => d.ApplicationNumber, d => d.DefendantId);

        while (r.Read())
        {
            var appNum = GetInt32(r, "ApplicationNumber");
            var e = new EIA
            {
                ApplicationNumber = appNum,
                DefendantId = defendantLookup.GetValueOrDefault(appNum),
                Judge = GetString(r, "Judge", 50),
                EIAResult = GetString(r, "EIAResult", 50),
                Jail = GetString(r, "jail", 50),
                Probation = GetString(r, "probation", 50),
                Reimbursement = GetDecimal(r, "Reimbursement"),
                Bond = GetDecimal(r, "bond"),
            };
            db.EIAs.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateChargeId(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new ChargeId
            {
                ChargeIdCode = GetString(r, "ChargeID", 10),
                Description = GetString(r, "Description", 255),
                ChargeSelect = GetString(r, "CHARGESELECT", 50),
            };
            db.ChargeIds.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateDenialCode(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new DenialCode
            {
                DenyCode = GetString(r, "DenyCode", 2),
                Description = GetString(r, "Description", 30),
                LongText = GetString(r, "Long text", 100),
            };
            db.DenialCodes.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateRemovalCode(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new RemovalCode
            {
                RemovalCodeValue = GetString(r, "RemovalCode", 2),
                Description = GetString(r, "Description", 20),
                Statement = GetString(r, "Statement", 100),
            };
            db.RemovalCodes.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateJurisdiction(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new Jurisdiction
            {
                JurisdictionCode = GetString(r, "JurisdictionCode", 3),
                Description = GetString(r, "Description", 30),
            };
            db.Jurisdictions.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateJudge(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new Judge
            {
                JudgeCode = GetString(r, "JudgeCode", 2),
                Description = GetString(r, "description", 20),
            };
            db.Judges.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateIncomeSource(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new IncomeSource
            {
                IncomeSourceCode = GetString(r, "IncomeSource", 4),
                Description = GetString(r, "Description", 20),
            };
            db.IncomeSources.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateType(OleDbDataReader r)
    {
        using var db = new PdTrackerDbContext(MakeOpts(_sqlitePath).Options);
        while (r.Read())
        {
            var e = new Core.Entities.Type
            {
                TypeCode = GetString(r, "TYPE", 2),
                TypeDescription = GetString(r, "TYPEDES", 50),
            };
            db.Types.Add(e);
        }
        db.SaveChanges();
    }

    // --- Safe accessors ---
    private static string GetString(OleDbDataReader r, string name, int maxLen)
    {
        try
        {
            var val = r[name];
            if (val == null || val == DBNull.Value) return "";
            var s = val.ToString() ?? "";
            return s.Length > maxLen ? s[..maxLen] : s;
        }
        catch { return ""; }
    }

    private static int GetInt32(OleDbDataReader r, string name)
    {
        try
        {
            var val = r[name];
            if (val == null || val == DBNull.Value) return 0;
            return Convert.ToInt32(val);
        }
        catch { return 0; }
    }

    private static int? GetInt32Nullable(OleDbDataReader r, string name)
    {
        try
        {
            var val = r[name];
            if (val == null || val == DBNull.Value) return null;
            return Convert.ToInt32(val);
        }
        catch { return null; }
    }

    private static bool GetBool(OleDbDataReader r, string name)
    {
        try
        {
            var val = r[name];
            if (val == null || val == DBNull.Value) return false;
            if (val is bool b) return b;
            if (val is int i) return i != 0;
            var s = val.ToString()?.ToUpperInvariant();
            return s == "TRUE" || s == "T" || s == "1" || s == "-1";
        }
        catch { return false; }
    }

    private static bool? GetBoolNullable(OleDbDataReader r, string name)
    {
        try
        {
            var val = r[name];
            if (val == null || val == DBNull.Value) return null;
            if (val is bool b) return b;
            if (val is int i) return i != 0;
            var s = val.ToString()?.ToUpperInvariant();
            return s == "TRUE" || s == "T" || s == "1" || s == "-1";
        }
        catch { return null; }
    }

    private static decimal GetDecimal(OleDbDataReader r, string name)
    {
        try
        {
            var val = r[name];
            if (val == null || val == DBNull.Value) return 0m;
            return Convert.ToDecimal(val);
        }
        catch { return 0m; }
    }

    private static DateTime GetDateTime(OleDbDataReader r, string name)
    {
        try
        {
            var val = r[name];
            if (val == null || val == DBNull.Value) return DateTime.MinValue;
            return Convert.ToDateTime(val);
        }
        catch { return DateTime.MinValue; }
    }

    private static T GetEnum<T>(OleDbDataReader r, string name) where T : struct, Enum
    {
        try
        {
            var val = r[name];
            if (val == null || val == DBNull.Value) return default;
            var s = val.ToString() ?? "";
            return Enum.TryParse<T>(s, true, out var result) ? result : default;
        }
        catch { return default; }
    }

    private static T? GetEnumNullable<T>(OleDbDataReader r, string name) where T : struct, Enum
    {
        try
        {
            var val = r[name];
            if (val == null || val == DBNull.Value) return null;
            var s = val.ToString() ?? "";
            return Enum.TryParse<T>(s, true, out var result) ? result : null;
        }
        catch { return null; }
    }

    private static string BuildAceConnectionString(string accdbPath)
    {
        return $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={accdbPath};Persist Security Info=False;";
    }
}
