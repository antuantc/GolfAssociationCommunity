using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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

        public IReadOnlyList<BrandingThemeOption> ThemeOptions => BrandingThemes.Options;

        public async Task<IActionResult> OnGetAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            SelectedThemeKey = BrandingThemes.Normalize(CurrentAssociation.ThemeKey);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

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
    }
}
