using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Курсовой_проект.Models;

public partial class OzvsKursachContext : DbContext
{
    public OzvsKursachContext()
    {
    }

    public OzvsKursachContext(DbContextOptions<OzvsKursachContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Driver> Drivers { get; set; }

    public virtual DbSet<MaintenanceRepair> MaintenanceRepairs { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Part> Parts { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Vehicle> Vehicles { get; set; }

    public virtual DbSet<VehicleAssignment> VehicleAssignments { get; set; }

    public virtual DbSet<WorkPart> WorkParts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlServer(Configuration.DatabaseSettings.GetConnectionString());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationRead>(entity =>
        {
            entity.ToTable("NotificationReads");
            entity.HasKey(x => new { x.UserId, x.EventKey });
            entity.Property(x => x.EventKey).HasMaxLength(200);
        });
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog");
            entity.HasKey(e => e.AuditLogId);
            entity.Property(e => e.ActorName).HasMaxLength(250).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(20).IsRequired();
            entity.Property(e => e.EntityName).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.ChangedAt);
            entity.HasIndex(e => new { e.ActorUserId, e.ChangedAt });
        });
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.DocumentId).HasName("PK__Document__1ABEEF0F23B11C7C");

            entity.Property(e => e.Cost).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.DocumentNumber).HasMaxLength(100);
            entity.Property(e => e.DocumentType).HasMaxLength(50);
            entity.Property(e => e.IssuingOrganization).HasMaxLength(200);

            entity.HasOne(d => d.Driver).WithMany(p => p.Documents)
                .HasForeignKey(d => d.DriverId)
                .HasConstraintName("FK_Documents_Drivers");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.Documents)
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK_Documents_Vehicles");
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(e => e.DriverId).HasName("PK__Drivers__F1B1CD042E944A7F");

            entity.HasIndex(e => e.LicenseNumber, "UQ__Drivers__E8890166FA590CA2").IsUnique();

            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.LicenseCategory).HasMaxLength(20);
            entity.Property(e => e.LicenseNumber).HasMaxLength(50);
            entity.Property(e => e.MiddleName).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
        });

        modelBuilder.Entity<MaintenanceRepair>(entity =>
        {
            entity.HasKey(e => e.WorkId).HasName("PK__Maintena__2DE6D5F523AAC01C");

            entity.Property(e => e.Cost).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.WorkType).HasMaxLength(200);

            entity.HasOne(d => d.Vehicle).WithMany(p => p.MaintenanceRepairs)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MaintenanceRepairs_Vehicles");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E12918FE359");

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DaysBefore).HasDefaultValue(30);
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.NotificationType).HasMaxLength(50);
            entity.Property(e => e.RelatedEntityType).HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Notifications_Users");
        });

        modelBuilder.Entity<Part>(entity =>
        {
            entity.HasKey(e => e.PartId).HasName("PK__Parts__7C3F0D50E3B1D639");

            entity.HasIndex(e => e.ArticleNumber, "UQ__Parts__3C991142BC82810C").IsUnique();

            entity.Property(e => e.ArticleNumber).HasMaxLength(100);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Cost).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1A1F8B4552");

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4CA6B92A1D");

            entity.HasIndex(e => e.Login, "UQ__Users__5E55825B398A81FF").IsUnique();

            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Login).HasMaxLength(100);
            entity.Property(e => e.MiddleName).HasMaxLength(100);
            entity.Property(e => e.Password).HasMaxLength(256);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Roles");
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.VehicleId).HasName("PK__Vehicles__476B549223CB9962");

            entity.HasIndex(e => e.LicensePlate, "UQ__Vehicles__026BC15C8EF41BBA").IsUnique();

            entity.HasIndex(e => e.VinNumber, "UQ__Vehicles__24D61C8E267E1D10").IsUnique();

            entity.Property(e => e.Brand).HasMaxLength(100);
            entity.Property(e => e.LicensePlate).HasMaxLength(20);
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("В эксплуатации");
            entity.Property(e => e.VinNumber).HasMaxLength(50);
            // Проверка исходных значений в UPDATE/DELETE без добавления столбцов в БД.
            foreach (var property in entity.Metadata.GetProperties())
                if (property.Name != nameof(Vehicle.VehicleId))
                    property.IsConcurrencyToken = true;
        });

        modelBuilder.Entity<VehicleAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("PK__VehicleA__32499E77914CA56F");

            entity.HasOne(d => d.Driver).WithMany(p => p.VehicleAssignments)
                .HasForeignKey(d => d.DriverId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VehicleAssignments_Drivers");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.VehicleAssignments)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VehicleAssignments_Vehicles");
        });

        modelBuilder.Entity<WorkPart>(entity =>
        {
            entity.HasKey(e => e.CompositionId).HasName("PK__WorkPart__B8E2331F0A6D18B6");

            entity.HasOne(d => d.Part).WithMany(p => p.WorkParts)
                .HasForeignKey(d => d.PartId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WorkParts_Parts");

            entity.HasOne(d => d.Work).WithMany(p => p.WorkParts)
                .HasForeignKey(d => d.WorkId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WorkParts_MaintenanceRepairs");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
