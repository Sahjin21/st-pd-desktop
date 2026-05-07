using Microsoft.Office.Interop.Access.Dao;
using PdTracker.Core.Entities;
using PdTracker.Data.DbContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace PdTracker.Desktop.Services;

/// <summary>
/// Migrates a legacy Access .accdb to SQLite using DAO to distinguish
/// local tables (read directly) from linked tables (skip).
/// </summary>
public class AccdbToSqliteMigrationService
{
    private DBEngine? _daoEngine;
    private Database? _daoDb;

    // DAO table attribute flags
    private const int dbAttachedTable = unchecked((int)0x80000002); // linked table
    private const int dbHiddenObject = 0x00000001;   // hidden system object

    public event Action<string>? OnProgress;

    public AccdbToSqliteMigrationService(string accdbPath, string sqlitePath)
    {
        AccdbPath = accdbPath;
        SqlitePath = sqlitePath;
    }

    public string AccdbPath { get; }
    public string SqlitePath { get; }

    // ─── Entry point ─────────────────────────────────────────────────────

    public void Run()
    {
        OnProgress?.Invoke($"Opening {AccdbPath}...");
        InitDao();
        EnsureEmptySqlite();
        MigrateAll();
        CleanupDao();
        OnProgress?.Invoke("Migration complete.");
    }

    // ─── DAO setup/teardown ───────────────────────────────────────────────

    private void InitDao()
    {
        _daoEngine = new DBEngine();
        _daoDb = _daoEngine.OpenDatabase(AccdbPath, false, false, "");
    }

    private void CleanupDao()
    {
        if (_daoDb != null) { Marshal.ReleaseComObject(_daoDb); _daoDb = null; }
        if (_daoEngine != null) { Marshal.ReleaseComObject(_daoEngine); _daoEngine = null; }
    }

    private static DbContextOptions<PdTrackerDbContext> MakeOpts(string path)
    {
        return new DbContextOptionsBuilder<PdTrackerDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
    }

    // ─── SQLite setup ────────────────────────────────────────────────────

    private void EnsureEmptySqlite()
    {
        if (File.Exists(SqlitePath)) File.Delete(SqlitePath);
        using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
        db.Database.EnsureCreated();
        OnProgress?.Invoke($"Created fresh SQLite: {SqlitePath}");
    }

    // ─── Migrate all local tables ────────────────────────────────────────

    private void MigrateAll()
    {
        var tableDefs = _daoDb!.TableDefs;
        for (int i = 0; i < tableDefs.Count; i++)
        {
            var td = tableDefs[i];
            bool isLinked = (td.Attributes & dbAttachedTable) != 0;
            bool isHidden = (td.Attributes & dbHiddenObject) != 0;
            string name = td.Name;
            // Skip hidden system tables
            if (isHidden || name.StartsWith("MSys")) continue;
            OnProgress?.Invoke($"  {(isLinked ? "LINKED" : "LOCAL")}: {name}");
        }

        // Migrate in dependency order
        // 1. Lookups first (no FK dependencies)
        if (TableHasData("CHARGE_ID")) MigrateChargeId();
        if (TableHasData("DENIAL_CODE")) MigrateDenialCode();
        if (TableHasData("REMOVAL_CODE")) MigrateRemovalCode();
        if (TableHasData("JURISDICTION")) MigrateJurisdiction();
        if (TableHasData("JUDGE")) MigrateJudge();
        if (TableHasData("INCOME_SOURCE")) MigrateIncomeSource();
        if (TableHasData("ATTORNEY_LIST")) MigrateAttorneyList();

        // 2. Defendant (no FK dependencies)
        if (TableHasData("DEFENDANT")) MigrateDefendant();

        // 3. Defendant satellites (depend on Defendant)
        if (TableHasData("DEF_ADDRESS")) MigrateDefAddress();
        if (TableHasData("DEF_PHONE")) MigrateDefPhone();
        if (TableHasData("DEF_SPOUSE")) MigrateDefSpouse();
        if (TableHasData("DEF_ALIAS")) MigrateDefAlias();
        if (TableHasData("DEPENDENT")) MigrateDependent();

        // 4. Financial (depend on Defendant)
        if (TableHasData("FIN_EMPLOYER")) MigrateFinEmployer();
        if (TableHasData("FIN_SPEMPLOYER")) MigrateFinSpEmployer();
        if (TableHasData("FIN_UNEMPLOYED")) MigrateFinUnemployed();
        if (TableHasData("FIN_SPUNEMPLOY")) MigrateFinSpUnemploy();
        if (TableHasData("FINANCE_AUTO")) MigrateFinAuto();
        if (TableHasData("FINANCE_BANK")) MigrateFinBank();
        if (TableHasData("FINANCE_HOME")) MigrateFinHome();
        if (TableHasData("FINANCE_RENT")) MigrateFinRent();
        if (TableHasData("FINANCE_OTHER")) MigrateFinOther();

        // 5. Qualify (depends on Defendant)
        if (TableHasData("QUALIFY")) MigrateQualify();

        // 6. Case management (depend on Defendant via ApplicationNumber)
        if (TableHasData("CHARGE")) MigrateCharge();
        if (TableHasData("WARRANT")) MigrateWarrant();
        if (TableHasData("APPOINTMENT")) MigrateAppointment();
        if (TableHasData("VOUCHER")) MigrateVoucher();
        if (TableHasData("EIA")) MigrateEIA();

        // 7. Backfill EIA DefendantIds
        MigrateEIAWithDefendantIds();
    }

    private bool TableHasData(string tableName)
    {
        try
        {
            var rs = _daoDb!.OpenRecordset($"SELECT TOP 1 * FROM [{tableName}]");
            bool hasData = !rs.EOF;
            rs.Close();
            return hasData;
        }
        catch { return false; }
    }

    // ─── DAO recordset helpers ──────────────────────────────────────────

    private Recordset OpenRecordset(string sql)
        => _daoDb!.OpenRecordset(sql, RecordsetTypeEnum.dbOpenDynaset);

    private static string? GetString(Recordset rs, string field, int maxLen = 0)
    {
        try
        {
            var val = rs.Fields[field].Value;
            if (val == null || val == DBNull.Value) return null;
            var str = val.ToString()!.Trim();
            if (maxLen > 0 && str.Length > maxLen) str = str[..maxLen];
            return str;
        }
        catch { return null; }
    }

    private static int GetInt32(Recordset rs, string field)
    {
        var val = GetDoubleNullable(rs, field);
        return val.HasValue ? (int)Math.Round(val.Value) : 0;
    }

    private static int? GetInt32Nullable(Recordset rs, string field)
    {
        var val = GetDoubleNullable(rs, field);
        return val.HasValue ? (int?)Math.Round(val.Value) : null;
    }

    private static double? GetDoubleNullable(Recordset rs, string fieldName)
    {
        try
        {
            var val = rs.Fields[fieldName].Value;
            if (val == null || val == DBNull.Value) return null;
            if (val is double d) return d;
            if (val is decimal dec) return (double)dec;
            if (val is int i) return i;
            if (double.TryParse(val.ToString(), out var parsed)) return parsed;
            return null;
        }
        catch { return null; }
    }

    private static bool? GetBool(Recordset rs, string field)
    {
        try
        {
            var val = rs.Fields[field].Value;
            if (val == null || val == DBNull.Value) return null;
            if (val is bool b) return b;
            if (val is int i) return i != 0;
            if (val is string s) return new[] { "Y", "T", "1", "YES", "TRUE" }.Contains(s, StringComparer.OrdinalIgnoreCase);
            return null;
        }
        catch { return null; }
    }

    private static DateTime? GetDateTime(Recordset rs, string field)
    {
        try
        {
            var val = rs.Fields[field].Value;
            if (val == null || val == DBNull.Value) return null;
            if (val is DateTime dt) return dt;
            if (DateTime.TryParse(val.ToString(), out var parsed)) return parsed;
            return null;
        }
        catch { return null; }
    }

    private static string? GetFirstString(Recordset rs, params (string field, int maxLen)[] options)
    {
        foreach (var (field, maxLen) in options)
        {
            var value = GetString(rs, field, maxLen);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    // ─── Enum parsers ────────────────────────────────────────────────────

    private static Core.Entities.JurisdictionCode? ParseJurisdictionCode(string? val)
        => val?.ToUpperInvariant() switch
        {
            "STE" => Core.Entities.JurisdictionCode.STE,
            "SUP" => Core.Entities.JurisdictionCode.SUP,
            "MAG" => Core.Entities.JurisdictionCode.MAG,
            "JUV" => Core.Entities.JurisdictionCode.JUV,
            _ => null
        };

    private static Core.Entities.VoucherOutcome ParseOutcome(string? val)
        => val?.ToUpperInvariant() switch
        {
            "G" => Core.Entities.VoucherOutcome.G,
            "N" => Core.Entities.VoucherOutcome.N,
            "W" => Core.Entities.VoucherOutcome.W,
            "D" => Core.Entities.VoucherOutcome.D,
            "O" => Core.Entities.VoucherOutcome.O,
            _ => Core.Entities.VoucherOutcome.O
        };

    private static IncomeSourceType ParseIncomeSource(string? val)
        => val?.ToUpperInvariant() switch
        {
            "A" or "E" => IncomeSourceType.Employment,
            "W" or "U" => IncomeSourceType.Unemployment,
            "SE" => IncomeSourceType.SpouseEmployment,
            "SU" => IncomeSourceType.SpouseUnemployment,
            _ => IncomeSourceType.Other
        };

    // ─── Lookup table migrators ─────────────────────────────────────────

    private void MigrateChargeId()
    {
        OnProgress?.Invoke("Migrating CHARGE_ID...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var rs = OpenRecordset("SELECT * FROM CHARGE_ID");
            int count = 0;
            while (!rs.EOF)
            {
                db.ChargeIds.Add(new ChargeId
                {
                    ChargeIdCode = GetString(rs, "ChargeID", 10) ?? "",
                    Description = GetString(rs, "Description", 255),
                    ChargeSelect = GetString(rs, "ChargeSelect"),
                });
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} CHARGE_ID records.");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR CHARGE_ID: {ex.Message}"); }
    }

    private void MigrateDenialCode()
    {
        OnProgress?.Invoke("Migrating DENIAL_CODE...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var rs = OpenRecordset("SELECT * FROM DENIAL_CODE");
            int count = 0;
            while (!rs.EOF)
            {
                db.DenialCodes.Add(new DenialCode
                {
                    DenyCode = GetString(rs, "DenyCode", 10) ?? "",
                    Description = GetString(rs, "Description", 255),
                    LongText = GetString(rs, "LongText"),
                });
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} DENIAL_CODE records.");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR DENIAL_CODE: {ex.Message}"); }
    }

    private void MigrateRemovalCode()
    {
        OnProgress?.Invoke("Migrating REMOVAL_CODE...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var rs = OpenRecordset("SELECT * FROM REMOVAL_CODE");
            int count = 0;
            while (!rs.EOF)
            {
                db.RemovalCodes.Add(new RemovalCode
                {
                    RemovalCodeValue = GetString(rs, "RemovalCode", 10) ?? "",
                    Description = GetString(rs, "Description", 255),
                    Statement = GetString(rs, "Statement"),
                });
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} REMOVAL_CODE records.");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR REMOVAL_CODE: {ex.Message}"); }
    }

    private void MigrateJurisdiction()
    {
        OnProgress?.Invoke("Migrating JURISDICTION...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var rs = OpenRecordset("SELECT * FROM JURISDICTION");
            int count = 0;
            while (!rs.EOF)
            {
                db.Jurisdictions.Add(new Jurisdiction
                {
                    JurisdictionCode = GetString(rs, "JurisdictionCode", 10) ?? "",
                    Description = GetString(rs, "Description", 255),
                });
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} JURISDICTION records.");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR JURISDICTION: {ex.Message}"); }
    }

    private void MigrateJudge()
    {
        OnProgress?.Invoke("Migrating JUDGE...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var rs = OpenRecordset("SELECT * FROM JUDGE");
            int count = 0;
            while (!rs.EOF)
            {
                db.Judges.Add(new Judge
                {
                    JudgeCode = GetString(rs, "JudgeCode", 10) ?? "",
                    Description = GetString(rs, "Description", 255),
                });
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} JUDGE records.");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR JUDGE: {ex.Message}"); }
    }

    private void MigrateIncomeSource()
    {
        OnProgress?.Invoke("Migrating INCOME_SOURCE...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var rs = OpenRecordset("SELECT * FROM INCOME_SOURCE");
            int count = 0, dupes = 0;
            var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (!rs.EOF)
            {
                var code = GetFirstString(rs, ("IncomeSource", 4), ("IncomeSourceCode", 4)) ?? "";
                if (string.IsNullOrWhiteSpace(code) || !seenCodes.Add(code))
                {
                    dupes++;
                    rs.MoveNext();
                    continue;
                }

                db.IncomeSources.Add(new IncomeSource
                {
                    IncomeSourceCode = code,
                    Description = GetString(rs, "Description", 20),
                });
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} INCOME_SOURCE records. ({dupes} duplicates skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR INCOME_SOURCE: {ex.Message}"); }
    }

    private void MigrateAttorneyList()
    {
        OnProgress?.Invoke("Migrating ATTORNEY_LIST...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var rs = OpenRecordset("SELECT * FROM ATTORNEY_LIST");
            int count = 0;
            while (!rs.EOF)
            {
                db.AttorneyLists.Add(new AttorneyList
                {
                    AttyCode = GetString(rs, "AttyCode", 10) ?? "",
                    FirstName = GetString(rs, "FirstName", 50) ?? "",
                    MiddleName = GetString(rs, "MiddleName", 50),
                    LastName = GetString(rs, "LastName", 50) ?? "",
                    Street = GetString(rs, "Street", 100),
                    Suite = GetString(rs, "Suite", 20),
                    City = GetString(rs, "City", 50),
                    State = GetString(rs, "State", 2),
                    ZipCode = GetString(rs, "ZipCode", 10),
                    Email = GetString(rs, "Email", 100),
                    OfficeNumber = GetString(rs, "OfficeNumber", 20),
                    FaxNumber = GetString(rs, "FaxNumber", 20),
                    HomeNumber = GetString(rs, "HomeNumber", 20),
                    PagerNumber = GetString(rs, "PagerNumber", 20),
                    MobileNumber = GetString(rs, "MobileNumber", 20),
                    OtherNumber = GetString(rs, "OtherNumber", 20),
                    PhoneType = GetString(rs, "PhoneType", 20),
                    Date = GetDateTime(rs, "Date"),
                    Status = ParseAttorneyStatus(GetString(rs, "Status")),
                    DeathPenalty = GetBool(rs, "Deathpenalty") ?? false,
                    Murder = GetBool(rs, "Murder") ?? false,
                    Felony = GetBool(rs, "Felony") ?? true,
                    Misd = GetBool(rs, "Misd") ?? true,
                    Appeal = GetBool(rs, "Appeal") ?? false,
                    Juvenile = GetBool(rs, "Juvenile") ?? false,
                    GAL = GetBool(rs, "GAL") ?? false,
                    VendorNumber = GetString(rs, "VenderNumber", 20),
                    Notes = GetString(rs, "Notes", 255),
                });
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} ATTORNEY_LIST records.");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR ATTORNEY_LIST: {ex.Message}"); }
    }

    private static AttorneyStatus ParseAttorneyStatus(string? val)
        => val?.ToUpperInvariant() switch
        {
            "A" => AttorneyStatus.Active,
            "I" => AttorneyStatus.Inactive,
            "S" or "R" => AttorneyStatus.Suspended,
            _ => AttorneyStatus.Active
        };

    // ─── Core entities ───────────────────────────────────────────────────

    private void MigrateDefendant()
    {
        OnProgress?.Invoke("Migrating DEFENDANT...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var rs = OpenRecordset("SELECT * FROM DEFENDANT");
            int count = 0, dupes = 0, batch = 0;
            var seen = new HashSet<string>();
            while (!rs.EOF)
            {
                var id = GetString(rs, "DefendantID", 9) ?? "";
                if (!seen.Add(id))
                {
                    OnProgress?.Invoke($"  SKIP dupe DEFENDANT: {id}");
                    dupes++;
                    rs.MoveNext();
                    continue;
                }
                db.Defendants.Add(new Defendant
                {
                    DefendantId = id,
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
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200)
                {
                    db.SaveChanges();
                    batch = 0;
                }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} DEFENDANT records. ({dupes} duplicates skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR DEFENDANT: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateQualify()
    {
        OnProgress?.Invoke("Migrating QUALIFY...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var rs = OpenRecordset("SELECT * FROM QUALIFY");
            int count = 0;
            while (!rs.EOF)
            {
                db.Qualifies.Add(new Qualify
                {
                    ApplicationNumber = GetInt32(rs, "ApplicationNumber"),
                    Date = GetDateTime(rs, "Date"),
                    NoAction = GetBool(rs, "NoAction") ?? false,
                    Comment = GetString(rs, "Comment"),
                    CourtInformation = GetString(rs, "CourtInformation"),
                    Military = GetBool(rs, "Military"),
                    EntryDate = GetDateTime(rs, "Entrydate"),
                    DefendantId = GetString(rs, "DefendantID", 9),
                });
                rs.MoveNext();
                count++;
            }
            rs.Close();
            db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} QUALIFY records.");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR QUALIFY: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    // ─── Defendant satellites ───────────────────────────────────────────

    private void MigrateDefAddress()
    {
        OnProgress?.Invoke("Migrating DEF_ADDRESS...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM DEF_ADDRESS");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId))
                {
                    OnProgress?.Invoke($"  SKIP FK-orphan DEF_ADDRESS: DefendantId={defId}");
                    skipped++;
                    rs.MoveNext();
                    continue;
                }
                db.DefAddresses.Add(new DefAddress
                {
                    DefendantId = defId,
                    Street = GetString(rs, "Street", 100),
                    City = GetString(rs, "City", 50),
                    State = GetString(rs, "State", 2),
                    ZipCode = GetString(rs, "ZipCode", 10),
                    AddressFlag = ParseAddressFlag(GetString(rs, "AddressFlag")),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} DEF_ADDRESS records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR DEF_ADDRESS: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private static AddressFlag ParseAddressFlag(string? val)
        => val?.ToUpperInvariant() switch
        {
            "P" or "R" => AddressFlag.Previous,
            _ => AddressFlag.Current
        };

    private void MigrateDefPhone()
    {
        OnProgress?.Invoke("Migrating DEF_PHONE...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM DEF_PHONE");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                db.DefPhones.Add(new DefPhone
                {
                    DefendantId = defId,
                    PhoneNumber = GetString(rs, "PhoneNumber", 20),
                    PhoneType = GetString(rs, "PhoneType", 20),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} DEF_PHONE records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR DEF_PHONE: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateDefSpouse()
    {
        OnProgress?.Invoke("Migrating DEF_SPOUSE...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM DEF_SPOUSE");
            int count = 0, skipped = 0, duplicates = 0, batch = 0;
            var seenDefendantIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                if (!seenDefendantIds.Add(defId))
                {
                    duplicates++;
                    rs.MoveNext();
                    continue;
                }

                db.DefSpouses.Add(new DefSpouse
                {
                    DefendantId = defId,
                    FirstName = GetString(rs, "FirstName", 50),
                    MiddleName = GetString(rs, "MiddleName", 50),
                    LastName = GetString(rs, "LastName", 50),
                    Employed = GetBool(rs, "Employed"),
                    SpouseCounter = GetInt32Nullable(rs, "SpouseCounter"),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} DEF_SPOUSE records. ({duplicates} duplicates, {skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR DEF_SPOUSE: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateDefAlias()
    {
        OnProgress?.Invoke("Migrating DEF_ALIAS...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM DEF_ALIAS");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                db.DefAliases.Add(new DefAlias
                {
                    DefendantId = defId,
                    FirstName = GetString(rs, "FirstName", 50),
                    MiddleName = GetString(rs, "MiddleName", 50),
                    LastName = GetString(rs, "LastName", 50),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} DEF_ALIAS records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR DEF_ALIAS: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateDependent()
    {
        OnProgress?.Invoke("Migrating DEPENDENT...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM DEPENDENT");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                db.Dependents.Add(new Dependent
                {
                    DefendantId = defId,
                    FirstName = GetString(rs, "FirstName", 50),
                    MiddleName = GetString(rs, "MiddleName", 50),
                    LastName = GetString(rs, "LastName", 50),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} DEPENDENT records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR DEPENDENT: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    // ─── Financial entities ──────────────────────────────────────────────

    private void MigrateFinEmployer()
    {
        OnProgress?.Invoke("Migrating FIN_EMPLOYER...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM FIN_EMPLOYER");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                db.FinEmployers.Add(new FinEmployer
                {
                    DefendantId = defId,
                    EmployerCounter = GetInt32Nullable(rs, "EmployerCounter"),
                    EmployerName = GetString(rs, "EmployerName", 100),
                    City = GetString(rs, "City", 50),
                    Phone = GetString(rs, "Phone", 20),
                    PayAmt = (decimal?)GetDoubleNullable(rs, "PayAmt"),
                    PayPeriod = GetString(rs, "PayPeriod", 20),
                    NetOrGross = GetString(rs, "NetOrGross", 10),
                    TimeEmployed = GetString(rs, "TimeEmployed", 50),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FIN_EMPLOYER records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR FIN_EMPLOYER: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateFinSpEmployer()
    {
        OnProgress?.Invoke("Migrating FIN_SPEMPLOYER...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM FIN_SPEMPLOYER");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                db.FinSpEmployers.Add(new FinSpEmployer
                {
                    DefendantId = defId,
                    SpEmployerCounter = GetInt32Nullable(rs, "SpunemployCounter"),
                    EmployerName = GetString(rs, "EmployerName", 100),
                    City = GetString(rs, "City", 50),
                    Phone = GetString(rs, "Phone", 20),
                    PayAmt = (decimal?)GetDoubleNullable(rs, "PayAmt"),
                    PayPeriod = GetString(rs, "PayPeriod", 20),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FIN_SPEMPLOYER records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR FIN_SPEMPLOYER: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateFinUnemployed()
    {
        OnProgress?.Invoke("Migrating FIN_UNEMPLOYED...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM FIN_UNEMPLOYED");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                db.FinUnemployeds.Add(new FinUnemployed
                {
                    DefendantId = defId,
                    UnemployCounter = GetInt32Nullable(rs, "UnemployCounter"),
                    IncomeSource = ParseIncomeSource(GetString(rs, "IncomeSource")),
                    Description = GetString(rs, "Description", 255),
                    TimeUnemployed = GetString(rs, "TimeUnemployed", 50),
                    PayPeriod = GetString(rs, "PayPeriod", 20),
                    PayAmt = (decimal?)GetDoubleNullable(rs, "PayAmt"),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FIN_UNEMPLOYED records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR FIN_UNEMPLOYED: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateFinSpUnemploy()
    {
        OnProgress?.Invoke("Migrating FIN_SPUNEMPLOY...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM FIN_SPUNEMPLOY");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                db.FinSpUnemploys.Add(new FinSpUnemploy
                {
                    DefendantId = defId,
                    SpUnemployCounter = GetInt32Nullable(rs, "SpunemployCounter"),
                    IncomeSource = ParseIncomeSource(GetString(rs, "IncomeSource")),
                    Description = GetString(rs, "Description", 255),
                    TimeUnemployed = GetString(rs, "TimeUnemployed", 50),
                    PayPeriod = GetString(rs, "PayPeriod", 20),
                    PayAmt = (decimal?)GetDoubleNullable(rs, "PayAmt"),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FIN_SPUNEMPLOY records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR FIN_SPUNEMPLOY: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateFinAuto()
    {
        OnProgress?.Invoke("Migrating FINANCE_AUTO...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM FINANCE_AUTO");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                db.FinAutos.Add(new FinAuto
                {
                    DefendantId = defId,
                    AutoCounter = GetInt32Nullable(rs, "AutoCounter"),
                    Model = GetString(rs, "Model", 50),
                    Year = GetString(rs, "Year", 4),
                    Balance = (decimal?)GetDoubleNullable(rs, "Balance"),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FINANCE_AUTO records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR FINANCE_AUTO: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateFinBank()
    {
        OnProgress?.Invoke("Migrating FINANCE_BANK...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM FINANCE_BANK");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                db.FinBanks.Add(new FinBank
                {
                    DefendantId = defId,
                    BankCounter = GetInt32Nullable(rs, "BankCounter"),
                    BankName = GetString(rs, "BankName", 100),
                    AccountType = GetString(rs, "AccountType", 50),
                    Balance = (decimal?)GetDoubleNullable(rs, "Balance"),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FINANCE_BANK records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR FINANCE_BANK: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateFinHome()
    {
        OnProgress?.Invoke("Migrating FINANCE_HOME...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM FINANCE_HOME");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                db.FinHomes.Add(new FinHome
                {
                    DefendantId = defId,
                    HomeCounter = GetInt32Nullable(rs, "HomeCounter"),
                    MortgagePay = (decimal?)GetDoubleNullable(rs, "MortgagePay"),
                    HomeValue = (decimal?)GetDoubleNullable(rs, "HomeValue"),
                    MortgageBalance = (decimal?)GetDoubleNullable(rs, "MortgageBalance"),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FINANCE_HOME records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR FINANCE_HOME: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateFinRent()
    {
        OnProgress?.Invoke("Migrating FINANCE_RENT...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM FINANCE_RENT");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                db.FinRents.Add(new FinRent
                {
                    DefendantId = defId,
                    RentCounter = GetInt32Nullable(rs, "RentCounter"),
                    MonthlyRent = (decimal?)GetDoubleNullable(rs, "MonthlyRent"),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FINANCE_RENT records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR FINANCE_RENT: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateFinOther()
    {
        OnProgress?.Invoke("Migrating FINANCE_OTHER...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM FINANCE_OTHER");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                db.FinOthers.Add(new FinOther
                {
                    DefendantId = defId,
                    OtherCounter = GetInt32Nullable(rs, "OtherCounter"),
                    Type = GetString(rs, "Type", 50),
                    Description = GetString(rs, "Description", 255),
                    MonthlyAmount = (decimal?)GetDoubleNullable(rs, "MonthlyAmount"),
                    TotalAmount = (decimal?)GetDoubleNullable(rs, "TotalAmount"),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} FINANCE_OTHER records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR FINANCE_OTHER: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    // ─── Case management ────────────────────────────────────────────────

    private void MigrateCharge()
    {
        OnProgress?.Invoke("Migrating CHARGE...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingAppNums = db.Defendants.Select(d => d.ApplicationNumber).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM CHARGE");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var appNum = GetInt32(rs, "ApplicationNumber");
                if (!existingAppNums.Contains(appNum)) { skipped++; rs.MoveNext(); continue; }
                db.Charges.Add(new Charge
                {
                    ApplicationNumber = appNum,
                    ChargeNumber = GetInt32Nullable(rs, "ChargeNumber"),
                    ChargeType = GetString(rs, "ChargeType", 50),
                    CaseNumber = GetString(rs, "CaseNumber", 20),
                    ChargeDate = GetDateTime(rs, "ChargeDate"),
                    AddCharge = GetString(rs, "AddCharge", 255),
                    WarrantNumber = GetString(rs, "WarrantNumber", 20),
                    ChargeId = GetString(rs, "ChargeID", 10),
                    Description = GetString(rs, "Description", 255),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} CHARGE records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR CHARGE: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateWarrant()
    {
        OnProgress?.Invoke("Migrating WARRANT...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingAppNums = db.Defendants.Select(d => d.ApplicationNumber).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM WARRANT");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var appNum = GetInt32(rs, "ApplicationNumber");
                if (!existingAppNums.Contains(appNum)) { skipped++; rs.MoveNext(); continue; }
                db.Warrants.Add(new Warrant
                {
                    ApplicationNumber = appNum,
                    WarrantNumber = GetString(rs, "WarrantNumber", 20),
                    CaseNumber = GetString(rs, "CaseNumber", 20),
                    Date = GetDateTime(rs, "Date"),
                    ArrestDate = GetDateTime(rs, "ArrestDate"),
                    JurisdictionCode = ParseJurisdictionCode(GetString(rs, "JurisdictionCode", 20)),
                    BondType = GetString(rs, "BondType", 20),
                    BondAmt = (decimal?)GetDoubleNullable(rs, "BondAmt"),
                    Jail = GetBool(rs, "Jail"),
                    AddOnCase = GetString(rs, "AddOnCase", 255),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} WARRANT records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR WARRANT: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateAppointment()
    {
        OnProgress?.Invoke("Migrating APPOINTMENT...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingAppNums = db.Defendants.Select(d => d.ApplicationNumber).ToHashSet();
            var existingAttyCodes = db.AttorneyLists.Select(a => a.AttyCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rs = OpenRecordset("SELECT * FROM APPOINTMENT");
            int count = 0, skipped = 0, invalidAttorneyRefs = 0;
            while (!rs.EOF)
            {
                var appNum = GetInt32(rs, "ApplicationNumber");
                if (!existingAppNums.Contains(appNum)) { skipped++; rs.MoveNext(); continue; }

                var attyCode = GetString(rs, "AttyCode", 10);
                if (!string.IsNullOrWhiteSpace(attyCode) && !existingAttyCodes.Contains(attyCode))
                {
                    invalidAttorneyRefs++;
                    attyCode = null;
                }

                db.Appointments.Add(new Appointment
                {
                    ApplicationNumber = appNum,
                    AttyCode = attyCode,
                    Date = GetDateTime(rs, "Date"),
                    Action = GetString(rs, "Action", 5),
                    DateSigned = GetDateTime(rs, "DateSigned"),
                    DenyCode = GetString(rs, "DenyCode", 10),
                    RemovalCode = GetString(rs, "RemovalCode", 10),
                    Bonded = GetBool(rs, "Bonded"),
                    GAL = GetBool(rs, "GAL"),
                    VoucherNumber = GetString(rs, "VoucherNumber", 20),
                    VoucherLetter = GetString(rs, "VoucherLetter", 5),
                    ContractCase = GetBool(rs, "ContractCase"),
                    DUICourt = GetBool(rs, "DUICourt"),
                    JuvenileSubstType = GetString(rs, "JuvenileSubstType", 50),
                });
                try
                {
                    db.SaveChanges();
                    count++;
                }
                catch (Exception ex)
                {
                    skipped++;
                    OnProgress?.Invoke($"  SKIP APPOINTMENT App#{appNum}: {ex.InnerException?.Message ?? ex.Message}");
                    db.ChangeTracker.Clear();
                }

                rs.MoveNext();
            }
            rs.Close();
            OnProgress?.Invoke($"  Migrated {count} APPOINTMENT records. ({invalidAttorneyRefs} invalid attorney refs nulled, {skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR APPOINTMENT: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateVoucher()
    {
        OnProgress?.Invoke("Migrating VOUCHER...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingAppNums = db.Defendants.Select(d => d.ApplicationNumber).ToHashSet();
            var existingAttyCodes = db.AttorneyLists.Select(a => a.AttyCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rs = OpenRecordset("SELECT * FROM VOUCHER");
            int count = 0, dupes = 0, skipped = 0, orphanedImported = 0, invalidAttorneyRefs = 0;
            var seenVnums = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (!rs.EOF)
            {
                var vnum = GetString(rs, "VoucherNumber", 20) ?? "";
                if (string.IsNullOrWhiteSpace(vnum))
                {
                    skipped++;
                    rs.MoveNext();
                    continue;
                }

                if (!seenVnums.Add(vnum))
                {
                    OnProgress?.Invoke($"  SKIP dupe VOUCHER: {vnum}");
                    dupes++;
                    rs.MoveNext();
                    continue;
                }

                var appNum = GetInt32Nullable(rs, "ApplicationNumber");
                if (appNum.HasValue && !existingAppNums.Contains(appNum.Value))
                {
                    orphanedImported++;
                    appNum = null;
                }

                var attyCode = GetString(rs, "AttyCode", 10);
                if (!string.IsNullOrWhiteSpace(attyCode) && !existingAttyCodes.Contains(attyCode))
                {
                    invalidAttorneyRefs++;
                    attyCode = null;
                }

                db.Vouchers.Add(new Voucher
                {
                    VoucherNumber = vnum,
                    VoucherLetter = GetString(rs, "VoucherLetter", 5),
                    ApplicationNumber = appNum,
                    AttyCode = attyCode,
                    DateVchrPaid = GetDateTime(rs, "DateVchrPaid"),
                    DateCaseCompleted = GetDateTime(rs, "DateCaseCompleted"),
                    InCourtHours = (decimal?)GetDoubleNullable(rs, "InCourtHours"),
                    OutCourtHours = (decimal?)GetDoubleNullable(rs, "OutCourtHours"),
                    CourtOrderedReimburse = (decimal?)GetDoubleNullable(rs, "CourtOrderedReimburse"),
                    TotalVoucherAmt = (decimal?)GetDoubleNullable(rs, "TotalVoucherAmt"),
                    TotalAmountPaid = (decimal?)GetDoubleNullable(rs, "TotalAmountPaid"),
                    Outcome = ParseOutcome(GetString(rs, "Outcome")),
                    OutcomeOther = GetString(rs, "OutcomeOther", 255),
                });
                try
                {
                    db.SaveChanges();
                    count++;
                }
                catch (Exception ex)
                {
                    skipped++;
                    OnProgress?.Invoke($"  SKIP VOUCHER {vnum}: {ex.InnerException?.Message ?? ex.Message}");
                    db.ChangeTracker.Clear();
                }

                rs.MoveNext();
            }
            rs.Close();
            OnProgress?.Invoke($"  Migrated {count} VOUCHER records. ({dupes} dupes, {orphanedImported} orphaned app# imported without defendant link, {invalidAttorneyRefs} invalid attorney refs nulled, {skipped} skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR VOUCHER: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateEIA()
    {
        OnProgress?.Invoke("Migrating EIA...");
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var existingIds = db.Defendants.Select(d => d.DefendantId).ToHashSet();
            var rs = OpenRecordset("SELECT * FROM EIA");
            int count = 0, skipped = 0, batch = 0;
            while (!rs.EOF)
            {
                var defId = GetString(rs, "DefendantID", 9) ?? "";
                if (!existingIds.Contains(defId)) { skipped++; rs.MoveNext(); continue; }
                db.EIAs.Add(new EIA
                {
                    DefendantId = defId,
                    ApplicationNumber = GetInt32(rs, "ApplicationNumber"),
                    Type = GetString(rs, "Type"),
                    ApplicationType = GetString(rs, "ApplicationType"),
                    Judge = GetString(rs, "Judge", 50),
                    EIAResult = GetString(rs, "EIAResult", 50),
                    Jail = GetString(rs, "jail", 50),
                    Probation = GetString(rs, "probation", 50),
                    Reimbursement = (decimal?)GetDoubleNullable(rs, "reimbursement"),
                    Bond = (decimal?)GetDoubleNullable(rs, "bond"),
                });
                rs.MoveNext();
                count++;
                batch++;
                if (batch >= 200) { db.SaveChanges(); batch = 0; }
            }
            rs.Close();
            if (batch > 0) db.SaveChanges();
            OnProgress?.Invoke($"  Migrated {count} EIA records. ({skipped} orphaned/skipped)");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  ERROR EIA: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }

    private void MigrateEIAWithDefendantIds()
    {
        // Backfill EIA records that have ApplicationNumber but no DefendantId
        // by looking up the DefendantId from the ApplicationNumber
        try
        {
            using var db = new PdTrackerDbContext(MakeOpts(SqlitePath));
            var eias = db.EIAs.Where(e => string.IsNullOrEmpty(e.DefendantId) && e.ApplicationNumber != 0).ToList();
            if (eias.Count == 0) return;

            foreach (var eia in eias)
            {
                var defId = db.Defendants
                    .Where(d => d.ApplicationNumber == eia.ApplicationNumber)
                    .Select(d => d.DefendantId)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(defId))
                    eia.DefendantId = defId;
            }
            db.SaveChanges();
            OnProgress?.Invoke($"  Backfilled DefendantId for {eias.Count} EIA records.");
        }
        catch (Exception ex) { OnProgress?.Invoke($"  WARNING EIA backfill: {ex.Message}\n  INNER: {ex.InnerException?.Message}"); }
    }
}
