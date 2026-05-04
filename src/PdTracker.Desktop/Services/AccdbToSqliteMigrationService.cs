using System.Data;
using System.Data.OleDb;
using System.Globalization;
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
    public event Action<int>? OnPercentComplete;

    public AccdbToSqliteMigrationService(string accdbPath, string sqlitePath)
    {
        _accdbPath = accdbPath;
        _sqlitePath = sqlitePath;
    }

    /// <summary>
    /// Opens the accdb file. Throws if the Access driver is not installed
    /// or the file is not accessible.
    /// </summary>
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

        // Create the SQLite file and seed schema via EF migrations
        CreateSqliteDatabase();

        // Migrate data table by table
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

        var dbPath = _sqlitePath;
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var optionsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        using var db = new PdTrackerDbContext(optionsBuilder.Options);
        db.Database.EnsureCreated(); // Creates all tables from EF model
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

    private void MigrateDefendant(OleDbDataReader r)
    {
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

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
                Dependants = GetInt32(r, "dependants"),
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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

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
                Status = GetString(r, "Status", 20),
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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new DefAddress
            {
                Id = GetInt32(r, "AddressCounter"),
                DefendantId = GetString(r, "ApplicationNumber", 9),
                Street = GetString(r, "Street", 100),
                City = GetString(r, "City", 50),
                State = GetString(r, "State", 2),
                ZipCode = GetString(r, "ZipCode", 10),
                AddressFlag = GetString(r, "AddressFlag", 1),
            };
            db.DefAddresses.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateDefPhone(OleDbDataReader r)
    {
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new DefPhone
            {
                Id = GetInt32(r, "PhoneCounter"),
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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new DefSpouse
            {
                Id = GetInt32(r, "SpouseCounter"),
                DefendantId = GetString(r, "ApplicationNumber", 9),
                FirstName = GetString(r, "FirstName", 50),
                MiddleName = GetString(r, "MiddleName", 50),
                LastName = GetString(r, "LastName", 50),
                Employed = GetBool(r, "Employed"),
                SpouseCounter = GetString(r, "Relationship", 20),
            };
            db.DefSpouses.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateDefAlias(OleDbDataReader r)
    {
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new DefAlias
            {
                Id = GetInt32(r, "AliasCounter"),
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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new Dependent
            {
                Id = GetInt32(r, "DependentCounter"),
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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new FinEmployer
            {
                Id = GetInt32(r, "EmployerCounter"),
                DefendantId = GetString(r, "ApplicationNumber", 9),
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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new FinSpEmployer
            {
                Id = GetInt32(r, "SpemployCounter"),
                DefendantId = GetString(r, "ApplicationNumber", 9),
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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new FinUnemployed
            {
                Id = GetInt32(r, "UnemployCounter"),
                DefendantId = GetString(r, "ApplicationNumber", 9),
                IncomeSource = GetString(r, "IncomeSource", 20),
                Description = GetString(r, "Description", 255),
                PayPeriod = GetString(r, "PayPeriod", 20),
                PayAmt = GetDecimal(r, "PayAmt"),
                TimeUnemployed = GetString(r, "TimeUnemployed", 50),
            };
            db.FinUnemployeds.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateFinSpUnemploy(OleDbDataReader r)
    {
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new FinSpUnemploy
            {
                Id = GetInt32(r, "SpunemployCounter"),
                DefendantId = GetString(r, "ApplicationNumber", 9),
                IncomeSource = GetString(r, "IncomeSource", 20),
                Description = GetString(r, "Description", 255),
                PayPeriod = GetString(r, "PayPeriod", 20),
                PayAmt = GetDecimal(r, "PayAmt"),
                TimeUnemployed = GetString(r, "TimeUnemployed", 50),
            };
            db.FinSpUnemploys.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateFinAuto(OleDbDataReader r)
    {
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new FinAuto
            {
                Id = GetInt32(r, "AutoCounter"),
                DefendantId = GetString(r, "ApplicationNumber", 9),
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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new FinBank
            {
                Id = GetInt32(r, "BankCounter"),
                DefendantId = GetString(r, "ApplicationNumber", 9),
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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new FinHome
            {
                Id = GetInt32(r, "HomeCounter"),
                DefendantId = GetString(r, "ApplicationNumber", 9),
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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new FinRent
            {
                Id = GetInt32(r, "RentCounter"),
                DefendantId = GetString(r, "ApplicationNumber", 9),
                MonthlyRent = GetDecimal(r, "MonthlyRent"),
            };
            db.FinRents.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateFinOther(OleDbDataReader r)
    {
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new FinOther
            {
                Id = GetInt32(r, "OtherCounter"),
                DefendantId = GetString(r, "ApplicationNumber", 9),
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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new Charge
            {
                ApplicationNumber = GetInt32(r, "ApplicationNumber"),
                ChargeNumber = GetString(r, "ChargeNumber", 20),
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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new Warrant
            {
                ApplicationNumber = GetInt32(r, "ApplicationNumber"),
                WarrantNumber = GetString(r, "WarrantNumber", 20),
                CaseNumber = GetString(r, "CaseNumber", 20),
                Date = GetDateTime(r, "Date"),
                ArrestDate = GetDateTime(r, "ArrestDate"),
                JurisdictionCode = GetString(r, "JurisdictionCode", 10),
                BondType = GetString(r, "BondType", 20),
                BondAmt = GetDecimal(r, "BondAmt"),
                Jail = GetBool(r, "Jail"),
                AddOnCase = GetString(r, "AddOnCase", 255),
            };
            db.Warrants.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateAppointment(OleDbDataReader r)
    {
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

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
                Bonded = GetBool(r, "Bonded"),
                GAL = GetBool(r, "GAL"),
                VoucherNumber = GetString(r, "VoucherNumber", 20),
                VoucherLetter = GetString(r, "VoucherLetter", 5),
                ContractCase = GetBool(r, "ContractCase"),
                DUICourt = GetBool(r, "DUICourt"),
                JuvenileSubstType = GetString(r, "JuvenileSubstType", 50),
            };
            db.Appointments.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateVoucher(OleDbDataReader r)
    {
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new Voucher
            {
                VoucherNumber = GetString(r, "VoucherNumber", 20),
                VoucherLetter = GetString(r, "VoucherLetter", 5),
                ApplicationNumber = GetInt32(r, "ApplicationNumber"),
                AttyCode = GetString(r, "AttyCode", 10),
                DateVchrPaid = GetDateTime(r, "DateVchrPaid"),
                DateCaseCompleted = GetDateTime(r, "DateCaseCompleted"),
                InCourtHours = GetDecimal(r, "InCourtHours"),
                OutCourtHours = GetDecimal(r, "OutCourtHours"),
                CourtOrderedReimburse = GetDecimal(r, "CourtOrderedReimburse"),
                TotalVoucherAmt = GetDecimal(r, "TotalVoucherAmt"),
                TotalAmountPaid = GetDecimal(r, "TotalAmountPaid"),
                Outcome = GetString(r, "Outcome", 50),
                OutcomeOther = GetString(r, "OutcomeOther", 255),
            };
            db.Vouchers.Add(e);
        }
        db.SaveChanges();
    }

    private void MigrateEIA(OleDbDataReader r)
    {
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new EIA
            {
                ApplicationNumber = GetInt32(r, "ApplicationNumber"),
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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

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
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        using var db = new PdTrackerDbContext(optsBuilder.Options);

        while (r.Read())
        {
            var e = new Type
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

    private static bool GetBool(OleDbDataReader r, string name)
    {
        try
        {
            var val = r[name];
            if (val == null || val == DBNull.Value) return false;
            if (val is bool b) return b;
            var s = val.ToString()?.ToUpperInvariant();
            return s == "TRUE" || s == "T" || s == "1" || s == "-1";
        }
        catch { return false; }
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

    private static string BuildAceConnectionString(string accdbPath)
    {
        // ACE 12/14 provider — works with Access 2007+ .accdb files
        // Provider must match the bitness of the running process (64-bit here)
        return $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={accdbPath};Persist Security Info=False;";
    }
}
