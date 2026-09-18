using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using LabReportApp.Models;

namespace LabReportApp.Database
{
    public static class DatabaseHelper
    {
        // The database file lives next to the .exe, so the app is fully portable/offline.
        private static readonly string DbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LabReports.db");
        private static string ConnectionString => $"Data Source={DbFile}";

        public static void InitializeDatabase()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var createPatients = @"
                CREATE TABLE IF NOT EXISTS Patients (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SerialNumber TEXT UNIQUE,
                    Name TEXT NOT NULL,
                    Age INTEGER,
                    Gender TEXT,
                    ReferredBy TEXT,
                    LabName TEXT,
                    PhoneNumber TEXT,
                    ReportHeading TEXT,
                    CreatedAt TEXT,
                    Status TEXT DEFAULT 'Pending',
                    TotalAmount REAL DEFAULT 0,
                    DiscountPercentage REAL DEFAULT 0,
                    NetAmount REAL DEFAULT 0
                );";

            var createResults = @"
                CREATE TABLE IF NOT EXISTS TestResults (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PatientId INTEGER NOT NULL,
                    CategoryHeading TEXT,
                    TestName TEXT,
                    Result TEXT,
                    Unit TEXT,
                    NormalRange TEXT,
                    Price REAL DEFAULT 0,
                    Discount REAL DEFAULT 0,
                    FOREIGN KEY (PatientId) REFERENCES Patients(Id)
                );";

            // Master list of predefined tests maintained by the Lab Attendant.
            // TestName is UNIQUE so we can "upsert" (insert or update) by name.
            var createDefinitions = @"
                CREATE TABLE IF NOT EXISTS TestDefinitions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CategoryHeading TEXT NOT NULL,
                    TestName TEXT NOT NULL UNIQUE,
                    Unit TEXT,
                    NormalRange TEXT,
                    Price REAL DEFAULT 0
                );";

            using (var cmd1 = new SqliteCommand(createPatients, connection)) cmd1.ExecuteNonQuery();
            using (var cmd2 = new SqliteCommand(createResults, connection)) cmd2.ExecuteNonQuery();
            using (var cmd3 = new SqliteCommand(createDefinitions, connection)) cmd3.ExecuteNonQuery();

            // Migration: if you're upgrading from an older version of the app, these columns
            // won't exist yet on an existing database file. Add them automatically if missing.
            EnsureColumnExists(connection, "TestResults", "CategoryHeading", "TEXT");
            EnsureColumnExists(connection, "TestResults", "Price", "REAL");
            EnsureColumnExists(connection, "TestResults", "Discount", "REAL");
            EnsureColumnExists(connection, "TestDefinitions", "Price", "REAL");
            EnsureColumnExists(connection, "Patients", "Status", "TEXT");
            EnsureColumnExists(connection, "Patients", "TotalAmount", "REAL");
            EnsureColumnExists(connection, "Patients", "DiscountPercentage", "REAL");
            EnsureColumnExists(connection, "Patients", "NetAmount", "REAL");

            // Any patient created before this update has no Status - treat those as already
            // Completed (their old-style report was already generated), so they don't
            // suddenly show up as "Pending".
            using var backfillCmd = new SqliteCommand(
                "UPDATE Patients SET Status = 'Completed' WHERE Status IS NULL OR Status = '';", connection);
            backfillCmd.ExecuteNonQuery();
        }

        private static void EnsureColumnExists(SqliteConnection connection, string table, string column, string sqlType)
        {
            bool exists = false;
            using (var cmd = new SqliteCommand($"PRAGMA table_info({table});", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    // Column index 1 in PRAGMA table_info result is the column name.
                    if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (!exists)
            {
                using var alterCmd = new SqliteCommand($"ALTER TABLE {table} ADD COLUMN {column} {sqlType};", connection);
                alterCmd.ExecuteNonQuery();
            }
        }

        // ==================== STAGE 1: BILLING (patient + selected tests, no results yet) ====================

        /// <summary>
        /// Saves the patient and the list of tests they've been billed for (Result is empty at this
        /// point). Saves Patient.Status as "Pending" and Patient.TotalAmount as given.
        /// Returns the generated unique serial number, e.g. LAB-000001.
        /// </summary>
        public static string SavePatientAndResults(Patient patient, List<TestResultItem> results)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                var insertPatient = @"
                    INSERT INTO Patients (Name, Age, Gender, ReferredBy, LabName, PhoneNumber, ReportHeading, CreatedAt, Status, TotalAmount, DiscountPercentage, NetAmount)
                    VALUES (@Name, @Age, @Gender, @ReferredBy, @LabName, @PhoneNumber, @ReportHeading, @CreatedAt, @Status, @TotalAmount, @DiscountPercentage, @NetAmount);
                    SELECT last_insert_rowid();";

                using var cmd = new SqliteCommand(insertPatient, connection, transaction);
                cmd.Parameters.AddWithValue("@Name", patient.Name);
                cmd.Parameters.AddWithValue("@Age", patient.Age);
                cmd.Parameters.AddWithValue("@Gender", patient.Gender);
                cmd.Parameters.AddWithValue("@ReferredBy", patient.ReferredBy);
                cmd.Parameters.AddWithValue("@LabName", patient.LabName);
                cmd.Parameters.AddWithValue("@PhoneNumber", patient.PhoneNumber);
                cmd.Parameters.AddWithValue("@ReportHeading", patient.ReportHeading);
                cmd.Parameters.AddWithValue("@CreatedAt", patient.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@Status", patient.Status);
                cmd.Parameters.AddWithValue("@TotalAmount", patient.TotalAmount);
                cmd.Parameters.AddWithValue("@DiscountPercentage", patient.DiscountPercentage);
                cmd.Parameters.AddWithValue("@NetAmount", patient.NetAmount);

                long newId = (long)(cmd.ExecuteScalar() ?? 0L);

                string serialNumber = $"LAB-{newId:D6}";

                using (var updateCmd = new SqliteCommand(
                    "UPDATE Patients SET SerialNumber = @SerialNumber WHERE Id = @Id", connection, transaction))
                {
                    updateCmd.Parameters.AddWithValue("@SerialNumber", serialNumber);
                    updateCmd.Parameters.AddWithValue("@Id", newId);
                    updateCmd.ExecuteNonQuery();
                }

                foreach (var row in results)
                {
                    using var resultCmd = new SqliteCommand(@"
                        INSERT INTO TestResults (PatientId, CategoryHeading, TestName, Result, Unit, NormalRange, Price, Discount)
                        VALUES (@PatientId, @CategoryHeading, @TestName, @Result, @Unit, @NormalRange, @Price, @Discount);", connection, transaction);

                    resultCmd.Parameters.AddWithValue("@PatientId", newId);
                    resultCmd.Parameters.AddWithValue("@CategoryHeading", string.IsNullOrWhiteSpace(row.CategoryHeading) ? "General" : row.CategoryHeading);
                    resultCmd.Parameters.AddWithValue("@TestName", row.TestName);
                    resultCmd.Parameters.AddWithValue("@Result", row.Result);
                    resultCmd.Parameters.AddWithValue("@Unit", row.Unit);
                    resultCmd.Parameters.AddWithValue("@NormalRange", row.NormalRange);
                    resultCmd.Parameters.AddWithValue("@Price", row.Price);
                    resultCmd.Parameters.AddWithValue("@Discount", row.Discount);
                    resultCmd.ExecuteNonQuery();
                }

                transaction.Commit();
                return serialNumber;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // ==================== STAGE 2: RESULTS ENTRY / COMPLETION ====================

        /// <summary>
        /// Returns every patient who has been billed but whose report has not been completed yet.
        /// </summary>
        public static List<Patient> GetPendingPatients()
        {
            var list = new List<Patient>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = new SqliteCommand(
                "SELECT Id, SerialNumber, Name, Age, Gender, ReferredBy, LabName, PhoneNumber, ReportHeading, CreatedAt, Status, TotalAmount, DiscountPercentage, NetAmount " +
                "FROM Patients WHERE Status = 'Pending' ORDER BY Id DESC", connection);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadPatient(reader));

            return list;
        }

        /// <summary>Returns a single patient by their database Id, or null if not found.</summary>
        public static Patient? GetPatientById(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = new SqliteCommand(
                "SELECT Id, SerialNumber, Name, Age, Gender, ReferredBy, LabName, PhoneNumber, ReportHeading, CreatedAt, Status, TotalAmount, DiscountPercentage, NetAmount " +
                "FROM Patients WHERE Id = @Id", connection);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadPatient(reader) : null;
        }

        /// <summary>
        /// Writes the entered Result value back into each existing TestResults row (matched by Id),
        /// and marks the patient's Status as "Completed". Call this once the Lab Attendant has
        /// finished performing the tests and typed in the results.
        /// </summary>
        public static void SaveResultsAndComplete(int patientId, List<TestResultItem> updatedResults)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var row in updatedResults)
                {
                    using var cmd = new SqliteCommand(
                        "UPDATE TestResults SET Result = @Result WHERE Id = @Id AND PatientId = @PatientId",
                        connection, transaction);
                    cmd.Parameters.AddWithValue("@Result", row.Result);
                    cmd.Parameters.AddWithValue("@Id", row.Id);
                    cmd.Parameters.AddWithValue("@PatientId", patientId);
                    cmd.ExecuteNonQuery();
                }

                using var statusCmd = new SqliteCommand(
                    "UPDATE Patients SET Status = 'Completed' WHERE Id = @PatientId", connection, transaction);
                statusCmd.Parameters.AddWithValue("@PatientId", patientId);
                statusCmd.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // ==================== SHARED READ HELPERS ====================

        public static List<Patient> GetAllPatients()
        {
            var list = new List<Patient>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = new SqliteCommand(
                "SELECT Id, SerialNumber, Name, Age, Gender, ReferredBy, LabName, PhoneNumber, ReportHeading, CreatedAt, Status, TotalAmount, DiscountPercentage, NetAmount " +
                "FROM Patients ORDER BY Id DESC", connection);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadPatient(reader));

            return list;
        }

        private static Patient ReadPatient(SqliteDataReader reader)
        {
            var patient = new Patient
            {
                Id = reader.GetInt32(0),
                SerialNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Name = reader.GetString(2),
                Age = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Gender = reader.IsDBNull(4) ? "" : reader.GetString(4),
                ReferredBy = reader.IsDBNull(5) ? "" : reader.GetString(5),
                LabName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                PhoneNumber = reader.IsDBNull(7) ? "" : reader.GetString(7),
                ReportHeading = reader.IsDBNull(8) ? "" : reader.GetString(8),
                CreatedAt = reader.IsDBNull(9) ? DateTime.Now : DateTime.Parse(reader.GetString(9)),
                Status = reader.IsDBNull(10) || string.IsNullOrWhiteSpace(reader.GetString(10)) ? "Completed" : reader.GetString(10),
                TotalAmount = reader.IsDBNull(11) ? 0 : Convert.ToDecimal(reader.GetDouble(11)),
                DiscountPercentage = reader.IsDBNull(12) ? 0 : Convert.ToDecimal(reader.GetDouble(12)),
                NetAmount = reader.IsDBNull(13) ? 0 : Convert.ToDecimal(reader.GetDouble(13))
            };

            // Backward compatibility: bills created before the discount feature existed have
            // NetAmount = 0 in the database. Treat those as "no discount" -> Net = Total.
            if (patient.NetAmount == 0 && patient.DiscountPercentage == 0 && patient.TotalAmount > 0)
                patient.NetAmount = patient.TotalAmount;

            return patient;
        }

        /// <summary>
        /// Returns the saved test-result rows for a given patient id, including Id, CategoryHeading
        /// and Price, in the order they were inserted (so grouping by category stays consistent).
        /// </summary>
        public static List<TestResultItem> GetResultsForPatient(int patientId)
        {
            var list = new List<TestResultItem>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = new SqliteCommand(
                "SELECT Id, CategoryHeading, TestName, Result, Unit, NormalRange, Price, Discount FROM TestResults WHERE PatientId = @PatientId ORDER BY Id", connection);
            cmd.Parameters.AddWithValue("@PatientId", patientId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TestResultItem
                {
                    Id = reader.GetInt32(0),
                    CategoryHeading = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    TestName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Result = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Unit = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    NormalRange = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Price = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetDouble(6)),
                    Discount = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader.GetDouble(7))
                });
            }
            return list;
        }

        // ==================== TEST DEFINITIONS (master data / autocomplete source) ====================

        /// <summary>
        /// Inserts a new predefined test, or updates it if a test with the same Id (or same TestName) already exists.
        /// </summary>
        public static void InsertOrUpdateTestDefinition(TestDefinition def)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            if (def.Id == 0)
            {
                // New entry. If a test with the same name already exists, update it instead (upsert).
                using var cmd = new SqliteCommand(@"
                    INSERT INTO TestDefinitions (CategoryHeading, TestName, Unit, NormalRange, Price)
                    VALUES (@Category, @TestName, @Unit, @NormalRange, @Price)
                    ON CONFLICT(TestName) DO UPDATE SET
                        CategoryHeading = excluded.CategoryHeading,
                        Unit = excluded.Unit,
                        NormalRange = excluded.NormalRange,
                        Price = excluded.Price;", connection);

                cmd.Parameters.AddWithValue("@Category", def.CategoryHeading);
                cmd.Parameters.AddWithValue("@TestName", def.TestName);
                cmd.Parameters.AddWithValue("@Unit", def.Unit);
                cmd.Parameters.AddWithValue("@NormalRange", def.NormalRange);
                cmd.Parameters.AddWithValue("@Price", def.Price);
                cmd.ExecuteNonQuery();
            }
            else
            {
                using var cmd = new SqliteCommand(@"
                    UPDATE TestDefinitions
                    SET CategoryHeading = @Category, TestName = @TestName, Unit = @Unit, NormalRange = @NormalRange, Price = @Price
                    WHERE Id = @Id;", connection);

                cmd.Parameters.AddWithValue("@Category", def.CategoryHeading);
                cmd.Parameters.AddWithValue("@TestName", def.TestName);
                cmd.Parameters.AddWithValue("@Unit", def.Unit);
                cmd.Parameters.AddWithValue("@NormalRange", def.NormalRange);
                cmd.Parameters.AddWithValue("@Price", def.Price);
                cmd.Parameters.AddWithValue("@Id", def.Id);
                cmd.ExecuteNonQuery();
            }
        }

        public static List<TestDefinition> GetAllTestDefinitions()
        {
            var list = new List<TestDefinition>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = new SqliteCommand(
                "SELECT Id, CategoryHeading, TestName, Unit, NormalRange, Price FROM TestDefinitions ORDER BY CategoryHeading, TestName", connection);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TestDefinition
                {
                    Id = reader.GetInt32(0),
                    CategoryHeading = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    TestName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Unit = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    NormalRange = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Price = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetDouble(5))
                });
            }
            return list;
        }

        public static void DeleteTestDefinition(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = new SqliteCommand("DELETE FROM TestDefinitions WHERE Id = @Id", connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }
    }
}