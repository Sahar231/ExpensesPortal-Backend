using System;
using System.Collections.Generic;
using FraisMission.Models;
using Microsoft.EntityFrameworkCore;

namespace FraisMission.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Approval> Approvals { get; set; }

    public virtual DbSet<Expense> Expenses { get; set; }

    public virtual DbSet<Mission> Missions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=localhost;Database=ExpensePortailDb;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Approval>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Approval__3214EC07BD813E03");

            entity.ToTable("Approval");

            entity.Property(e => e.ReviewedAt)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Status).HasMaxLength(50);

            entity.HasOne(d => d.Expense).WithMany(p => p.Approvals)
                .HasForeignKey(d => d.ExpenseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Approval_Expense");

            entity.HasOne(d => d.ReviewedBy).WithMany(p => p.Approvals)
                .HasForeignKey(d => d.ReviewedById)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Approval_User");
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Expense__3214EC07FF5BE3E9");

            entity.ToTable("Expense");

            entity.Property(e => e.Categorie).HasMaxLength(100);
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Montant).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Statut).HasMaxLength(50);

            entity.HasOne(d => d.Employee).WithMany(p => p.Expenses)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Expense_User");

            entity.HasOne(d => d.Mission).WithMany(p => p.Expenses)
                .HasForeignKey(d => d.MissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Expense_Mission");
        });

        modelBuilder.Entity<Mission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Mission__3214EC0784DBE08C");

            entity.ToTable("Mission");

            entity.Property(e => e.DateDebut).HasColumnType("datetime");
            entity.Property(e => e.DateFin).HasColumnType("datetime");
            entity.Property(e => e.Lieu).HasMaxLength(150);
            entity.Property(e => e.Nom).HasMaxLength(200);

            entity.HasOne(d => d.Manager).WithMany(p => p.Missions)
                .HasForeignKey(d => d.ManagerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Mission_User");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__User__3214EC074F9D15A5");

            entity.ToTable("User");

            entity.HasIndex(e => e.Email, "UQ__User__A9D10534F5C13078").IsUnique();

            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.Nom).HasMaxLength(100);
            entity.Property(e => e.Prenom).HasMaxLength(100);
            entity.Property(e => e.Role).HasMaxLength(50);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}