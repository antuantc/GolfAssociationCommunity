using GolfAssociationCommunity.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<GolfAssociation> GolfAssociations { get; set; } = null!;
        public DbSet<Tournament> Tournaments { get; set; } = null!;
        public DbSet<SponsorshipPackage> SponsorshipPackages { get; set; } = null!;
        public DbSet<Registration> Registrations { get; set; } = null!;
        public DbSet<PlayerScore> PlayerScores { get; set; } = null!;
        public DbSet<Leaderboard> Leaderboards { get; set; } = null!;
        public DbSet<AdminAuditEvent> AdminAuditEvents { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<GolfAssociation>()
                .HasMany(ga => ga.Members)
                .WithOne(u => u.GolfAssociation)
                .HasForeignKey(u => u.GolfAssociationId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<GolfAssociation>()
                .HasMany(ga => ga.Tournaments)
                .WithOne(t => t.GolfAssociation)
                .HasForeignKey(t => t.GolfAssociationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<GolfAssociation>()
                .HasMany(ga => ga.SponsorshipPackages)
                .WithOne(sp => sp.GolfAssociation)
                .HasForeignKey(sp => sp.GolfAssociationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SponsorshipPackage>()
                .Property(sp => sp.Amount)
                .HasPrecision(18, 2);

            builder.Entity<SponsorshipPackage>()
                .Property(sp => sp.Name)
                .HasMaxLength(120);

            builder.Entity<SponsorshipPackage>()
                .Property(sp => sp.Benefits)
                .HasMaxLength(2000);

            builder.Entity<SponsorshipPackage>()
                .HasIndex(sp => new { sp.GolfAssociationId, sp.DisplayOrder });

            builder.Entity<Tournament>()
                .HasMany(t => t.Registrations)
                .WithOne(r => r.Tournament)
                .HasForeignKey(r => r.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Tournament>()
                .Property(t => t.EntryFee)
                .HasPrecision(18, 2);

            builder.Entity<Tournament>()
                .HasMany(t => t.PlayerScores)
                .WithOne(ps => ps.Tournament)
                .HasForeignKey(ps => ps.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Registration>()
                .Property(r => r.RegistrationFee)
                .HasPrecision(18, 2);

            builder.Entity<Registration>()
                .Property(r => r.Handicap)
                .HasPrecision(5, 2);

            builder.Entity<Registration>()
                .HasOne(r => r.Player)
                .WithMany()
                .HasForeignKey(r => r.PlayerId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<PlayerScore>()
                .HasOne(ps => ps.Player)
                .WithMany()
                .HasForeignKey(ps => ps.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Leaderboard>()
                .HasOne(l => l.Player)
                .WithMany()
                .HasForeignKey(l => l.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Leaderboard>()
                .HasOne(l => l.Tournament)
                .WithMany()
                .HasForeignKey(l => l.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AdminAuditEvent>()
                .Property(a => a.Action)
                .HasMaxLength(200);

            builder.Entity<AdminAuditEvent>()
                .Property(a => a.Actor)
                .HasMaxLength(256);

            builder.Entity<AdminAuditEvent>()
                .HasIndex(a => a.AtUtc);

            builder.Entity<AdminAuditEvent>()
                .HasIndex(a => a.Action);
        }
    }
}
