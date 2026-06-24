using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace GolfAssociationCommunity.Models
{
    public enum RegistrationStatus
    {
        Pending,
        Registered,
        Cancelled,
        Withdrew
    }

    public enum TournamentStatus
    {
        Scheduled,
        Ongoing,
        Completed,
        Cancelled
    }

    public enum TournamentFormat
    {
        Stroke,
        BestBall,
        Scramble,
        Fourball
    }

    public enum MediaType
    {
        Photo,
        Video
    }

    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int? GolfAssociationId { get; set; }
        public GolfAssociation? GolfAssociation { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool RequirePasswordChange { get; set; }
    }

    public class GolfAssociation
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Country { get; set; }
        public string ThemeKey { get; set; } = BrandingThemes.DefaultKey;
        public string? AuthorizeNetApiLoginId { get; set; }
        public string? AuthorizeNetTransactionKey { get; set; }
        public bool? AuthorizeNetUseSandbox { get; set; }
        public string? Website { get; set; }
        public string? LogoUrl { get; set; }
        public string? AdminUserId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        // Homepage branding
        public string? HeroImageUrl { get; set; }
        public string? HeroVideoUrl { get; set; }
        public string? Tagline { get; set; }
        public string? Motto { get; set; }
        public int? EstYear { get; set; }
        // Charity
        public string? CharityName { get; set; }
        public string? CharityDescription { get; set; }
        public string? CharityUrl { get; set; }
        public ICollection<ApplicationUser> Members { get; set; } = new List<ApplicationUser>();
        public ICollection<AssociationPlayer> Players { get; set; } = new List<AssociationPlayer>();
        public ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
        public ICollection<SponsorshipPackage> SponsorshipPackages { get; set; } = new List<SponsorshipPackage>();
        public ICollection<SponsorshipPayment> SponsorshipPayments { get; set; } = new List<SponsorshipPayment>();
        public ICollection<AssociationOfficer> OfficersAndMembers { get; set; } = new List<AssociationOfficer>();
        public ICollection<AssociationMedia> MediaItems { get; set; } = new List<AssociationMedia>();
        public ICollection<AssociationSponsor> Sponsors { get; set; } = new List<AssociationSponsor>();
        public ICollection<AssociationCharity> Charities { get; set; } = new List<AssociationCharity>();
    }

    public class AssociationOfficer
    {
        public int Id { get; set; }
        public int GolfAssociationId { get; set; }
        public GolfAssociation? GolfAssociation { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? PictureUrl { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AssociationPlayer
    {
        public int Id { get; set; }
        public int GolfAssociationId { get; set; }
        public GolfAssociation? GolfAssociation { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal? HandicapIndex { get; set; }
        public bool IsActive { get; set; } = true;
        public string? PhotoUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
        public ICollection<PlayerScore> Scores { get; set; } = new List<PlayerScore>();
        public ICollection<Leaderboard> LeaderboardEntries { get; set; } = new List<Leaderboard>();
    }

    public class SponsorshipPackage
    {
        public int Id { get; set; }
        public int GolfAssociationId { get; set; }
        public GolfAssociation? GolfAssociation { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string Benefits { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ICollection<SponsorshipPayment> SponsorshipPayments { get; set; } = new List<SponsorshipPayment>();
    }

    public class SponsorshipPayment
    {
        public int Id { get; set; }
        public int GolfAssociationId { get; set; }
        public GolfAssociation? GolfAssociation { get; set; }
        public int? SponsorshipPackageId { get; set; }
        public SponsorshipPackage? SponsorshipPackage { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string SponsorName { get; set; } = string.Empty;
        public string SponsorEmail { get; set; } = string.Empty;
        public string? SponsorCompany { get; set; }
        public decimal AmountPaid { get; set; }
        public bool PaymentConfirmed { get; set; }
        public DateTime PaidAtUtc { get; set; }
        public string? AuthorizeNetTransactionId { get; set; }
        public string? CardLast4 { get; set; }
        public decimal? RefundAmount { get; set; }
        public string? RefundTransactionId { get; set; }
        public DateTime? RefundedAtUtc { get; set; }
        public string BillingAddressLine1 { get; set; } = string.Empty;
        public string BillingCity { get; set; } = string.Empty;
        public string BillingState { get; set; } = string.Empty;
        public string BillingZipCode { get; set; } = string.Empty;
        public string BillingCountry { get; set; } = string.Empty;
    }

    public class Tournament
    {
        public int Id { get; set; }
        public int GolfAssociationId { get; set; }
        public GolfAssociation? GolfAssociation { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime? RegistrationDeadline { get; set; }
        public TournamentFormat Format { get; set; } = TournamentFormat.Stroke;
        public string? Location { get; set; }
        public string? GolfCourse { get; set; }
        public decimal EntryFee { get; set; }
        public int MaxPlayers { get; set; }
        public TournamentStatus Status { get; set; } = TournamentStatus.Scheduled;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
        public ICollection<PlayerScore> PlayerScores { get; set; } = new List<PlayerScore>();
        public ICollection<TournamentFlight> Flights { get; set; } = new List<TournamentFlight>();
    }

    public class Registration
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public Tournament? Tournament { get; set; }
        public int? AssociationPlayerId { get; set; }
        public AssociationPlayer? AssociationPlayer { get; set; }
        public string GuestName { get; set; } = string.Empty;
        public string GuestEmail { get; set; } = string.Empty;
        public decimal? Handicap { get; set; }
        public DateTime RegistrationDate { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public decimal RegistrationFee { get; set; }
        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;
        public bool PaymentConfirmed { get; set; }
        public string? AuthorizeNetTransactionId { get; set; }
        public string? CardLast4 { get; set; }
        public decimal? RefundAmount { get; set; }
        public string? RefundTransactionId { get; set; }
        public DateTime? RefundedAtUtc { get; set; }
        public string BillingAddressLine1 { get; set; } = string.Empty;
        public string BillingCity { get; set; } = string.Empty;
        public string BillingState { get; set; } = string.Empty;
        public string BillingZipCode { get; set; } = string.Empty;
        public string BillingCountry { get; set; } = string.Empty;
        public DateTime? PaymentDate { get; set; }
        public string? WithdrawalReason { get; set; }
        public DateTime? WithdrawalDate { get; set; }
        public string? Flight { get; set; }
        public int? TournamentFlightId { get; set; }
        public TournamentFlight? TournamentFlight { get; set; }
    }

    public class PlayerScore
    {
        public const int RoundTotalEntryHoleNumber = 0;
        public const int MaxTiebreakerEntries = 4;

        public int Id { get; set; }
        public int TournamentId { get; set; }
        public Tournament? Tournament { get; set; }
        public int AssociationPlayerId { get; set; }
        public AssociationPlayer? AssociationPlayer { get; set; }
        public int Round { get; set; }
        public int HoleNumber { get; set; }
        public int Score { get; set; }
        public int HolePar { get; set; }
        public int HandicapStrokes { get; set; }
        public int? TiebreakerHoleHandicap { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public bool IsRoundTotalEntry => HoleNumber == RoundTotalEntryHoleNumber;
    }

    public class Leaderboard
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public Tournament? Tournament { get; set; }
        public int AssociationPlayerId { get; set; }
        public AssociationPlayer? AssociationPlayer { get; set; }
        public int Position { get; set; }
        public int TotalScore { get; set; }
        public int ScoreDifferential { get; set; }
        public string? Flight { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>Tiebreaker hole scores (hardest handicap hole first). Not persisted — populated at query time.</summary>
        [NotMapped]
        public List<int> TiebreakerScores { get; set; } = new();
    }

    public class AssociationMedia
    {
        public int Id { get; set; }
        public int GolfAssociationId { get; set; }
        public GolfAssociation? GolfAssociation { get; set; }
        public MediaType MediaType { get; set; } = MediaType.Photo;
        public string Url { get; set; } = string.Empty;
        public string? Caption { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>Applies only to Video items. When true, appends autoplay parameters to the embed URL.</summary>
        public bool AutoPlay { get; set; } = false;
        public DateTime CreatedAt { get; set; }
    }

    public class AssociationSponsor
    {
        public int Id { get; set; }
        public int GolfAssociationId { get; set; }
        public GolfAssociation? GolfAssociation { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? LogoUrl { get; set; }
        public string? Website { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    public class AssociationCharity
    {
        public int Id { get; set; }
        public int GolfAssociationId { get; set; }
        public GolfAssociation? GolfAssociation { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Url { get; set; }
        public string? ImageUrl { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    public class AdminAuditEvent
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Actor { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime AtUtc { get; set; }
    }

    public class TournamentFlight
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public Tournament? Tournament { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
    }
}
