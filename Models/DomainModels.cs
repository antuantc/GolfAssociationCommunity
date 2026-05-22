using Microsoft.AspNetCore.Identity;

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
        Stableford,
        BestBall,
        Scramble,
        Fourball
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
        public ICollection<ApplicationUser> Members { get; set; } = new List<ApplicationUser>();
        public ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
        public ICollection<SponsorshipPackage> SponsorshipPackages { get; set; } = new List<SponsorshipPackage>();
        public ICollection<SponsorshipPayment> SponsorshipPayments { get; set; } = new List<SponsorshipPayment>();
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
    }

    public class Registration
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public Tournament? Tournament { get; set; }
        public string? PlayerId { get; set; }
        public ApplicationUser? Player { get; set; }
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
        public string BillingAddressLine1 { get; set; } = string.Empty;
        public string BillingCity { get; set; } = string.Empty;
        public string BillingState { get; set; } = string.Empty;
        public string BillingZipCode { get; set; } = string.Empty;
        public string BillingCountry { get; set; } = string.Empty;
        public DateTime? PaymentDate { get; set; }
        public string? WithdrawalReason { get; set; }
        public DateTime? WithdrawalDate { get; set; }
    }

    public class PlayerScore
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public Tournament? Tournament { get; set; }
        public string PlayerId { get; set; } = string.Empty;
        public ApplicationUser? Player { get; set; }
        public int Round { get; set; }
        public int HoleNumber { get; set; }
        public int Score { get; set; }
        public int HolePar { get; set; }
        public int HandicapStrokes { get; set; }
        public int StablefordPoints { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class Leaderboard
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public Tournament? Tournament { get; set; }
        public string PlayerId { get; set; } = string.Empty;
        public ApplicationUser? Player { get; set; }
        public int Position { get; set; }
        public int TotalScore { get; set; }
        public int StablefordPoints { get; set; }
        public int ScoreDifferential { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AdminAuditEvent
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Actor { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime AtUtc { get; set; }
    }
}
