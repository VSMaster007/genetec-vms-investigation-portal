using Investigation.Domain;
using Microsoft.EntityFrameworkCore;

namespace Investigation.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<CameraMapping> CameraMappings => Set<CameraMapping>();
    public DbSet<InvestigationAudit> Audits => Set<InvestigationAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.Email).HasMaxLength(256);
            entity.Property(user => user.DisplayName).HasMaxLength(128);
            entity.Property(user => user.PasswordHash).HasMaxLength(512);
        });

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.HasIndex(incident => incident.ExternalIncidentId).IsUnique();
            entity.HasIndex(incident => incident.EventTimestampUtc);
            entity.HasIndex(incident => incident.ExternalCameraId);
            entity.Property(incident => incident.ExternalIncidentId).HasMaxLength(64);
            entity.Property(incident => incident.Title).HasMaxLength(200);
            entity.Property(incident => incident.ExternalCameraId).HasMaxLength(64);
            entity.Property(incident => incident.EventType).HasMaxLength(80);
            entity.Property(incident => incident.Location).HasMaxLength(160);
        });

        modelBuilder.Entity<Camera>(entity =>
        {
            entity.HasIndex(camera => camera.GenetecCameraGuid).IsUnique();
            entity.Property(camera => camera.CameraName).HasMaxLength(200);
            entity.Property(camera => camera.Location).HasMaxLength(160);
        });

        modelBuilder.Entity<CameraMapping>(entity =>
        {
            entity.HasIndex(mapping => mapping.ExternalCameraId).IsUnique();
            entity.HasOne(mapping => mapping.Camera)
                .WithMany(camera => camera.Mappings)
                .HasForeignKey(mapping => mapping.GenetecCameraId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvestigationAudit>(entity =>
        {
            entity.HasIndex(audit => audit.CreatedAt);
            entity.HasIndex(audit => audit.UserId);
            entity.Property(audit => audit.Action).HasMaxLength(64);
            entity.Property(audit => audit.Details).HasMaxLength(1000);
            entity.HasOne(audit => audit.User)
                .WithMany()
                .HasForeignKey(audit => audit.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(audit => audit.Incident)
                .WithMany()
                .HasForeignKey(audit => audit.IncidentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
