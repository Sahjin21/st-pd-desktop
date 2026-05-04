using System.Runtime.InteropServices;
using Microsoft.Office.Interop.Access.Dao;
using Microsoft.EntityFrameworkCore;
using PdTracker.Core.Entities;
using PdTracker.Data.DbContext;

namespace PdTracker.Desktop.Services;

/// <summary>
/// Migrates data from a legacy Access .accdb file to SQLite.
/// Uses DAO to distinguish local tables from linked tables — linked tables
/// (which forward to an inaccessible BE) are skipped entirely.
/// </summary>
public class AccdbToSqliteMigrationService
{
    private readonly string _accdbPath;
    private readonly string _sqlitePath;
    private DBEngine? _daoEngine;
    private Database? _daoDb;

    // DAO table attribute flags
    private const int dbAttachedTable = 0x80000002; // linked table
    private const int dbHiddenObject = 0x00000001;   // hidden system object

    public event Action<string>? OnProgress;

    public AccdbToSqliteMigrationService(string accdbPath, string sqlitePath)
    {
        _accdbPath = accdbPath;
        _sqlitePath = sqlitePath;
    }

    public void TestAccessConnection()
    {
        _daoEngine = new DBEngine();
        _daoDb = _daoEngine.OpenDatabase(_accdbPath);
    }

    public void Run()
    {
        if (_daoDb == null)
            TestAccessConnection();

        CreateSqliteDatabase();

        // Use DAO to enumerate tables and filter local-only
        var tableDefs = _daoDb!.TableDefs;

        var localTables = new List<(string Name, bool IsLocal)>();
        for (int i = 0; i < tableDefs.Count; i++)
        {
            var td = tableDefs[i];
            var name = td.Name;
            var attrs = td.Attributes;

            // Skip hidden/system tables and MSys* tables
            if (name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("~", StringComparison.Ordinal))
                continue;

            // Skip linked tables (attrs & dbAttachedTable != 0)
            bool isLinked = (attrs & dbAttachedTable) != 0;
            localTables.Add((name, !isLinked));
        }

        // Log table inventory
        OnProgress?.Invoke($"Found {localTables.Count} table(s) in accdb");
        foreach (var (name, isLocal) in localTables)
            OnProgress?.Invoke($"  {(isLocal ? "LOCAL " : "LINKED")} : {name}");

        // Migrate each local table
        if (TableHasData("DEFENDANT")) MigrateDefendant();
        if (TableHasData("ATTORNEY_LIST")) MigrateAttorneyList();
        if (TableHasData("QUALIFY")) MigrateQualify();
        if (TableHasData("DEF_ADDRESS")) MigrateDefAddress();
        if (TableHasData("DEF_PHONE")) MigrateDefPhone();
        if (TableHasData("DEF_SPOUSE")) MigrateDefSpouse();
        if (TableHasData("DEF_ALIAS")) MigrateDefAlias();
        if (TableHasData("DEPENDENT")) MigrateDependent();
        if (TableHasData("FIN_EMPLOYER")) MigrateFinEmployer();
        if (TableHasData("FIN_SPEMPLOYER")) MigrateFinSpEmployer();
        if (TableHasData("FIN_UNEMPLOYED")) MigrateFinUnemployed();
        if (TableHasData("FIN_SPUNEMPLOY")) MigrateFinSpUnemploy();
        if (TableHasData("FINANCE_AUTO")) MigrateFinAuto();
        if (TableHasData("FINANCE_BANK")) MigrateFinBank();
        if (TableHasData("FINANCE_HOME")) MigrateFinHome();
        if (TableHasData("FINANCE_RENT")) MigrateFinRent();
        if (TableHasData("FINANCE_OTHER")) MigrateFinOther();
        if (TableHasData("CHARGE")) MigrateCharge();
        if (TableHasData("WARRANT")) MigrateWarrant();
        if (TableHasData("APPOINTMENT")) MigrateAppointment();
        if (TableHasData("VOUCHER")) MigrateVoucher();
        if (TableHasData("EIA")) MigrateEIA();
        if (TableHasData("CHARGE_ID")) MigrateLookup("CHARGE_ID", "Lookups");
        if (TableHasData("DENIAL_CODE")) MigrateLookup("DENIAL_CODE", "DenialCodes");
        if (TableHasData("REMOVAL_CODE")) MigrateLookup("REMOVAL_CODE", "RemovalCodes");
        if (TableHasData("JURISDICTION")) MigrateLookup("JURISDICTION", "Jurisdictions");
        if (TableHasData("JUDGE")) MigrateLookup("JUDGE", "Judges");
        if (TableHasData("INCOME_SOURCE")) MigrateLookup("INCOME_SOURCE", "IncomeSources");
        if (TableHasData("TYPE")) MigrateLookup("TYPE", "Types");

        CloseDao();
    }

    /// <summary>Check if a table is present and has records via DAO recordset.</summary>
    private bool TableHasData(string tableName)
    {
        try
        {
            if (_daoDb == null) return false;
            var rs = _daoDb.OpenRecordset($"SELECT TOP 1 * FROM [{tableName}]");
            bool hasData = !rs.EOF;
            rs.Close();
            return hasData;
        }
        catch
        {
            return false;
        }
    }

    private void CloseDao()
    {
        try { _daoDb?.Close(); } catch { }
        try { Marshal.ReleaseComObject(_daoDb); } catch { }
        try { Marshal.ReleaseComObject(_daoEngine); } catch { }
        _daoDb = null;
        _daoEngine = null;
    }

    private void CreateSqliteDatabase()
    {
        OnProgress?.Invoke("Creating SQLite database...");
        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (File.Exists(_sqlitePath))
            File.Delete(_sqlitePath);

        using var db = new PdTrackerDbContext(MakeOpts().Options);
        db.Database.EnsureCreated();
        OnProgress?.Invoke("SQLite database created.");
    }

    private DbContextOptionsBuilder<PdTrackerDbContext> MakeOpts()
    {
        var optsBuilder = new DbContextOptionsBuilder<PdTrackerDbContext>();
        optsBuilder.UseSqlite($"Data Source={_sqlitePath}");
        return optsBuilder;
    }

    // ─── Migration helpers ────────────────────────────────────────────────────

    private T GetFieldValue<T>(Recordset rs, string fieldName)
    {
        try
        {
            var fld = rs.Fields[fieldName];
            if (fld.Value == null || fld.Value == DBNull.Value)
                return default!;
            return (T)fld.Value;
        }
        catch
        {
            return default!;
        }
    }

    private string? GetString(Recordset rs, string fieldName, int maxLen = 255)
    {
        var val = GetFieldValue<object?>(rs, fieldName);
        if (val == null) return null;
        var s = val.ToString() ?? "";
        return s.Length > maxLen ? s[..maxLen] : s;
    }

    private int GetInt32(Recordset rs, string fieldName)
    {
        var val = GetFieldValue<object?>(rs, fieldName);
        return val switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            decimal dec => (int)dec,
            _ => 0
        };
    }

    private int? GetInt32Nullable(Recordset rs, string fieldName)
    {
        var val = GetFieldValue<object?>(rs, fieldName);
        if (val == null || val == DBNull.Value) return null;
        return val switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            decimal dec => (int)dec,
            _ => null
        };
    }

    private bool GetBool(Recordset rs, string fieldName)
    {
        var val = GetFieldValue<object?>(rs, fieldName);
        return val switch
        {
            bool b => b,
            int i => i != 0,
            _ => false
        };
    }

    private DateTime? GetDateTime(Recordset rs, string fieldName)
    {
        var val = GetFieldValue<object?>(rs, fieldName);
        if (val == null) return null;
        if (val is DateTime dt) return dt;
        if (DateTime.TryParse(val.ToString(), out var parsed)) return parsed;
        return null;
    }

    private double? GetDoubleNullable(Recordset rs, string fieldName)
    {
        var val = GetFieldValue<object?>(rs, fieldName);
        if (val == null || val == DBNull.Value) return null;
        if (val is double d) return d;
        if (val is decimal dec) return (double)dec;
        if (val is int i) return i;
        return null;
    }

    private static Core.Entities.Type? ParseTypeEnum(string? val)
    {
        if (string.IsNullOrEmpty(val)) return null;
        return val.ToUpperInvariant() switch
        {
            "F" => Core.Entities.Type.F,
            "M" => Core.Entities.Type.M,
            "O" => Core.Entities.Type.O,
            _ => null
        };
    }

    // ─── Table migrators ─────────────────────────────────────────────────────

    private void MigrateDefendant()
    {
        OnProgress?.Invoke("Migrating DEFENDANT...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM DEFENDANT");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new Defendant
                {
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    ApplicationNumber = GetInt32(rs, "ApplicationNumber"),
                    SOID = GetString(rs, "SOID", 20),
                    FirstName = GetString(rs, "FirstName", 50) ?? "",
                    MiddleName = GetString(rs, "MiddleName", 50),
                    LastName = GetString(rs, "LastName", 50) ?? "",
                    DOB = GetDateTime(rs, "DOB"),
                    Education = GetString(rs, "Education", 50),
                    Race = GetString(rs, "Race", 20),
                    Sex = GetString(rs, "Sex", 10),
                    Reference1 = GetString(rs, "Reference1", 100),
                    Reference2 = GetString(rs, "Reference2", 100),
                    DefPhoto = GetString(rs, "defphoto"),
                    DefSignature = GetString(rs, "defsignature"),
                    Dependants = GetInt32Nullable(rs, "dependants"),
                    DepDescription = GetString(rs, "dep_description"),
                    DepOther = GetString(rs, "dep_other"),
                    DateAdded = GetDateTime(rs, "DateAdded") ?? DateTime.Now,
                };
                db.Defendants.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} DEFENDANT records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR DEFENDANT: {ex.Message}");
        }
    }

    private void MigrateAttorneyList()
    {
        OnProgress?.Invoke("Migrating ATTORNEY_LIST...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM ATTORNEY_LIST");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new AttorneyList
                {
                    AttyCode = GetString(rs, "AttyCode", 10) ?? "",
                    FirstName = GetString(rs, "FirstName", 50) ?? "",
                    MiddleName = GetString(rs, "MiddleName", 50),
                    LastName = GetString(rs, "LastName", 50) ?? "",
                    Street = GetString(rs, "Street", 100),
                    Suite = GetString(rs, "Suite", 20),
                    City = GetString(rs, "City", 50),
                    State = GetString(rs, "State", 2),
                    Zip = GetString(rs, "Zip", 20),
                    Phone1 = GetString(rs, "Phone1", 20),
                    Phone2 = GetString(rs, "Phone2", 20),
                    Fax = GetString(rs, "Fax", 20),
                    Email = GetString(rs, "Email", 100),
                    BarNum = GetString(rs, "BarNum", 20),
                    Status = ParseAttorneyStatus(GetString(rs, "Status")),
                };
                db.AttorneyLists.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} ATTORNEY_LIST records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR ATTORNEY_LIST: {ex.Message}");
        }
    }

    private static AttorneyStatus ParseAttorneyStatus(string? val)
        => val?.ToUpperInvariant() switch
        {
            "A" => AttorneyStatus.A,
            "I" => AttorneyStatus.I,
            "R" => AttorneyStatus.R,
            _ => AttorneyStatus.A
        };

    private void MigrateQualify()
    {
        OnProgress?.Invoke("Migrating QUALIFY...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM QUALIFY");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new Qualify
                {
                    ApplicationNumber = GetInt32(rs, "ApplicationNumber"),
                    Date = GetDateTime(rs, "Date"),
                    NoAction = GetBool(rs, "NoAction"),
                    CourtInformation = GetString(rs, "CourtInfo"),
                    Military = GetBool(rs, "Military"),
                    Comment = GetString(rs, "Comment"),
                    DefendantId = GetString(rs, "DefendantID", 9),
                };
                db.Qualifies.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} QUALIFY records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR QUALIFY: {ex.Message}");
        }
    }

    private void MigrateDefAddress()
    {
        OnProgress?.Invoke("Migrating DEF_ADDRESS...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM DEF_ADDRESS");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new DefAddress
                {
                    DefAddressCounter = GetInt32(rs, "DefAddressCounter"),
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    AddressFlag = ParseAddressFlag(GetString(rs, "AddressFlag"))[0],
                    Street = GetString(rs, "Street", 100),
                    City = GetString(rs, "City", 50),
                    State = GetString(rs, "State", 2),
                    Zip = GetString(rs, "Zip", 20),
                    Status = GetString(rs, "Status", 20),
                };
                db.DefAddresses.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} DEF_ADDRESS records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR DEF_ADDRESS: {ex.Message}");
        }
    }

    private AddressFlag[] ParseAddressFlag(string? val)
    {
        if (string.IsNullOrEmpty(val)) return [];
        return val.ToUpperInvariant().Select(c => c switch
        {
            'M' => AddressFlag.M,
            'P' => AddressFlag.P,
            'B' => AddressFlag.B,
            'R' => AddressFlag.R,
            _ => AddressFlag.M
        }).ToArray();
    }

    private void MigrateDefPhone()
    {
        OnProgress?.Invoke("Migrating DEF_PHONE...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM DEF_PHONE");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new DefPhone
                {
                    DefPhoneCounter = GetInt32(rs, "DefPhoneCounter"),
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    PhoneFlag = GetString(rs, "PhoneFlag", 10) ?? "",
                    PhoneNumber = GetString(rs, "PhoneNumber", 20),
                    Description = GetString(rs, "Description", 50),
                };
                db.DefPhones.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} DEF_PHONE records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR DEF_PHONE: {ex.Message}");
        }
    }

    private void MigrateDefSpouse()
    {
        OnProgress?.Invoke("Migrating DEF_SPOUSE...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM DEF_SPOUSE");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new DefSpouse
                {
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    FirstName = GetString(rs, "FirstName", 50),
                    MiddleName = GetString(rs, "MiddleName", 50),
                    LastName = GetString(rs, "LastName", 50),
                    Employed = GetBool(rs, "Employed"),
                    SpouseCounter = GetInt32Nullable(rs, "SpouseCounter"),
                };
                db.DefSpouses.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} DEF_SPOUSE records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR DEF_SPOUSE: {ex.Message}");
        }
    }

    private void MigrateDefAlias()
    {
        OnProgress?.Invoke("Migrating DEF_ALIAS...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM DEF_ALIAS");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new DefAlias
                {
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    FirstName = GetString(rs, "FirstName", 50),
                    MiddleName = GetString(rs, "MiddleName", 50),
                    LastName = GetString(rs, "LastName", 50),
                };
                db.DefAliases.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} DEF_ALIAS records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR DEF_ALIAS: {ex.Message}");
        }
    }

    private void MigrateDependent()
    {
        OnProgress?.Invoke("Migrating DEPENDENT...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM DEPENDENT");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new Dependent
                {
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    DependentCounter = GetInt32Nullable(rs, "DependentCounter") ?? 0,
                    FirstName = GetString(rs, "FirstName", 50),
                    LastName = GetString(rs, "LastName", 50),
                    Relation = GetString(rs, "Relation", 20),
                    DOB = GetDateTime(rs, "DOB"),
                    Education = GetString(rs, "Education", 50),
                };
                db.Dependents.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} DEPENDENT records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR DEPENDENT: {ex.Message}");
        }
    }

    private void MigrateFinEmployer()
    {
        OnProgress?.Invoke("Migrating FIN_EMPLOYER...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM FIN_EMPLOYER");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new FinEmployer
                {
                    EmployerCounter = GetInt32(rs, "EmployerCounter"),
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    EmployerName = GetString(rs, "EmployerName", 100),
                    EmployerAddress = GetString(rs, "EmployerAddress", 150),
                    City = GetString(rs, "City", 50),
                    State = GetString(rs, "State", 2),
                    Zip = GetString(rs, "Zip", 20),
                    HourlyRate = GetDoubleNullable(rs, "HourlyRate"),
                    HoursPerWeek = GetDoubleNullable(rs, "HoursPerWeek"),
                    PayFrequency = GetString(rs, "PayFrequency", 20),
                    StartDate = GetDateTime(rs, "StartDate"),
                    Supervisor = GetString(rs, "Supervisor", 100),
                    SupervisorPhone = GetString(rs, "SupervisorPhone", 20),
                    Occupation = GetString(rs, "Occupation", 50),
                };
                db.FinEmployers.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FIN_EMPLOYER records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR FIN_EMPLOYER: {ex.Message}");
        }
    }

    private void MigrateFinSpEmployer()
    {
        OnProgress?.Invoke("Migrating FIN_SPEMPLOYER...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM FIN_SPEMPLOYER");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new FinSpEmployer
                {
                    SpEmployerCounter = GetInt32(rs, "SpEmployerCounter"),
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    EmployerName = GetString(rs, "EmployerName", 100),
                    EmployerAddress = GetString(rs, "EmployerAddress", 150),
                    City = GetString(rs, "City", 50),
                    State = GetString(rs, "State", 2),
                    Zip = GetString(rs, "Zip", 20),
                    HourlyRate = GetDoubleNullable(rs, "HourlyRate"),
                    HoursPerWeek = GetDoubleNullable(rs, "HoursPerWeek"),
                    PayFrequency = GetString(rs, "PayFrequency", 20),
                    StartDate = GetDateTime(rs, "StartDate"),
                    Supervisor = GetString(rs, "Supervisor", 100),
                    SupervisorPhone = GetString(rs, "SupervisorPhone", 20),
                    Occupation = GetString(rs, "Occupation", 50),
                };
                db.FinSpEmployers.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FIN_SPEMPLOYER records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR FIN_SPEMPLOYER: {ex.Message}");
        }
    }

    private void MigrateFinUnemployed()
    {
        OnProgress?.Invoke("Migrating FIN_UNEMPLOYED...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM FIN_UNEMPLOYED");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new FinUnemployed
                {
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    IncomeSource = ParseIncomeSource(GetString(rs, "IncomeSource")),
                };
                db.FinUnemployeds.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FIN_UNEMPLOYED records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR FIN_UNEMPLOYED: {ex.Message}");
        }
    }

    private void MigrateFinSpUnemploy()
    {
        OnProgress?.Invoke("Migrating FIN_SPUNEMPLOY...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM FIN_SPUNEMPLOY");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new FinSpUnemploy
                {
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    IncomeSource = ParseIncomeSource(GetString(rs, "IncomeSource")),
                };
                db.FinSpUnemploys.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FIN_SPUNEMPLOY records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR FIN_SPUNEMPLOY: {ex.Message}");
        }
    }

    private static IncomeSourceType ParseIncomeSource(string? val)
        => val?.ToUpperInvariant() switch
        {
            "A" => IncomeSourceType.A,
            "W" => IncomeSourceType.W,
            "AF" => IncomeSourceType.AF,
            "SS" => IncomeSourceType.SS,
            "RET" => IncomeSourceType.Ret,
            "OTH" => IncomeSourceType.Oth,
            _ => IncomeSourceType.A
        };

    private void MigrateFinAuto()
    {
        OnProgress?.Invoke("Migrating FINANCE_AUTO...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM FINANCE_AUTO");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new FinAuto
                {
                    AutoCounter = GetInt32(rs, "AutoCounter"),
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    CarYear = GetString(rs, "CarYear", 4),
                    CarMake = GetString(rs, "CarMake", 50),
                    CarModel = GetString(rs, "CarModel", 50),
                    CarVin = GetString(rs, "CarVin", 20),
                    EstimatedValue = GetDoubleNullable(rs, "EstimatedValue"),
                    AmountOwed = GetDoubleNullable(rs, "AmountOwed"),
                };
                db.FinAutos.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FINANCE_AUTO records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR FINANCE_AUTO: {ex.Message}");
        }
    }

    private void MigrateFinBank()
    {
        OnProgress?.Invoke("Migrating FINANCE_BANK...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM FINANCE_BANK");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new FinBank
                {
                    BankCounter = GetInt32(rs, "BankCounter"),
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    BankName = GetString(rs, "BankName", 50),
                    AccountNumber = GetString(rs, "AccountNumber", 20),
                    EstimatedValue = GetDoubleNullable(rs, "EstimatedValue"),
                    AmountOwed = GetDoubleNullable(rs, "AmountOwed"),
                };
                db.FinBanks.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FINANCE_BANK records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR FINANCE_BANK: {ex.Message}");
        }
    }

    private void MigrateFinHome()
    {
        OnProgress?.Invoke("Migrating FINANCE_HOME...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM FINANCE_HOME");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new FinHome
                {
                    HomeCounter = GetInt32(rs, "HomeCounter"),
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    Address = GetString(rs, "Address", 150),
                    City = GetString(rs, "City", 50),
                    State = GetString(rs, "State", 2),
                    Zip = GetString(rs, "Zip", 20),
                    EstimatedValue = GetDoubleNullable(rs, "EstimatedValue"),
                    AmountOwed = GetDoubleNullable(rs, "AmountOwed"),
                    MortgageCompany = GetString(rs, "MortgageCompany", 100),
                    MortgagePayment = GetDoubleNullable(rs, "MortgagePayment"),
                };
                db.FinHomes.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FINANCE_HOME records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR FINANCE_HOME: {ex.Message}");
        }
    }

    private void MigrateFinRent()
    {
        OnProgress?.Invoke("Migrating FINANCE_RENT...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM FINANCE_RENT");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new FinRent
                {
                    RentCounter = GetInt32(rs, "RentCounter"),
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    LandlordName = GetString(rs, "LandlordName", 100),
                    MonthlyRent = GetDoubleNullable(rs, "MonthlyRent"),
                    Address = GetString(rs, "Address", 150),
                    City = GetString(rs, "City", 50),
                    State = GetString(rs, "State", 2),
                    Zip = GetString(rs, "Zip", 20),
                    Phone = GetString(rs, "Phone", 20),
                };
                db.FinRents.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FINANCE_RENT records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR FINANCE_RENT: {ex.Message}");
        }
    }

    private void MigrateFinOther()
    {
        OnProgress?.Invoke("Migrating FINANCE_OTHER...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM FINANCE_OTHER");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new FinOther
                {
                    OtherCounter = GetInt32(rs, "OtherCounter"),
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    Description = GetString(rs, "Description", 100),
                    EstimatedValue = GetDoubleNullable(rs, "EstimatedValue"),
                    AmountOwed = GetDoubleNullable(rs, "AmountOwed"),
                };
                db.FinOthers.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FINANCE_OTHER records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR FINANCE_OTHER: {ex.Message}");
        }
    }

    private void MigrateCharge()
    {
        OnProgress?.Invoke("Migrating CHARGE...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM CHARGE");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new Charge
                {
                    ChargeCounter = GetInt32(rs, "ChargeCounter"),
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    ApplicationNumber = GetInt32(rs, "ApplicationNumber"),
                    CaseNumber = GetString(rs, "CaseNumber", 20),
                    ChargeDate = GetDateTime(rs, "ChargeDate"),
                    ChargeType = GetString(rs, "ChargeType", 20),
                    Description = GetString(rs, "Description", 200),
                    ChargeNumber = GetInt32Nullable(rs, "ChargeNumber"),
                };
                db.Charges.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} CHARGE records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR CHARGE: {ex.Message}");
        }
    }

    private void MigrateWarrant()
    {
        OnProgress?.Invoke("Migrating WARRANT...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM WARRANT");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new Warrant
                {
                    WarrantCounter = GetInt32(rs, "WarrantCounter"),
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    ApplicationNumber = GetInt32(rs, "ApplicationNumber"),
                    WarrantNumber = GetString(rs, "WarrantNumber", 20),
                    CaseNumber = GetString(rs, "CaseNumber", 20),
                    JurisdictionCode = GetString(rs, "JurisdictionCode", 20),
                    BondAmt = GetDoubleNullable(rs, "BondAmt"),
                    Jail = GetBool(rs, "Jail"),
                };
                db.Warrants.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} WARRANT records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR WARRANT: {ex.Message}");
        }
    }

    private void MigrateAppointment()
    {
        OnProgress?.Invoke("Migrating APPOINTMENT...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM APPOINTMENT");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new Appointment
                {
                    ApptCounter = GetInt32(rs, "ApptCounter"),
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    ApplicationNumber = GetInt32(rs, "ApplicationNumber"),
                    AttorneyId = GetString(rs, "AttorneyID", 10),
                    Date = GetDateTime(rs, "Date"),
                    Action = GetString(rs, "Action", 20),
                    DateSigned = GetDateTime(rs, "DateSigned"),
                    VoucherNumber = GetString(rs, "VoucherNumber", 20),
                    GAL = GetBool(rs, "GAL"),
                };
                db.Appointments.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} APPOINTMENT records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR APPOINTMENT: {ex.Message}");
        }
    }

    private void MigrateVoucher()
    {
        OnProgress?.Invoke("Migrating VOUCHER...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM VOUCHER");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new Voucher
                {
                    VoucherCounter = GetInt32(rs, "VoucherCounter"),
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    ApplicationNumber = GetInt32(rs, "ApplicationNumber"),
                    AttorneyId = GetString(rs, "AttorneyID", 10),
                    Date = GetDateTime(rs, "Date"),
                    Action = GetString(rs, "Action", 20),
                    Outcome = ParseOutcome(GetString(rs, "Outcome")),
                };
                db.Vouchers.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} VOUCHER records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR VOUCHER: {ex.Message}");
        }
    }

    private static Core.Entities.VoucherOutcome ParseOutcome(string? val)
        => val?.ToUpperInvariant() switch
        {
            "G" => Core.Entities.VoucherOutcome.G,
            "N" => Core.Entities.VoucherOutcome.N,
            "W" => Core.Entities.VoucherOutcome.W,
            "D" => Core.Entities.VoucherOutcome.D,
            _ => Core.Entities.VoucherOutcome.G
        };

    private void MigrateEIA()
    {
        OnProgress?.Invoke("Migrating EIA...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset("SELECT * FROM EIA");
            int count = 0;
            while (!rs.EOF)
            {
                var e = new EIA
                {
                    DefendantId = GetString(rs, "DefendantID", 9) ?? "",
                    ApplicationNumber = GetInt32(rs, "ApplicationNumber"),
                    Type = ParseTypeEnum(GetString(rs, "Type")),
                    ApplicationType = ParseTypeEnum(GetString(rs, "ApplicationType")),
                };
                db.EIAs.Add(e);
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} EIA records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR EIA: {ex.Message}");
        }
    }

    private void MigrateEIAWithDefendantIds()
    {
        // If EIA was migrated without DefendantIds, backfill them from APPOINTMENT
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var eias = db.EIAs.Where(e => string.IsNullOrEmpty(e.DefendantId)).ToList();
            if (eias.Count == 0) return;

            foreach (var eia in eias)
            {
                var defId = db.Appointments
                    .Where(a => a.ApplicationNumber == eia.ApplicationNumber)
                    .Select(a => a.DefendantId)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(defId))
                    eia.DefendantId = defId;
            }
            db.SaveChanges();
            OnProgress?.Invoke($"  Backfilled DefendantId for {eias.Count} EIA records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  WARNING EIA backfill: {ex.Message}");
        }
    }

    private void MigrateLookup(string sourceTable, string entityName)
    {
        OnProgress?.Invoke($"Migrating {sourceTable}...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts().Options);
            var rs = _daoDb!.OpenRecordset($"SELECT * FROM [{sourceTable}]");
            int count = 0;
            while (!rs.EOF)
            {
                var id = GetString(rs, rs.Fields[0].Name, 10);
                var desc = GetString(rs, rs.Fields[1].Name, 200);
                if (!string.IsNullOrEmpty(id))
                {
                    db.AttorneyListLookups.Add(new AttorneyListLookups
                    {
                        TableName = sourceTable,
                        Code = id,
                        Description = desc ?? id,
                    });
                    count++;
                }
                rs.MoveNext();
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} {entityName} records.");
        }
        catch (Exception ex)
        {
            OnProgress?.Invoke($"  ERROR {sourceTable}: {ex.Message}");
        }
    }
}
