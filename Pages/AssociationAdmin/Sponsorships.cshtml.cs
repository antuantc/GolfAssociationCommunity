using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class SponsorshipsModel : AssociationAdminPageModel
    {
        public SponsorshipsModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
            : base(userManager, context)
        {
        }

        public List<SponsorshipPackage> Packages { get; private set; } = new();

        [BindProperty]
        public NewSponsorshipInput Input { get; set; } = new();

        public class NewSponsorshipInput
        {
            [Required]
            [StringLength(120)]
            public string Name { get; set; } = string.Empty;

            [StringLength(500)]
            public string? Description { get; set; }

            [Range(0, 100000000)]
            public decimal Amount { get; set; }

            [Required]
            [StringLength(2000)]
            public string Benefits { get; set; } = string.Empty;

            [Range(0, 1000)]
            public int DisplayOrder { get; set; }

            public bool IsActive { get; set; } = true;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            await LoadPackagesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            if (!ModelState.IsValid)
            {
                await LoadPackagesAsync();
                return Page();
            }

            var package = new SponsorshipPackage
            {
                GolfAssociationId = CurrentAssociation.Id,
                Name = Input.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
                Amount = Input.Amount,
                Benefits = Input.Benefits.Trim(),
                DisplayOrder = Input.DisplayOrder,
                IsActive = Input.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            Context.SponsorshipPackages.Add(package);
            await Context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Sponsorship package created.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleActiveAsync(int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            var package = await Context.SponsorshipPackages
                .FirstOrDefaultAsync(sp => sp.Id == id && sp.GolfAssociationId == CurrentAssociation.Id);

            if (package is null)
            {
                return NotFound();
            }

            package.IsActive = !package.IsActive;
            package.UpdatedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync();

            TempData["SuccessMessage"] = package.IsActive
                ? "Sponsorship package activated."
                : "Sponsorship package deactivated.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            var package = await Context.SponsorshipPackages
                .FirstOrDefaultAsync(sp => sp.Id == id && sp.GolfAssociationId == CurrentAssociation.Id);

            if (package is null)
            {
                return NotFound();
            }

            Context.SponsorshipPackages.Remove(package);
            await Context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Sponsorship package deleted.";
            return RedirectToPage();
        }

        private async Task LoadPackagesAsync()
        {
            var packages = await Context.SponsorshipPackages
                .Where(sp => sp.GolfAssociationId == CurrentAssociation.Id)
                .ToListAsync();

            // SQLite does not support ordering by decimal in SQL translation.
            Packages = packages
                .OrderBy(sp => sp.DisplayOrder)
                .ThenByDescending(sp => sp.Amount)
                .ToList();
        }
    }
}
