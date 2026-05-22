using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class EditSponsorshipModel : AssociationAdminPageModel
    {
        public EditSponsorshipModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
            : base(userManager, context)
        {
        }

        public int SponsorshipId { get; private set; }

        [BindProperty]
        public EditSponsorshipInput Input { get; set; } = new();

        public class EditSponsorshipInput
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

            public bool IsActive { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
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

            SponsorshipId = package.Id;
            Input = new EditSponsorshipInput
            {
                Name = package.Name,
                Description = package.Description,
                Amount = package.Amount,
                Benefits = package.Benefits,
                DisplayOrder = package.DisplayOrder,
                IsActive = package.IsActive
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
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

            if (!ModelState.IsValid)
            {
                SponsorshipId = id;
                return Page();
            }

            package.Name = Input.Name.Trim();
            package.Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();
            package.Amount = Input.Amount;
            package.Benefits = Input.Benefits.Trim();
            package.DisplayOrder = Input.DisplayOrder;
            package.IsActive = Input.IsActive;
            package.UpdatedAt = DateTime.UtcNow;

            await Context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Sponsorship package updated.";
            return RedirectToPage("/AssociationAdmin/Sponsorships");
        }
    }
}
