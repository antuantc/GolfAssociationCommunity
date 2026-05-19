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
    public class CreateAssociationModel : PageModel
    {
        private readonly IAssociationService _associationService;
        private readonly ILogger<CreateAssociationModel> _logger;

        public CreateAssociationModel(
            IAssociationService associationService,
            ILogger<CreateAssociationModel> logger)
        {
            _associationService = associationService;
            _logger = logger;
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

            [StringLength(300)]
            public string? Website { get; set; }

            [StringLength(300)]
            public string? LogoUrl { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Input.Website = NormalizeOptionalUrl(Input.Website, nameof(Input.Website));
            Input.LogoUrl = NormalizeOptionalUrl(Input.LogoUrl, nameof(Input.LogoUrl));

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

            try
            {
                var created = await _associationService.CreateAssociationAsync(association);
                TempData["SuccessMessage"] = $"Association '{created.Name}' created successfully.";
                return RedirectToPage("/Associations/Details", new { id = created.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create association {AssociationName}", association.Name);
                ModelState.AddModelError(string.Empty, "Create association failed. Check the values and try again.");
                return Page();
            }
        }

        private string? NormalizeOptionalUrl(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (!trimmed.Contains("://", StringComparison.Ordinal))
            {
                trimmed = $"https://{trimmed}";
            }

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out _))
            {
                ModelState.AddModelError(fieldName, "Enter a valid URL.");
            }

            return trimmed;
        }
    }
}
