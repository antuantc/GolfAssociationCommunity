using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity.UI.Services;
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
        private readonly IEmailSender _emailSender;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(
            ITournamentService tournamentService,
            IRegistrationService registrationService,
            ILeaderboardService leaderboardService,
            IAuthorizeNetPaymentService authorizeNetPaymentService,
            IEmailSender emailSender,
            ILogger<DetailsModel> logger)
        {
            _tournamentService = tournamentService;
            _registrationService = registrationService;
            _leaderboardService = leaderboardService;
            _authorizeNetPaymentService = authorizeNetPaymentService;
            _emailSender = emailSender;
            _logger = logger;
        }

        public Tournament? Tournament { get; set; }
        public int? AssociationId { get; set; }
        public int RegistrationCount { get; set; }
        public bool CanRegister { get; set; }
        public List<Leaderboard> Leaderboard { get; set; } = new();
        public List<TournamentFlight> TournamentFlights { get; set; } = new();

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

            [Required(ErrorMessage = "Handicap is required.")]
            [Range(-10, 60, ErrorMessage = "Handicap must be between -10 and 60.")]
            public decimal? Handicap { get; set; }

            public int? FlightId { get; set; }

            public bool IncludePracticeRound { get; set; }

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
                    ViewData["PublicThemeKey"] = BrandingThemes.Normalize(Tournament.GolfAssociation.ThemeKey);
                    ViewData["PublicAssociationLogoUrl"] = Tournament.GolfAssociation.LogoUrl;

                    var nextTmmt = (await _tournamentService.GetUpcomingTournamentsAsync(Tournament.GolfAssociationId)).FirstOrDefault();
                    if (nextTmmt != null)
                    {
                        ViewData["NextTournamentName"] = nextTmmt.Name;
                        ViewData["NextTournamentDate"] = nextTmmt.StartDate.ToString("MMMM d, yyyy");
                        ViewData["NextTournamentCourse"] = nextTmmt.GolfCourse;
                        ViewData["NextTournamentLocation"] = nextTmmt.Location;
                        ViewData["NextTournamentId"] = nextTmmt.Id;
                    }
                }
            }

            RegistrationCount = await _registrationService.GetRegistrationCountAsync(id, RegistrationStatus.Registered);
            Leaderboard = (await _leaderboardService.GetTournamentLeaderboardAsync(id)).ToList();
            TournamentFlights = Tournament.Flights.OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name).ToList();
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
                    ViewData["PublicThemeKey"] = BrandingThemes.Normalize(tournament.GolfAssociation.ThemeKey);
                    ViewData["PublicAssociationLogoUrl"] = tournament.GolfAssociation.LogoUrl;
                }
                RegistrationCount = await _registrationService.GetRegistrationCountAsync(tournamentId, RegistrationStatus.Registered);
                Leaderboard = (await _leaderboardService.GetTournamentLeaderboardAsync(tournamentId)).ToList();
                TournamentFlights = tournament.Flights.OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name).ToList();
                CanRegister = true;
                Input.CardNumber = string.Empty;
                Input.Cvv = string.Empty;
                return Page();
            }

            // Validate flight selection when tournament has flights
            TournamentFlight? selectedFlight = null;
            var tournamentFlights = tournament.Flights.OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name).ToList();
            if (tournamentFlights.Count > 0)
            {
                if (!Input.FlightId.HasValue)
                {
                    ModelState.AddModelError(string.Empty, "Please select a flight to complete your registration.");
                    Tournament = tournament;
                    AssociationId = associationId;
                    if (associationId.HasValue && tournament.GolfAssociation != null)
                    {
                        ViewData["PublicAssociationId"] = tournament.GolfAssociation.Id;
                        ViewData["PublicAssociationName"] = tournament.GolfAssociation.Name;
                        ViewData["PublicThemeKey"] = BrandingThemes.Normalize(tournament.GolfAssociation.ThemeKey);
                        ViewData["PublicAssociationLogoUrl"] = tournament.GolfAssociation.LogoUrl;
                    }
                    RegistrationCount = await _registrationService.GetRegistrationCountAsync(tournamentId, RegistrationStatus.Registered);
                    Leaderboard = (await _leaderboardService.GetTournamentLeaderboardAsync(tournamentId)).ToList();
                    TournamentFlights = tournamentFlights;
                    CanRegister = true;
                    Input.CardNumber = string.Empty;
                    Input.Cvv = string.Empty;
                    return Page();
                }
                selectedFlight = tournamentFlights.FirstOrDefault(f => f.Id == Input.FlightId.Value);
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
                    ViewData["PublicThemeKey"] = BrandingThemes.Normalize(tournament.GolfAssociation.ThemeKey);
                    ViewData["PublicAssociationLogoUrl"] = tournament.GolfAssociation.LogoUrl;
                }
                RegistrationCount = await _registrationService.GetRegistrationCountAsync(tournamentId, RegistrationStatus.Registered);
                Leaderboard = (await _leaderboardService.GetTournamentLeaderboardAsync(tournamentId)).ToList();
                TournamentFlights = tournamentFlights;
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

            var practiceRoundFee = (Input.IncludePracticeRound && tournament.HasPracticeRound)
                ? tournament.PracticeRoundFee : 0;
            var totalFee = tournament.EntryFee + practiceRoundFee;
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
                tournament.GolfAssociationId,
                totalFee,
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
                GuestName = Input.GuestName.Trim(),
                GuestEmail = Input.GuestEmail.Trim(),
                Handicap = Input.Handicap,
                RegistrationFee = totalFee,
                Status = RegistrationStatus.Registered,
                PaymentConfirmed = true,
                PaymentDate = DateTime.UtcNow,
                AuthorizeNetTransactionId = paymentResult.TransactionId,
                CardLast4 = sanitizedCardNumber.Length >= 4 ? sanitizedCardNumber[^4..] : null,
                BillingAddressLine1 = paymentBillingAddress.AddressLine1,
                BillingCity = paymentBillingAddress.City,
                BillingState = paymentBillingAddress.State,
                BillingZipCode = paymentBillingAddress.ZipCode,
                BillingCountry = paymentBillingAddress.Country,
                TournamentFlightId = selectedFlight?.Id,
                Flight = selectedFlight?.Name,
                IncludesPracticeRound = practiceRoundFee > 0
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

            string? playerEmailError = null;
            try
            {
                await SendRegistrationConfirmationEmailAsync(tournament, registration, successTransactionReference);
            }
            catch (Exception ex)
            {
                playerEmailError = ex.Message;
                _logger.LogError(ex, "Failed to send registration confirmation email to {Email}", registration.GuestEmail);
            }

            try
            {
                await SendAdminRegistrationNotificationAsync(tournament, registration, successTransactionReference, playerEmailError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send admin registration notification for tournament {TournamentId}", tournament.Id);
            }

            return RedirectToPage(new { id = tournamentId, associationId });
        }

        private async Task SendRegistrationConfirmationEmailAsync(Tournament tournament, Registration registration, string transactionReference)
        {
            var tournamentDate = tournament.StartDate.ToString("dddd, MMMM d, yyyy");
            var location = string.IsNullOrWhiteSpace(tournament.Location) ? "TBD" : tournament.Location;
            var golfCourse = string.IsNullOrWhiteSpace(tournament.GolfCourse) ? string.Empty : $" &mdash; {tournament.GolfCourse}";
            var flightLine = string.IsNullOrWhiteSpace(registration.Flight)
                ? string.Empty
                : $"<tr><td style='padding:4px 0;color:#555;'>Flight</td><td style='padding:4px 0;font-weight:600;'>{registration.Flight}</td></tr>";
            var practiceRoundLine = registration.IncludesPracticeRound
                ? $"<tr><td style='padding:4px 0;color:#555;'>Practice Round</td><td style='padding:4px 0;font-weight:600;'>Included</td></tr>"
                : string.Empty;

            var body = $"""
                <!DOCTYPE html>
                <html lang="en">
                <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
                <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,sans-serif;">
                  <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f4;padding:32px 0;">
                    <tr><td align="center">
                      <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;max-width:600px;width:100%;">
                        <tr><td style="background:#1a6b3a;padding:28px 32px;">
                          <h1 style="margin:0;color:#ffffff;font-size:22px;">Registration Confirmed</h1>
                        </td></tr>
                        <tr><td style="padding:32px;">
                          <p style="margin:0 0 8px;font-size:16px;color:#222;">Hi {registration.GuestName},</p>
                          <p style="margin:0 0 24px;font-size:15px;color:#444;">Your registration for <strong>{tournament.Name}</strong> has been confirmed and payment has been received.</p>
                          <table cellpadding="0" cellspacing="0" style="width:100%;border-top:1px solid #e0e0e0;border-bottom:1px solid #e0e0e0;margin-bottom:24px;">
                            <tr><td style="padding:4px 0;color:#555;">Tournament</td><td style="padding:4px 0;font-weight:600;">{tournament.Name}</td></tr>
                            <tr><td style="padding:4px 0;color:#555;">Date</td><td style="padding:4px 0;font-weight:600;">{tournamentDate}</td></tr>
                            <tr><td style="padding:4px 0;color:#555;">Location</td><td style="padding:4px 0;font-weight:600;">{location}{golfCourse}</td></tr>
                            {flightLine}
                            {practiceRoundLine}
                            <tr><td style="padding:4px 0;color:#555;">Entry Fee Paid</td><td style="padding:4px 0;font-weight:600;">{registration.RegistrationFee:C}</td></tr>
                            <tr><td style="padding:4px 0;color:#555;">Transaction</td><td style="padding:4px 0;font-weight:600;">{transactionReference}</td></tr>
                          </table>
                          <p style="margin:0;font-size:13px;color:#888;">Please keep this email as your receipt. If you have any questions, contact the association directly.</p>
                        </td></tr>
                      </table>
                    </td></tr>
                  </table>
                </body>
                </html>
                """;

            await _emailSender.SendEmailAsync(
                registration.GuestEmail,
                $"Registration Confirmed \u2014 {tournament.Name}",
                body);
        }

        private async Task SendAdminRegistrationNotificationAsync(
            Tournament tournament,
            Registration registration,
            string transactionReference,
            string? playerEmailError)
        {
            var adminEmail = tournament.GolfAssociation?.ContactEmail;
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                _logger.LogWarning(
                    "No ContactEmail configured for association {AssociationId}; skipping admin registration notification.",
                    tournament.GolfAssociationId);
                return;
            }

            var tournamentDate = tournament.StartDate.ToString("dddd, MMMM d, yyyy");
            var location = string.IsNullOrWhiteSpace(tournament.Location) ? "TBD" : tournament.Location;
            var golfCourse = string.IsNullOrWhiteSpace(tournament.GolfCourse) ? string.Empty : $" &mdash; {tournament.GolfCourse}";
            var flightLine = string.IsNullOrWhiteSpace(registration.Flight)
                ? string.Empty
                : $"<tr><td style='padding:4px 0;color:#555;'>Flight</td><td style='padding:4px 0;font-weight:600;'>{registration.Flight}</td></tr>";
            var practiceRoundLine = registration.IncludesPracticeRound
                ? $"<tr><td style='padding:4px 0;color:#555;'>Practice Round</td><td style='padding:4px 0;font-weight:600;'>Included</td></tr>"
                : string.Empty;

            var notificationStatus = playerEmailError is null
                ? "<span style='color:#1a6b3a;font-weight:600;'>&#10003; Sent successfully</span>"
                : $"<span style='color:#c0392b;font-weight:600;'>&#10007; Failed to send</span><br><span style='font-size:12px;color:#888;'>{playerEmailError}</span>";

            var body = $"""
                <!DOCTYPE html>
                <html lang="en">
                <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
                <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,sans-serif;">
                  <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f4;padding:32px 0;">
                    <tr><td align="center">
                      <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;max-width:600px;width:100%;">
                        <tr><td style="background:#1a6b3a;padding:28px 32px;">
                          <h1 style="margin:0;color:#ffffff;font-size:22px;">New Tournament Registration</h1>
                        </td></tr>
                        <tr><td style="padding:32px;">
                          <p style="margin:0 0 24px;font-size:15px;color:#444;">A new registration has been completed for <strong>{tournament.Name}</strong>.</p>
                          <h2 style="margin:0 0 8px;font-size:16px;color:#222;">Registration Details</h2>
                          <table cellpadding="0" cellspacing="0" style="width:100%;border-top:1px solid #e0e0e0;border-bottom:1px solid #e0e0e0;margin-bottom:24px;">
                            <tr><td style="padding:4px 0;color:#555;">Player Name</td><td style="padding:4px 0;font-weight:600;">{registration.GuestName}</td></tr>
                            <tr><td style="padding:4px 0;color:#555;">Player Email</td><td style="padding:4px 0;font-weight:600;">{registration.GuestEmail}</td></tr>
                            <tr><td style="padding:4px 0;color:#555;">Handicap</td><td style="padding:4px 0;font-weight:600;">{registration.Handicap?.ToString() ?? "N/A"}</td></tr>
                            <tr><td style="padding:4px 0;color:#555;">Tournament</td><td style="padding:4px 0;font-weight:600;">{tournament.Name}</td></tr>
                            <tr><td style="padding:4px 0;color:#555;">Date</td><td style="padding:4px 0;font-weight:600;">{tournamentDate}</td></tr>
                            <tr><td style="padding:4px 0;color:#555;">Location</td><td style="padding:4px 0;font-weight:600;">{location}{golfCourse}</td></tr>
                            {flightLine}                            {practiceRoundLine}                            <tr><td style="padding:4px 0;color:#555;">Entry Fee</td><td style="padding:4px 0;font-weight:600;">{registration.RegistrationFee:C}</td></tr>
                            <tr><td style="padding:4px 0;color:#555;">Transaction</td><td style="padding:4px 0;font-weight:600;">{transactionReference}</td></tr>
                            <tr><td style="padding:4px 0;color:#555;">Registered At</td><td style="padding:4px 0;font-weight:600;">{registration.RegistrationDate:yyyy-MM-dd HH:mm} UTC</td></tr>
                          </table>
                          <h2 style="margin:0 0 8px;font-size:16px;color:#222;">Player Notification</h2>
                          <p style="margin:0 0 24px;font-size:14px;color:#444;">{notificationStatus}</p>
                        </td></tr>
                      </table>
                    </td></tr>
                  </table>
                </body>
                </html>
                """;

            await _emailSender.SendEmailAsync(
                adminEmail,
                $"New Registration \u2014 {tournament.Name} ({registration.GuestName})",
                body);
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
