using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Services
{
    public class AuthorizeNetTransactionSummary
    {
        public string SourceType { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public int AssociationId { get; set; }
        public string AssociationName { get; set; } = string.Empty;
        public string SourceLabel { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool PaymentConfirmed { get; set; }
        public DateTime? PaidAtUtc { get; set; }
        public string? CardLast4 { get; set; }
        public decimal? RefundAmount { get; set; }
        public string? RefundTransactionId { get; set; }
        public DateTime? RefundedAtUtc { get; set; }

        public bool IsRefunded => RefundedAtUtc.HasValue;
        public string StatusLabel => IsRefunded ? "Refunded" : PaymentConfirmed ? "Paid" : "Pending";
    }

    public class AuthorizeNetTransactionDetail : AuthorizeNetTransactionSummary
    {
        public string BillingAddressLine1 { get; set; } = string.Empty;
        public string BillingCity { get; set; } = string.Empty;
        public string BillingState { get; set; } = string.Empty;
        public string BillingZipCode { get; set; } = string.Empty;
        public string BillingCountry { get; set; } = string.Empty;
        public string? WithdrawalReason { get; set; }
        public DateTime? WithdrawalDate { get; set; }
        public GatewayTransactionDetails? GatewayDetails { get; set; }
    }

    public interface IAuthorizeNetTransactionService
    {
        Task<List<AuthorizeNetTransactionSummary>> GetTransactionsAsync(int? associationId = null);
        Task<AuthorizeNetTransactionDetail?> GetTransactionAsync(string sourceType, int sourceId, int? associationId = null);
        Task<PaymentResult> RefundTransactionAsync(string sourceType, int sourceId, decimal amount, int? associationId = null);
    }

    public class AuthorizeNetTransactionService : IAuthorizeNetTransactionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizeNetPaymentService _paymentService;

        public AuthorizeNetTransactionService(ApplicationDbContext context, IAuthorizeNetPaymentService paymentService)
        {
            _context = context;
            _paymentService = paymentService;
        }

        public async Task<List<AuthorizeNetTransactionSummary>> GetTransactionsAsync(int? associationId = null)
        {
            var transactions = new List<AuthorizeNetTransactionSummary>();

            var registrationsQuery = _context.Registrations
                .AsNoTracking()
                .Include(registration => registration.AssociationPlayer)
                .Include(registration => registration.Tournament)
                    .ThenInclude(tournament => tournament!.GolfAssociation)
                .Where(registration => !string.IsNullOrWhiteSpace(registration.AuthorizeNetTransactionId));

            if (associationId.HasValue)
            {
                registrationsQuery = registrationsQuery.Where(registration => registration.Tournament != null && registration.Tournament.GolfAssociationId == associationId.Value);
            }

            var registrationRows = await registrationsQuery
                .Select(registration => new AuthorizeNetTransactionSummary
                {
                    SourceType = nameof(Registration),
                    SourceId = registration.Id,
                    AssociationId = registration.Tournament != null ? registration.Tournament.GolfAssociationId : 0,
                    AssociationName = registration.Tournament != null && registration.Tournament.GolfAssociation != null
                        ? registration.Tournament.GolfAssociation.Name
                        : string.Empty,
                    SourceLabel = registration.Tournament != null ? registration.Tournament.Name : "Registration",
                    TransactionId = registration.AuthorizeNetTransactionId ?? string.Empty,
                    CustomerName = registration.AssociationPlayer != null
                        ? registration.AssociationPlayer.DisplayName
                        : (!string.IsNullOrWhiteSpace(registration.GuestName) ? registration.GuestName : "Unknown Customer"),
                    CustomerEmail = registration.AssociationPlayer != null
                        ? registration.AssociationPlayer.Email
                        : registration.GuestEmail,
                    Amount = registration.RegistrationFee,
                    PaymentConfirmed = registration.PaymentConfirmed,
                    PaidAtUtc = registration.PaymentDate ?? registration.RegistrationDate,
                    CardLast4 = registration.CardLast4,
                    RefundAmount = registration.RefundAmount,
                    RefundTransactionId = registration.RefundTransactionId,
                    RefundedAtUtc = registration.RefundedAtUtc
                })
                .ToListAsync();

            transactions.AddRange(registrationRows);

            var sponsorshipsQuery = _context.SponsorshipPayments
                .AsNoTracking()
                .Include(payment => payment.GolfAssociation)
                .Where(payment => !string.IsNullOrWhiteSpace(payment.AuthorizeNetTransactionId));

            if (associationId.HasValue)
            {
                sponsorshipsQuery = sponsorshipsQuery.Where(payment => payment.GolfAssociationId == associationId.Value);
            }

            var sponsorshipRows = await sponsorshipsQuery
                .Select(payment => new AuthorizeNetTransactionSummary
                {
                    SourceType = nameof(SponsorshipPayment),
                    SourceId = payment.Id,
                    AssociationId = payment.GolfAssociationId,
                    AssociationName = payment.GolfAssociation != null ? payment.GolfAssociation.Name : string.Empty,
                    SourceLabel = payment.PackageName,
                    TransactionId = payment.AuthorizeNetTransactionId ?? string.Empty,
                    CustomerName = payment.SponsorName,
                    CustomerEmail = payment.SponsorEmail,
                    Amount = payment.AmountPaid,
                    PaymentConfirmed = payment.PaymentConfirmed,
                    PaidAtUtc = payment.PaidAtUtc,
                    CardLast4 = payment.CardLast4,
                    RefundAmount = payment.RefundAmount,
                    RefundTransactionId = payment.RefundTransactionId,
                    RefundedAtUtc = payment.RefundedAtUtc
                })
                .ToListAsync();

            transactions.AddRange(sponsorshipRows);

            return transactions
                .OrderByDescending(transaction => transaction.PaidAtUtc ?? DateTime.MinValue)
                .ThenByDescending(transaction => transaction.SourceType)
                .ThenByDescending(transaction => transaction.SourceId)
                .ToList();
        }

        public async Task<AuthorizeNetTransactionDetail?> GetTransactionAsync(string sourceType, int sourceId, int? associationId = null)
        {
            if (string.Equals(sourceType, nameof(Registration), StringComparison.OrdinalIgnoreCase))
            {
                var registration = await _context.Registrations
                    .AsNoTracking()
                    .Include(item => item.AssociationPlayer)
                    .Include(item => item.Tournament)
                        .ThenInclude(tournament => tournament!.GolfAssociation)
                    .FirstOrDefaultAsync(item => item.Id == sourceId);

                if (registration is null || string.IsNullOrWhiteSpace(registration.AuthorizeNetTransactionId))
                {
                    return null;
                }

                if (associationId.HasValue && (registration.Tournament == null || registration.Tournament.GolfAssociationId != associationId.Value))
                {
                    return null;
                }

                var detail = CreateRegistrationDetail(registration);
                detail.GatewayDetails = await _paymentService.GetTransactionDetailsAsync(detail.AssociationId, detail.TransactionId);
                return detail;
            }

            if (string.Equals(sourceType, nameof(SponsorshipPayment), StringComparison.OrdinalIgnoreCase))
            {
                var sponsorship = await _context.SponsorshipPayments
                    .AsNoTracking()
                    .Include(item => item.GolfAssociation)
                    .FirstOrDefaultAsync(item => item.Id == sourceId);

                if (sponsorship is null || string.IsNullOrWhiteSpace(sponsorship.AuthorizeNetTransactionId))
                {
                    return null;
                }

                if (associationId.HasValue && sponsorship.GolfAssociationId != associationId.Value)
                {
                    return null;
                }

                var detail = CreateSponsorshipDetail(sponsorship);
                detail.GatewayDetails = await _paymentService.GetTransactionDetailsAsync(detail.AssociationId, detail.TransactionId);
                return detail;
            }

            return null;
        }

        public async Task<PaymentResult> RefundTransactionAsync(string sourceType, int sourceId, decimal amount, int? associationId = null)
        {
            if (amount <= 0)
            {
                return new PaymentResult
                {
                    Succeeded = false,
                    ErrorMessage = "Refund amount must be greater than zero."
                };
            }

            if (string.Equals(sourceType, nameof(Registration), StringComparison.OrdinalIgnoreCase))
            {
                var registration = await _context.Registrations
                    .Include(item => item.Tournament)
                    .FirstOrDefaultAsync(item => item.Id == sourceId);

                if (registration is null || string.IsNullOrWhiteSpace(registration.AuthorizeNetTransactionId))
                {
                    return new PaymentResult { Succeeded = false, ErrorMessage = "Registration payment was not found." };
                }

                if (associationId.HasValue && (registration.Tournament == null || registration.Tournament.GolfAssociationId != associationId.Value))
                {
                    return new PaymentResult { Succeeded = false, ErrorMessage = "Registration is outside the current association." };
                }

                if (registration.RefundedAtUtc.HasValue)
                {
                    return new PaymentResult { Succeeded = false, ErrorMessage = "This registration has already been refunded." };
                }

                if (amount > registration.RegistrationFee)
                {
                    return new PaymentResult { Succeeded = false, ErrorMessage = "Refund amount cannot exceed the original transaction amount." };
                }

                var refundResult = await _paymentService.RefundTransactionAsync(
                    registration.Tournament?.GolfAssociationId ?? associationId ?? 0,
                    registration.AuthorizeNetTransactionId,
                    amount,
                    registration.CardLast4 ?? string.Empty);

                if (!refundResult.Succeeded)
                {
                    return refundResult;
                }

                registration.RefundAmount = amount;
                registration.RefundTransactionId = refundResult.TransactionId;
                registration.RefundedAtUtc = DateTime.UtcNow;
                registration.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return refundResult;
            }

            if (string.Equals(sourceType, nameof(SponsorshipPayment), StringComparison.OrdinalIgnoreCase))
            {
                var sponsorship = await _context.SponsorshipPayments
                    .FirstOrDefaultAsync(item => item.Id == sourceId);

                if (sponsorship is null || string.IsNullOrWhiteSpace(sponsorship.AuthorizeNetTransactionId))
                {
                    return new PaymentResult { Succeeded = false, ErrorMessage = "Sponsorship payment was not found." };
                }

                if (associationId.HasValue && sponsorship.GolfAssociationId != associationId.Value)
                {
                    return new PaymentResult { Succeeded = false, ErrorMessage = "Sponsorship payment is outside the current association." };
                }

                if (sponsorship.RefundedAtUtc.HasValue)
                {
                    return new PaymentResult { Succeeded = false, ErrorMessage = "This sponsorship has already been refunded." };
                }

                if (amount > sponsorship.AmountPaid)
                {
                    return new PaymentResult { Succeeded = false, ErrorMessage = "Refund amount cannot exceed the original transaction amount." };
                }

                var refundResult = await _paymentService.RefundTransactionAsync(
                    sponsorship.GolfAssociationId,
                    sponsorship.AuthorizeNetTransactionId,
                    amount,
                    sponsorship.CardLast4 ?? string.Empty);

                if (!refundResult.Succeeded)
                {
                    return refundResult;
                }

                sponsorship.RefundAmount = amount;
                sponsorship.RefundTransactionId = refundResult.TransactionId;
                sponsorship.RefundedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return refundResult;
            }

            return new PaymentResult
            {
                Succeeded = false,
                ErrorMessage = "Unknown transaction source."
            };
        }

        private static AuthorizeNetTransactionDetail CreateRegistrationDetail(Registration registration)
        {
            return new AuthorizeNetTransactionDetail
            {
                SourceType = nameof(Registration),
                SourceId = registration.Id,
                AssociationId = registration.Tournament?.GolfAssociationId ?? 0,
                AssociationName = registration.Tournament?.GolfAssociation?.Name ?? string.Empty,
                SourceLabel = registration.Tournament?.Name ?? "Registration",
                TransactionId = registration.AuthorizeNetTransactionId ?? string.Empty,
                CustomerName = registration.AssociationPlayer != null ? registration.AssociationPlayer.DisplayName : registration.GuestName,
                CustomerEmail = registration.AssociationPlayer != null ? registration.AssociationPlayer.Email : registration.GuestEmail,
                Amount = registration.RegistrationFee,
                PaymentConfirmed = registration.PaymentConfirmed,
                PaidAtUtc = registration.PaymentDate ?? registration.RegistrationDate,
                CardLast4 = registration.CardLast4,
                RefundAmount = registration.RefundAmount,
                RefundTransactionId = registration.RefundTransactionId,
                RefundedAtUtc = registration.RefundedAtUtc,
                BillingAddressLine1 = registration.BillingAddressLine1,
                BillingCity = registration.BillingCity,
                BillingState = registration.BillingState,
                BillingZipCode = registration.BillingZipCode,
                BillingCountry = registration.BillingCountry,
                WithdrawalReason = registration.WithdrawalReason,
                WithdrawalDate = registration.WithdrawalDate
            };
        }

        private static AuthorizeNetTransactionDetail CreateSponsorshipDetail(SponsorshipPayment sponsorship)
        {
            return new AuthorizeNetTransactionDetail
            {
                SourceType = nameof(SponsorshipPayment),
                SourceId = sponsorship.Id,
                AssociationId = sponsorship.GolfAssociationId,
                AssociationName = sponsorship.GolfAssociation?.Name ?? string.Empty,
                SourceLabel = sponsorship.PackageName,
                TransactionId = sponsorship.AuthorizeNetTransactionId ?? string.Empty,
                CustomerName = sponsorship.SponsorName,
                CustomerEmail = sponsorship.SponsorEmail,
                Amount = sponsorship.AmountPaid,
                PaymentConfirmed = sponsorship.PaymentConfirmed,
                PaidAtUtc = sponsorship.PaidAtUtc,
                CardLast4 = sponsorship.CardLast4,
                RefundAmount = sponsorship.RefundAmount,
                RefundTransactionId = sponsorship.RefundTransactionId,
                RefundedAtUtc = sponsorship.RefundedAtUtc,
                BillingAddressLine1 = sponsorship.BillingAddressLine1,
                BillingCity = sponsorship.BillingCity,
                BillingState = sponsorship.BillingState,
                BillingZipCode = sponsorship.BillingZipCode,
                BillingCountry = sponsorship.BillingCountry
            };
        }
    }
}