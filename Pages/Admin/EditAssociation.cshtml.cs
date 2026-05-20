using System.ComponentModel.DataAnnotations;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace GolfAssociationCommunity.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class EditAssociationModel : PageModel
    {
        private readonly IAssociationService _associationService;
        private readonly IAdminAuditService _adminAuditService;

        public EditAssociationModel(IAssociationService associationService, IAdminAuditService adminAuditService)
        {
            _associationService = associationService;
            _adminAuditService = adminAuditService;
        }

        [BindProperty]
        public EditAssociationInput Input { get; set; } = new();

        public int AssociationId { get; private set; }

        public class EditAssociationInput
        {
            [Required]
            [StringLength(120)]
            public string Name { get; set; } = string.Empty;

            [StringLength(1000)]
            public string? Description { get; set; }

            [EmailAddress]
            [StringLength(256)]
            public string? ContactEmail { get; set; }

            [StringLength(50)]
            public string? ContactPhone { get; set; }

            [StringLength(150)]
            public string? Street { get; set; }

            [StringLength(100)]
            public string? City { get; set; }

            [StringLength(100)]
            public string? State { get; set; }

            [StringLength(20)]
            public string? ZipCode { get; set; }

            [StringLength(100)]
            public string? Country { get; set; }

            [Url]
            [StringLength(300)]
            public string? Website { get; set; }

            [Url]
            [StringLength(300)]
            public string? LogoUrl { get; set; }

            [StringLength(450)]
            public string? AdminUserId { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var association = await _associationService.GetAssociationByIdAsync(id);
            if (association is null)
            {
                return NotFound();
            }

            AssociationId = association.Id;
            Input = new EditAssociationInput
            {
                Name = association.Name,
                Description = association.Description,
                ContactEmail = association.ContactEmail,
                ContactPhone = association.ContactPhone,
                Street = association.Street,
                City = association.City,
                State = association.State,
                ZipCode = association.ZipCode,
                Country = association.Country,
                Website = association.Website,
                LogoUrl = association.LogoUrl,
                AdminUserId = association.AdminUserId
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            if (!ModelState.IsValid)
            {
                AssociationId = id;
                return Page();
            }

            var updatedAssociation = new GolfAssociation
            {
                Name = Input.Name.Trim(),
                Description = Input.Description,
                ContactEmail = Input.ContactEmail,
                ContactPhone = Input.ContactPhone,
                Street = Input.Street,
                City = Input.City,
                State = Input.State,
                ZipCode = Input.ZipCode,
                Country = Input.Country,
                Website = Input.Website,
                LogoUrl = Input.LogoUrl,
                AdminUserId = Input.AdminUserId
            };

            var result = await _associationService.UpdateAssociationAsync(id, updatedAssociation);
            if (result is null)
            {
                await _adminAuditService.WriteAsync("Failed to update association", User?.Identity?.Name ?? "anonymous", new Dictionary<string, string?>
                {
                    ["AssociationId"] = id.ToString()
                });
                return NotFound();
            }

            await _adminAuditService.WriteAsync("Updated association", User?.Identity?.Name ?? "anonymous", new Dictionary<string, string?>
            {
                ["AssociationId"] = id.ToString(),
                ["AssociationName"] = result.Name
            });

            TempData["SuccessMessage"] = "Association updated successfully.";
            return RedirectToPage("/Admin/Associations");
        }
    }
}
