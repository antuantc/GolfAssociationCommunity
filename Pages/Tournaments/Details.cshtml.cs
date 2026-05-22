using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Tournaments
{
    public class DetailsModel : PageModel
    {
        private readonly ITournamentService _tournamentService;
        private readonly IRegistrationService _registrationService;
        private readonly ILeaderboardService _leaderboardService;
        private readonly IAuthorizeNetPaymentService _authorizeNetPaymentService;

        public DetailsModel(
            ITournamentService tournamentService,
            IRegistrationService registrationService,
            ILeaderboardService leaderboardService,
            IAuthorizeNetPaymentService authorizeNetPaymentService)
        {
            _tournamentService = tournamentService;
            _registrationService = registrationService;
            _leaderboardService = leaderboardService;
            _authorizeNetPaymentService = authorizeNetPaymentService;
        }

        public Tournament? Tournament { get; set; }
        public int? AssociationId { get; set; }
        public int RegistrationCount { get; set; }
        public bool CanRegister { get; set; }
        public List<Leaderboard> Leaderboard { get; set; } = new();

        [BindProperty]
        public GuestRegistrationInput Input { get; set; } = new();

        public class GuestRegistrationInput
        {
            [Required]
            [StringLength(120)]
            public string GuestName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            [StringLength(256)]
            public string GuestEmail { get; set; } = string.Empty;

            [Range(-10, 60, ErrorMessage = "Handicap must be between -10 and 60.")]
            public decimal? Handicap { get; set; }

            [Required]
            [CreditCard]
            [StringLength(19)]
            public string CardNumber { get; set; } = string.Empty;

            [Required]
            [StringLength(120)]
            public string CardholderName { get; set; } = string.Empty;

            [Required]
            [RegularExpression("^(0[1-9]|1[0-2])\\s*/\\s*([0-9]{2}|[0-9]{4})$", ErrorMessage = "Use MM/YY or MM/YYYY.")]
            public string CardExpiry { get; set; } = string.Empty;

            [Required]
            [RegularExpression("^[0-9]{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits.")]
            public string Cvv { get; set; } = string.Empty;

            [Required]
            [StringLength(150)]
            public string BillingAddressLine1 { get; set; } = string.Empty;

            [Required]
            [StringLength(100)]
            public string BillingCity { get; set; } = string.Empty;

            [Required]
            [StringLength(100)]
            public string BillingState { get; set; } = string.Empty;

            [Required]
            [StringLength(20)]
            public string BillingZipCode { get; set; } = string.Empty;

            [Required]
            [StringLength(100)]
            public string BillingCountry { get; set; } = "US";
        }

        public async Task<IActionResult> OnGetAsync(int id, int? associationId)
        {
            Tournament = await _tournamentService.GetTournamentByIdAsync(id);
            if (Tournament == null)
            {
                return NotFound();
            }

            if (!associationId.HasValue)
            {
                return RedirectToPage(new { id, associationId = Tournament.GolfAssociationId });
            }

            if (associationId.HasValue)
            {
                if (Tournament.GolfAssociationId != associationId.Value)
                {
                    return NotFound();
                }

                AssociationId = associationId.Value;
                if (Tournament.GolfAssociation != null)
                {
                    ViewData["PublicAssociationId"] = Tournament.GolfAssociation.Id;
                    ViewData["PublicAssociationName"] = Tournament.GolfAssociation.Name;
                }
            }

            RegistrationCount = await _registrationService.GetRegistrationCountAsync(id, RegistrationStatus.Registered);
            Leaderboard = (await _leaderboardService.GetTournamentLeaderboardAsync(id)).ToList();
            CanRegister = (!Tournament.RegistrationDeadline.HasValue || Tournament.RegistrationDeadline.Value >= DateTime.UtcNow)
                && RegistrationCount < Tournament.MaxPlayers;

            return Page();
        }

        public async Task<IActionResult> OnPostRegisterAsync(int tournamentId, int? associationId)
        {
            var tournament = await _tournamentService.GetTournamentByIdAsync(tournamentId);
            if (tournament == null)
            {
                return NotFound();
            }

            associationId ??= tournament.GolfAssociationId;

            if (associationId.HasValue && tournament.GolfAssociationId != associationId.Value)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                Tournament = tournament;
                AssociationId = associationId;
                if (associationId.HasValue && tournament.GolfAssociation != null)
                {
                    ViewData["PublicAssociationId"] = tournament.GolfAssociation.Id;
                    ViewData["PublicAssociationName"] = tournament.GolfAssociation.Name;
                }
                RegistrationCount = await _registrationService.GetRegistrationCountAsync(tournamentId, RegistrationStatus.Registered);
                Leaderboard = (await _leaderboardService.GetTournamentLeaderboardAsync(tournamentId)).ToList();
                CanRegister = true;
                Input.CardNumber = string.Empty;
                Input.Cvv = string.Empty;
                return Page();
            }

            var expirationDate = BuildAuthorizeNetExpirationDate(Input.CardExpiry);
            if (expirationDate == null)
            {
                ModelState.AddModelError(string.Empty, "Use a valid card expiration date.");

                Tournament = tournament;
                AssociationId = associationId;
                if (associationId.HasValue && tournament.GolfAssociation != null)
                {
                    ViewData["PublicAssociationId"] = tournament.GolfAssociation.Id;
                    ViewData["PublicAssociationName"] = tournament.GolfAssociation.Name;
                }
                RegistrationCount = await _registrationService.GetRegistrationCountAsync(tournamentId, RegistrationStatus.Registered);
                Leaderboard = (await _leaderboardService.GetTournamentLeaderboardAsync(tournamentId)).ToList();
                CanRegister = true;
                Input.CardNumber = string.Empty;
                Input.Cvv = string.Empty;
                return Page();
            }

            if (!await _registrationService.CanGuestRegisterAsync(tournamentId, Input.GuestEmail))
            {
                TempData["ErrorMessage"] = "Registration is not available for this tournament or this email is already registered.";
                return RedirectToPage(new { id = tournamentId, associationId });
            }

            var sanitizedCardNumber = new string(Input.CardNumber.Where(char.IsDigit).ToArray());
            var paymentBillingAddress = new PaymentBillingAddress
            {
                FullName = Input.CardholderName.Trim(),
                AddressLine1 = Input.BillingAddressLine1.Trim(),
                City = Input.BillingCity.Trim(),
                State = Input.BillingState.Trim(),
                ZipCode = Input.BillingZipCode.Trim(),
                Country = Input.BillingCountry.Trim()
            };

            var paymentResult = await _authorizeNetPaymentService.ProcessPaymentAsync(
                tournament.EntryFee,
                sanitizedCardNumber,
                expirationDate,
                Input.Cvv,
                paymentBillingAddress);

            if (!paymentResult.Succeeded)
            {
                TempData["ErrorMessage"] = paymentResult.ErrorMessage ?? "Payment was declined.";
                return RedirectToPage(new { id = tournamentId, associationId });
            }

            var registration = new Registration
            {
                TournamentId = tournamentId,
                PlayerId = null,
                GuestName = Input.GuestName.Trim(),
                GuestEmail = Input.GuestEmail.Trim(),
                Handicap = Input.Handicap,
                RegistrationFee = tournament.EntryFee,
                Status = RegistrationStatus.Registered,
                PaymentConfirmed = true,
                PaymentDate = DateTime.UtcNow,
                AuthorizeNetTransactionId = paymentResult.TransactionId,
                CardLast4 = sanitizedCardNumber.Length >= 4 ? sanitizedCardNumber[^4..] : null,
                BillingAddressLine1 = paymentBillingAddress.AddressLine1,
                BillingCity = paymentBillingAddress.City,
                BillingState = paymentBillingAddress.State,
                BillingZipCode = paymentBillingAddress.ZipCode,
                BillingCountry = paymentBillingAddress.Country
            };

            try
            {
                await _registrationService.CreateRegistrationAsync(registration);
            }
            catch
            {
                var transactionReference = BuildTransactionReference(paymentResult.TransactionId);
                TempData["ErrorMessage"] =
                    $"Payment was approved, but registration could not be finalized. Please contact support with transaction reference {transactionReference}.";
                return RedirectToPage(new { id = tournamentId, associationId });
            }

            var successTransactionReference = BuildTransactionReference(paymentResult.TransactionId);
            TempData["SuccessMessage"] =
                $"Payment approved and registration completed. Transaction reference: {successTransactionReference}.";
            return RedirectToPage(new { id = tournamentId, associationId });
        }

        private static string BuildTransactionReference(string? transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return "unavailable";
            }

            var trimmed = transactionId.Trim();
            if (trimmed.Length <= 4)
            {
                return trimmed;
            }

            return $"****{trimmed[^4..]}";
        }

        private static string? BuildAuthorizeNetExpirationDate(string expiry)
        {
            if (string.IsNullOrWhiteSpace(expiry))
            {
                return null;
            }

            var parts = expiry.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return null;
            }

            if (!int.TryParse(parts[0], out var monthNumber) || monthNumber < 1 || monthNumber > 12)
            {
                return null;
            }

            var trimmedYear = parts[1].Trim();
            int fullYear;
            if (trimmedYear.Length == 2)
            {
                if (!int.TryParse(trimmedYear, out var shortYear))
                {
                    return null;
                }

                fullYear = 2000 + shortYear;
            }
            else if (trimmedYear.Length == 4)
            {
                if (!int.TryParse(trimmedYear, out fullYear))
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            if (fullYear < DateTime.UtcNow.Year - 1)
            {
                return null;
            }

            return $"{fullYear:D4}-{monthNumber:D2}";
        }
    }
}
