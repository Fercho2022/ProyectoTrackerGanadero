using Npgsql;

namespace ApiWebTrackerGanado
{
    public class TestConnection
    {
        public static async Task TestDatabaseConnection()
        {
            var connectionString = "Host=localhost;Database=CattleTrackingDB;Username=postgres;Password=Ferchitovil1830;Port=5432";

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                Console.WriteLine("✅ Database connection successful!");

                // Test if tables exist
                var command = new NpgsqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'", connection);
                using var reader = await command.ExecuteReaderAsync();

                Console.WriteLine("Existing tables:");
                while (await reader.ReadAsync())
                {
                    Console.WriteLine($"  - {reader.GetString(0)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Database connection failed: {ex.Message}");
            }
        }

        public static async Task CreateTablesDirectly()
        {
            var connectionString = "Host=localhost;Database=CattleTrackingDB;Username=postgres;Password=Ferchitovil1830;Port=5432";

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                Console.WriteLine("Creating all missing tables...");

                // Create all tables in proper order (dependencies first)

                // 1. Users table
                await ExecuteCommand(connection, @"
                    CREATE TABLE IF NOT EXISTS ""Users"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Name"" TEXT NOT NULL,
                        ""Email"" TEXT NOT NULL,
                        ""PasswordHash"" TEXT NOT NULL,
                        ""Role"" TEXT NOT NULL DEFAULT 'User',
                        ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Email"" ON ""Users""(""Email"");
                ");

                // 2. Trackers table (updated with new customer management fields)
                await ExecuteCommand(connection, @"
                    CREATE TABLE IF NOT EXISTS ""Trackers"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""DeviceId"" TEXT NOT NULL,
                        ""Name"" TEXT,
                        ""SerialNumber"" TEXT,
                        ""Manufacturer"" TEXT,
                        ""Status"" TEXT NOT NULL DEFAULT 'Active',
                        ""IsAvailableForAssignment"" BOOLEAN NOT NULL DEFAULT TRUE,
                        ""BatteryLevel"" DOUBLE PRECISION NOT NULL,
                        ""IsActive"" BOOLEAN NOT NULL,
                        ""LastHeartbeat"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Trackers_DeviceId"" ON ""Trackers""(""DeviceId"");
                    ALTER TABLE ""Trackers"" ADD COLUMN IF NOT EXISTS ""Name"" TEXT;
                    ALTER TABLE ""Trackers"" ADD COLUMN IF NOT EXISTS ""SerialNumber"" TEXT;
                    ALTER TABLE ""Trackers"" ADD COLUMN IF NOT EXISTS ""Manufacturer"" TEXT;
                    ALTER TABLE ""Trackers"" ADD COLUMN IF NOT EXISTS ""Status"" TEXT NOT NULL DEFAULT 'Active';
                    ALTER TABLE ""Trackers"" ADD COLUMN IF NOT EXISTS ""IsAvailableForAssignment"" BOOLEAN NOT NULL DEFAULT TRUE;
                ");

                // 3. Farms table - add missing Address column
                await ExecuteCommand(connection, @"
                    CREATE TABLE IF NOT EXISTS ""Farms"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Name"" TEXT NOT NULL,
                        ""Location"" TEXT NOT NULL,
                        ""Address"" TEXT,
                        ""TotalArea"" NUMERIC NOT NULL,
                        ""Boundaries"" TEXT NOT NULL,
                        ""UserId"" INTEGER NOT NULL REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    ALTER TABLE ""Farms"" ADD COLUMN IF NOT EXISTS ""Address"" TEXT;
                    CREATE INDEX IF NOT EXISTS ""IX_Farms_UserId"" ON ""Farms""(""UserId"");
                ");

                // 4. Animals table
                await ExecuteCommand(connection, @"
                    CREATE TABLE IF NOT EXISTS ""Animals"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Tag"" TEXT NOT NULL,
                        ""Name"" TEXT NOT NULL,
                        ""Breed"" TEXT NOT NULL,
                        ""Gender"" TEXT NOT NULL,
                        ""BirthDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""Weight"" NUMERIC(10,2) NOT NULL,
                        ""HealthStatus"" TEXT NOT NULL,
                        ""FarmId"" INTEGER NOT NULL REFERENCES ""Farms""(""Id"") ON DELETE CASCADE,
                        ""TrackerId"" INTEGER REFERENCES ""Trackers""(""Id""),
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_Animals_FarmId"" ON ""Animals""(""FarmId"");
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Animals_TrackerId"" ON ""Animals""(""TrackerId"") WHERE ""TrackerId"" IS NOT NULL;
                ");

                // 5. Pastures table - add missing IsActive column
                await ExecuteCommand(connection, @"
                    CREATE TABLE IF NOT EXISTS ""Pastures"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Name"" TEXT NOT NULL,
                        ""Area"" TEXT NOT NULL,
                        ""AreaSize"" NUMERIC(10,2) NOT NULL,
                        ""GrassType"" TEXT NOT NULL,
                        ""Capacity"" INTEGER NOT NULL,
                        ""CurrentOccupancy"" INTEGER NOT NULL,
                        ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                        ""FarmId"" INTEGER NOT NULL REFERENCES ""Farms""(""Id"") ON DELETE CASCADE,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    ALTER TABLE ""Pastures"" ADD COLUMN IF NOT EXISTS ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE;
                    CREATE INDEX IF NOT EXISTS ""IX_Pastures_FarmId"" ON ""Pastures""(""FarmId"");
                ");

                // 6. Rest of the tables
                await ExecuteCommand(connection, @"
                    CREATE TABLE IF NOT EXISTS ""Alerts"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Type"" TEXT NOT NULL,
                        ""Message"" TEXT NOT NULL,
                        ""Severity"" TEXT NOT NULL,
                        ""IsResolved"" BOOLEAN NOT NULL,
                        ""AnimalId"" INTEGER NOT NULL REFERENCES ""Animals""(""Id"") ON DELETE CASCADE,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_Alerts_AnimalId"" ON ""Alerts""(""AnimalId"");
                    CREATE INDEX IF NOT EXISTS ""IX_Alerts_CreatedAt"" ON ""Alerts""(""CreatedAt"");
                ");

                await ExecuteCommand(connection, @"
                    CREATE TABLE IF NOT EXISTS ""LocationHistories"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Location"" TEXT NOT NULL,
                        ""Timestamp"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""Accuracy"" DOUBLE PRECISION NOT NULL,
                        ""Speed"" DOUBLE PRECISION NOT NULL,
                        ""AnimalId"" INTEGER NOT NULL REFERENCES ""Animals""(""Id"") ON DELETE CASCADE,
                        ""TrackerId"" INTEGER NOT NULL REFERENCES ""Trackers""(""Id"") ON DELETE CASCADE,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_LocationHistories_AnimalId"" ON ""LocationHistories""(""AnimalId"");
                    CREATE INDEX IF NOT EXISTS ""IX_LocationHistories_TrackerId"" ON ""LocationHistories""(""TrackerId"");
                    CREATE INDEX IF NOT EXISTS ""IX_LocationHistories_Timestamp"" ON ""LocationHistories""(""Timestamp"");
                ");

                await ExecuteCommand(connection, @"
                    CREATE TABLE IF NOT EXISTS ""HealthRecords"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""TreatmentType"" TEXT NOT NULL,
                        ""TreatmentDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""VeterinarianName"" TEXT NOT NULL,
                        ""Cost"" NUMERIC(10,2) NOT NULL,
                        ""Notes"" TEXT NOT NULL,
                        ""NextTreatmentDate"" TIMESTAMP WITH TIME ZONE,
                        ""AnimalId"" INTEGER NOT NULL REFERENCES ""Animals""(""Id"") ON DELETE CASCADE,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_HealthRecords_AnimalId"" ON ""HealthRecords""(""AnimalId"");
                ");

                await ExecuteCommand(connection, @"
                    CREATE TABLE IF NOT EXISTS ""WeightRecords"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Weight"" NUMERIC(10,2) NOT NULL,
                        ""MeasuredDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""Notes"" TEXT NOT NULL,
                        ""AnimalId"" INTEGER NOT NULL REFERENCES ""Animals""(""Id"") ON DELETE CASCADE,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_WeightRecords_AnimalId"" ON ""WeightRecords""(""AnimalId"");
                ");

                await ExecuteCommand(connection, @"
                    CREATE TABLE IF NOT EXISTS ""BreedingRecords"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""MatingDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""ExpectedCalvingDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""ActualCalvingDate"" TIMESTAMP WITH TIME ZONE,
                        ""BullId"" TEXT NOT NULL,
                        ""Notes"" TEXT NOT NULL,
                        ""AnimalId"" INTEGER NOT NULL REFERENCES ""Animals""(""Id"") ON DELETE CASCADE,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_BreedingRecords_AnimalId"" ON ""BreedingRecords""(""AnimalId"");
                ");

                await ExecuteCommand(connection, @"
                    CREATE TABLE IF NOT EXISTS ""Transactions"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Type"" TEXT NOT NULL,
                        ""Amount"" NUMERIC(12,2) NOT NULL,
                        ""Date"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""Description"" TEXT NOT NULL,
                        ""BuyerSellerInfo"" TEXT NOT NULL,
                        ""AnimalId"" INTEGER NOT NULL REFERENCES ""Animals""(""Id"") ON DELETE CASCADE,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_Transactions_AnimalId"" ON ""Transactions""(""AnimalId"");
                ");

                await ExecuteCommand(connection, @"
                    CREATE TABLE IF NOT EXISTS ""PastureUsages"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""StartDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""EndDate"" TIMESTAMP WITH TIME ZONE,
                        ""PastureId"" INTEGER NOT NULL REFERENCES ""Pastures""(""Id"") ON DELETE CASCADE,
                        ""AnimalId"" INTEGER NOT NULL REFERENCES ""Animals""(""Id"") ON DELETE CASCADE,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_PastureUsages_PastureId"" ON ""PastureUsages""(""PastureId"");
                    CREATE INDEX IF NOT EXISTS ""IX_PastureUsages_AnimalId"" ON ""PastureUsages""(""AnimalId"");
                ");

                // Insert test user if not exists
                await ExecuteCommand(connection, @"
                    INSERT INTO ""Users"" (""Name"", ""Email"", ""PasswordHash"", ""Role"", ""IsActive"")
                    SELECT 'Test User', 'test@trackerganadero.com', 'hashed_password', 'User', TRUE
                    WHERE NOT EXISTS (SELECT 1 FROM ""Users"" WHERE ""Email"" = 'test@trackerganadero.com');
                ");

                Console.WriteLine("✅ All database tables created successfully!");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Table creation failed: {ex.Message}");
            }
        }

        private static async Task ExecuteCommand(NpgsqlConnection connection, string sql)
        {
            var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}