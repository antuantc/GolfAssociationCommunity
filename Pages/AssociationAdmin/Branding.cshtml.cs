using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class BrandingModel : AssociationAdminPageModel
    {
        public BrandingModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
            : base(userManager, context)
        {
        }

        [BindProperty]
        public string SelectedThemeKey { get; set; } = BrandingThemes.DefaultKey;

        [BindProperty]
        [StringLength(128)]
        public string? AuthorizeNetApiLoginId { get; set; }

        [BindProperty]
        [StringLength(128)]
        public string? AuthorizeNetTransactionKey { get; set; }

        [BindProperty]
        public bool AuthorizeNetUseSandbox { get; set; } = true;

        public bool HasStoredAuthorizeNetTransactionKey { get; private set; }

        public IReadOnlyList<BrandingThemeOption> ThemeOptions => BrandingThemes.Options;

        public async Task<IActionResult> OnGetAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            SelectedThemeKey = BrandingThemes.Normalize(CurrentAssociation.ThemeKey);
            AuthorizeNetApiLoginId = CurrentAssociation.AuthorizeNetApiLoginId;
            AuthorizeNetUseSandbox = CurrentAssociation.AuthorizeNetUseSandbox ?? true;
            HasStoredAuthorizeNetTransactionKey = !string.IsNullOrWhiteSpace(CurrentAssociation.AuthorizeNetTransactionKey);
            return Page();
        }

        public async Task<IActionResult> OnPostThemeAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            AuthorizeNetApiLoginId = CurrentAssociation.AuthorizeNetApiLoginId;
            AuthorizeNetUseSandbox = CurrentAssociation.AuthorizeNetUseSandbox ?? true;
            HasStoredAuthorizeNetTransactionKey = !string.IsNullOrWhiteSpace(CurrentAssociation.AuthorizeNetTransactionKey);

            if (!BrandingThemes.IsValid(SelectedThemeKey))
            {
                ModelState.AddModelError(nameof(SelectedThemeKey), "Select one of the available branding themes.");
                SelectedThemeKey = BrandingThemes.Normalize(CurrentAssociation.ThemeKey);
                return Page();
            }

            CurrentAssociation.ThemeKey = BrandingThemes.Normalize(SelectedThemeKey);
            CurrentAssociation.UpdatedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Branding theme updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostPaymentAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            // Payment form does not edit theme; ignore implicit non-nullable validation for theme key.
            ModelState.Remove(nameof(SelectedThemeKey));
            SelectedThemeKey = BrandingThemes.Normalize(CurrentAssociation.ThemeKey);
            HasStoredAuthorizeNetTransactionKey = !string.IsNullOrWhiteSpace(CurrentAssociation.AuthorizeNetTransactionKey);

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var normalizedApiLoginId = string.IsNullOrWhiteSpace(AuthorizeNetApiLoginId)
                ? null
                : AuthorizeNetApiLoginId.Trim();

            var normalizedInputTransactionKey = string.IsNullOrWhiteSpace(AuthorizeNetTransactionKey)
                ? null
                : AuthorizeNetTransactionKey.Trim();

            if (normalizedApiLoginId == null && normalizedInputTransactionKey != null)
            {
                ModelState.AddModelError(nameof(AuthorizeNetApiLoginId), "API Login ID is required when setting a transaction key.");
                return Page();
            }

            if (normalizedApiLoginId != null &&
                normalizedInputTransactionKey == null &&
                string.IsNullOrWhiteSpace(CurrentAssociation.AuthorizeNetTransactionKey))
            {
                ModelState.AddModelError(nameof(AuthorizeNetTransactionKey), "Transaction Key is required when setting an API Login ID for the first time.");
                return Page();
            }

            if (normalizedApiLoginId == null)
            {
                CurrentAssociation.AuthorizeNetApiLoginId = null;
                CurrentAssociation.AuthorizeNetTransactionKey = null;
            }
            else
            {
                CurrentAssociation.AuthorizeNetApiLoginId = normalizedApiLoginId;
                if (normalizedInputTransactionKey != null)
                {
                    CurrentAssociation.AuthorizeNetTransactionKey = normalizedInputTransactionKey;
                }
            }

            CurrentAssociation.AuthorizeNetUseSandbox = AuthorizeNetUseSandbox;
            CurrentAssociation.UpdatedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Authorize.Net settings updated.";
            return RedirectToPage();
        }
    }
}
