using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ApiWebTrackerGanado.Data
{
    public class CattleTrackingContext : DbContext
    {
        public CattleTrackingContext(DbContextOptions<CattleTrackingContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Farm> Farms { get; set; }
        public DbSet<Animal> Animals { get; set; }
        public DbSet<Tracker> Trackers { get; set; }
        public DbSet<LocationHistory> LocationHistories { get; set; }
        public DbSet<Pasture> Pastures { get; set; }
        public DbSet<PastureUsage> PastureUsages { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<HealthRecord> HealthRecords { get; set; }
        public DbSet<WeightRecord> WeightRecords { get; set; }
        public DbSet<BreedingRecord> BreedingRecords { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<FarmBoundary> FarmBoundaries { get; set; }

        // Nuevas entidades para gestión de clientes y licencias
        public DbSet<Customer> Customers { get; set; }
        public DbSet<License> Licenses { get; set; }
        public DbSet<CustomerTracker> CustomerTrackers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User Configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).IsRequired();
                entity.Property(e => e.PasswordHash).IsRequired();
            });

            // Farm Configuration
            modelBuilder.Entity<Farm>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                      .WithMany(e => e.Farms)
                      .HasForeignKey(e => e.UserId);
                entity.HasMany(e => e.BoundaryCoordinates)
                      .WithOne(e => e.Farm)
                      .HasForeignKey(e => e.FarmId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // FarmBoundary Configuration
            modelBuilder.Entity<FarmBoundary>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Farm)
                      .WithMany(e => e.BoundaryCoordinates)
                      .HasForeignKey(e => e.FarmId);
                entity.Property(e => e.Latitude).HasPrecision(10, 8);
                entity.Property(e => e.Longitude).HasPrecision(11, 8);
                entity.HasIndex(e => new { e.FarmId, e.SequenceOrder });
            });

            // Animal Configuration
            modelBuilder.Entity<Animal>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Farm)
                      .WithMany(e => e.Animals)
                      .HasForeignKey(e => e.FarmId);
                entity.HasOne(e => e.Tracker)
                      .WithOne(e => e.Animal)
                      .HasForeignKey<Animal>(e => e.TrackerId)
                      .IsRequired(false);
                entity.HasOne(e => e.CustomerTracker)
                      .WithOne(e => e.AssignedAnimal)
                      .HasForeignKey<Animal>(e => e.CustomerTrackerId)
                      .IsRequired(false);
                entity.Property(e => e.Weight).HasPrecision(10, 2);
            });

            // Tracker Configuration
            modelBuilder.Entity<Tracker>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.DeviceId).IsUnique();

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .IsRequired(false);
            });

            // LocationHistory Configuration
            modelBuilder.Entity<LocationHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Animal)
                      .WithMany(e => e.LocationHistories)
                      .HasForeignKey(e => e.AnimalId);
                entity.HasOne(e => e.Tracker)
                      .WithMany(e => e.LocationHistories)
                      .HasForeignKey(e => e.TrackerId);
                // Temporarily disabled for PostGIS migration
                // entity.Property(e => e.Location).HasColumnType("geometry");
                entity.HasIndex(e => e.Timestamp);
                // CRITICAL: Composite index for map queries (GROUP BY AnimalId ORDER BY Timestamp DESC)
                entity.HasIndex(e => new { e.AnimalId, e.Timestamp });
                // Index for tracker-based queries
                entity.HasIndex(e => new { e.TrackerId, e.Timestamp });
            });

            // Pasture Configuration
            modelBuilder.Entity<Pasture>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Farm)
                      .WithMany(e => e.Pastures)
                      .HasForeignKey(e => e.FarmId);
                entity.Property(e => e.Area).HasColumnType("geometry");
                entity.Property(e => e.AreaSize).HasPrecision(10, 2);
            });

            // PastureUsage Configuration
            modelBuilder.Entity<PastureUsage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Pasture)
                      .WithMany(e => e.PastureUsages)
                      .HasForeignKey(e => e.PastureId);
                entity.HasOne(e => e.Animal)
                      .WithMany()
                      .HasForeignKey(e => e.AnimalId);
            });

            // Alert Configuration
            modelBuilder.Entity<Alert>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Animal)
                      .WithMany(e => e.Alerts)
                      .HasForeignKey(e => e.AnimalId);
                entity.HasIndex(e => e.CreatedAt);
            });

            // HealthRecord Configuration
            modelBuilder.Entity<HealthRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Animal)
                      .WithMany(e => e.HealthRecords)
                      .HasForeignKey(e => e.AnimalId);
                entity.Property(e => e.Cost).HasPrecision(10, 2);
            });

            // WeightRecord Configuration
            modelBuilder.Entity<WeightRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Animal)
                      .WithMany(e => e.WeightRecords)
                      .HasForeignKey(e => e.AnimalId);
                entity.Property(e => e.Weight).HasPrecision(10, 2);
            });

            // BreedingRecord Configuration
            modelBuilder.Entity<BreedingRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Animal)
                      .WithMany(e => e.BreedingRecords)
                      .HasForeignKey(e => e.AnimalId);
            });

            // Transaction Configuration
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Animal)
                      .WithMany()
                      .HasForeignKey(e => e.AnimalId);
                entity.Property(e => e.Amount).HasPrecision(12, 2);
            });

            // Customer Configuration
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                      .WithOne()
                      .HasForeignKey<Customer>(e => e.UserId);
                entity.HasIndex(e => e.TaxId).IsUnique();
                entity.HasIndex(e => e.CompanyName);
                entity.Property(e => e.CompanyName).IsRequired();
            });

            // License Configuration
            modelBuilder.Entity<License>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Customer)
                      .WithMany(e => e.Licenses)
                      .HasForeignKey(e => e.CustomerId);
                entity.HasIndex(e => e.LicenseKey).IsUnique();
                entity.Property(e => e.LicenseKey).IsRequired();
                entity.HasIndex(e => new { e.Status, e.ExpiresAt });
            });

            // CustomerTracker Configuration
            modelBuilder.Entity<CustomerTracker>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Customer)
                      .WithMany(e => e.CustomerTrackers)
                      .HasForeignKey(e => e.CustomerId);
                entity.HasOne(e => e.Tracker)
                      .WithMany(e => e.CustomerTrackers)
                      .HasForeignKey(e => e.TrackerId);
                // Relación comentada porque la columna AssignedByUserId no existe en BD
                // entity.HasOne(e => e.AssignedByUser)
                //       .WithMany()
                //       .HasForeignKey(e => e.AssignedByUserId)
                //       .IsRequired(false);
                // entity.HasOne(e => e.License)
                //       .WithMany()
                //       .HasForeignKey(e => e.LicenseId)
                //       .IsRequired(false);
                // NOTA: Relación comentada porque la columna LicenseId no existe en BD

                // Constraint: Un tracker solo puede estar asignado a un cliente a la vez
                entity.HasIndex(e => new { e.TrackerId, e.Status })
                      .HasDatabaseName("IX_CustomerTracker_OneActivePerTracker")
                      .HasFilter("\"Status\" = 'Active'")
                      .IsUnique();

                entity.HasIndex(e => new { e.CustomerId, e.Status });
            });
        }
    }
}

