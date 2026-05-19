using System.ComponentModel.DataAnnotations;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class CreateAssociationModel : PageModel
    {
        private readonly IAssociationService _associationService;

        public CreateAssociationModel(IAssociationService associationService)
        {
            _associationService = associationService;
        }

        [BindProperty]
        public CreateAssociationInput Input { get; set; } = new();

        public class CreateAssociationInput
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
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var association = new GolfAssociation
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
                LogoUrl = Input.LogoUrl
            };

            var created = await _associationService.CreateAssociationAsync(association);
            TempData["SuccessMessage"] = $"Association '{created.Name}' created successfully.";
            return RedirectToPage("/Associations/Details", new { id = created.Id });
        }
    }
}
