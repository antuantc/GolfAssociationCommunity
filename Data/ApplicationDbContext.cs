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
        public DbSet<AssociationPlayer> AssociationPlayers { get; set; } = null!;
        public DbSet<AssociationOfficer> AssociationOfficers { get; set; } = null!;
        public DbSet<Tournament> Tournaments { get; set; } = null!;
        public DbSet<SponsorshipPackage> SponsorshipPackages { get; set; } = null!;
        public DbSet<SponsorshipPayment> SponsorshipPayments { get; set; } = null!;
        public DbSet<Registration> Registrations { get; set; } = null!;
        public DbSet<PlayerScore> PlayerScores { get; set; } = null!;
        public DbSet<Leaderboard> Leaderboards { get; set; } = null!;
        public DbSet<AdminAuditEvent> AdminAuditEvents { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<GolfAssociation>()
                .HasMany(ga => ga.OfficersAndMembers)
                .WithOne(o => o.GolfAssociation)
                .HasForeignKey(o => o.GolfAssociationId)
                .OnDelete(DeleteBehavior.Cascade);

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
                .HasMany(ga => ga.Players)
                .WithOne(player => player.GolfAssociation)
                .HasForeignKey(player => player.GolfAssociationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<GolfAssociation>()
                .Property(ga => ga.ThemeKey)
                .HasMaxLength(50);

            builder.Entity<GolfAssociation>()
                .Property(ga => ga.AuthorizeNetApiLoginId)
                .HasMaxLength(128);

            builder.Entity<GolfAssociation>()
                .Property(ga => ga.AuthorizeNetTransactionKey)
                .HasMaxLength(128);

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

            builder.Entity<GolfAssociation>()
                .HasMany(ga => ga.SponsorshipPayments)
                .WithOne(sp => sp.GolfAssociation)
                .HasForeignKey(sp => sp.GolfAssociationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SponsorshipPackage>()
                .HasMany(sp => sp.SponsorshipPayments)
                .WithOne(p => p.SponsorshipPackage)
                .HasForeignKey(p => p.SponsorshipPackageId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<SponsorshipPayment>()
                .Property(sp => sp.AmountPaid)
                .HasPrecision(18, 2);

            builder.Entity<SponsorshipPayment>()
                .Property(sp => sp.PackageName)
                .HasMaxLength(120);

            builder.Entity<SponsorshipPayment>()
                .Property(sp => sp.SponsorName)
                .HasMaxLength(120);

            builder.Entity<SponsorshipPayment>()
                .Property(sp => sp.SponsorEmail)
                .HasMaxLength(256);

            builder.Entity<SponsorshipPayment>()
                .HasIndex(sp => sp.PaidAtUtc);

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

            builder.Entity<AssociationPlayer>()
                .Property(player => player.DisplayName)
                .HasMaxLength(160);

            builder.Entity<AssociationPlayer>()
                .Property(player => player.Email)
                .HasMaxLength(256);

            builder.Entity<AssociationPlayer>()
                .Property(player => player.HandicapIndex)
                .HasPrecision(5, 2);

            builder.Entity<AssociationPlayer>()
                .HasIndex(player => new { player.GolfAssociationId, player.Email });

            builder.Entity<Registration>()
                .Property(r => r.RegistrationFee)
                .HasPrecision(18, 2);

            builder.Entity<Registration>()
                .Property(r => r.Handicap)
                .HasPrecision(5, 2);

            builder.Entity<Registration>()
                .HasOne(r => r.AssociationPlayer)
                .WithMany(player => player.Registrations)
                .HasForeignKey(r => r.AssociationPlayerId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<PlayerScore>()
                .HasOne(ps => ps.AssociationPlayer)
                .WithMany(player => player.Scores)
                .HasForeignKey(ps => ps.AssociationPlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Leaderboard>()
                .HasOne(l => l.AssociationPlayer)
                .WithMany(player => player.LeaderboardEntries)
                .HasForeignKey(l => l.AssociationPlayerId)
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
