using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace GolfAssociationCommunity.Pages.Associations
{
    public class SponsorModel : PageModel
    {
        private readonly IAuthorizeNetPaymentService _authorizeNetPaymentService;
        private readonly ApplicationDbContext _context;

        public SponsorModel(
            IAuthorizeNetPaymentService authorizeNetPaymentService,
            ApplicationDbContext context)
        {
            _authorizeNetPaymentService = authorizeNetPaymentService;
            _context = context;
        }

        public GolfAssociation? Association { get; set; }
        public List<SponsorshipPackage> ActiveSponsorshipPackages { get; private set; } = new();

        [BindProperty]
        public SponsorshipPaymentInput Input { get; set; } = new();

        public class SponsorshipPaymentInput
        {
            [Range(1, int.MaxValue, ErrorMessage = "Select a sponsorship package.")]
            public int SponsorshipPackageId { get; set; }

            [Required]
            [StringLength(120)]
            public string SponsorName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            [StringLength(256)]
            public string SponsorEmail { get; set; } = string.Empty;

            [StringLength(160)]
            public string? SponsorCompany { get; set; }

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

        public async Task<IActionResult> OnGetAsync(int id, int? selectedPackageId)
        {
            if (!await LoadAssociationAsync(id))
            {
                return NotFound();
            }

            Input.BillingCountry = "US";
            if (selectedPackageId.HasValue)
            {
                var selectedPackageExists = ActiveSponsorshipPackages
                    .Any(sp => sp.Id == selectedPackageId.Value);
                if (selectedPackageExists)
                {
                    Input.SponsorshipPackageId = selectedPackageId.Value;
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            if (!await LoadAssociationAsync(id))
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                Input.CardNumber = string.Empty;
                Input.Cvv = string.Empty;
                return Page();
            }

            if (ActiveSponsorshipPackages.Count == 0)
            {
                TempData["ErrorMessage"] = "Sponsorship is not currently available for this association.";
                return RedirectToPage(new { id });
            }

            var package = ActiveSponsorshipPackages
                .FirstOrDefault(sp => sp.Id == Input.SponsorshipPackageId);

            if (package == null)
            {
                ModelState.AddModelError(nameof(Input.SponsorshipPackageId), "Select an active sponsorship package.");
                Input.CardNumber = string.Empty;
                Input.Cvv = string.Empty;
                return Page();
            }

            var expirationDate = BuildAuthorizeNetExpirationDate(Input.CardExpiry);
            if (expirationDate == null)
            {
                ModelState.AddModelError(string.Empty, "Use a valid card expiration date.");
                Input.CardNumber = string.Empty;
                Input.Cvv = string.Empty;
                return Page();
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
                Association!.Id,
                package.Amount,
                sanitizedCardNumber,
                expirationDate,
                Input.Cvv,
                paymentBillingAddress);

            if (!paymentResult.Succeeded)
            {
                TempData["ErrorMessage"] = paymentResult.ErrorMessage ?? "Payment was declined.";
                return RedirectToPage(new { id, selectedPackageId = Input.SponsorshipPackageId });
            }

            var sponsorshipPayment = new SponsorshipPayment
            {
                GolfAssociationId = Association!.Id,
                SponsorshipPackageId = package.Id,
                PackageName = package.Name,
                SponsorName = Input.SponsorName.Trim(),
                SponsorEmail = Input.SponsorEmail.Trim(),
                SponsorCompany = string.IsNullOrWhiteSpace(Input.SponsorCompany) ? null : Input.SponsorCompany.Trim(),
                AmountPaid = package.Amount,
                PaymentConfirmed = true,
                PaidAtUtc = DateTime.UtcNow,
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
                _context.SponsorshipPayments.Add(sponsorshipPayment);
                await _context.SaveChangesAsync();
            }
            catch
            {
                var transactionReference = BuildTransactionReference(paymentResult.TransactionId);
                TempData["ErrorMessage"] =
                    $"Payment was approved, but sponsorship confirmation could not be finalized. Please contact support with transaction reference {transactionReference}.";
                return RedirectToPage(new { id, selectedPackageId = Input.SponsorshipPackageId });
            }

            var successTransactionReference = BuildTransactionReference(paymentResult.TransactionId);
            TempData["SuccessMessage"] =
                $"Thank you for sponsoring at the {package.Name} level. Transaction reference: {successTransactionReference}.";

            return RedirectToPage(new { id, selectedPackageId = Input.SponsorshipPackageId });
        }

        private async Task<bool> LoadAssociationAsync(int id)
        {
            Association = await _context.GolfAssociations
                .Include(ga => ga.Tournaments)
                .Include(ga => ga.SponsorshipPackages)
                .FirstOrDefaultAsync(ga => ga.Id == id && ga.IsActive);
            if (Association == null)
            {
                return false;
            }

            ViewData["PublicAssociationId"] = Association.Id;
            ViewData["PublicAssociationName"] = Association.Name;
            ViewData["PublicThemeKey"] = BrandingThemes.Normalize(Association.ThemeKey);
            ViewData["PublicAssociationLogoUrl"] = Association.LogoUrl;

            var nextTmmt = Association.Tournaments
                .Where(t => t.StartDate >= DateTime.UtcNow && t.Status != TournamentStatus.Cancelled)
                .OrderBy(t => t.StartDate)
                .FirstOrDefault();
            if (nextTmmt != null)
            {
                ViewData["NextTournamentName"] = nextTmmt.Name;
                ViewData["NextTournamentDate"] = nextTmmt.StartDate.ToString("MMMM d, yyyy");
                ViewData["NextTournamentCourse"] = nextTmmt.GolfCourse;
                ViewData["NextTournamentLocation"] = nextTmmt.Location;
                ViewData["NextTournamentId"] = nextTmmt.Id;
            }

            ActiveSponsorshipPackages = Association.SponsorshipPackages
                .Where(sp => sp.IsActive)
                .OrderBy(sp => sp.DisplayOrder)
                .ThenByDescending(sp => sp.Amount)
                .ToList();
            return true;
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
