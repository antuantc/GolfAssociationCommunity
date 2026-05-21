using System.ComponentModel.DataAnnotations;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace GolfAssociationCommunity.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class CreateAssociationModel : PageModel
    {
        private readonly IAssociationService _associationService;
        private readonly IAdminAuditService _adminAuditService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<CreateAssociationModel> _logger;

        public CreateAssociationModel(
            IAssociationService associationService,
            IAdminAuditService adminAuditService,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<CreateAssociationModel> logger)
        {
            _associationService = associationService;
            _adminAuditService = adminAuditService;
            _userManager = userManager;
            _roleManager = roleManager;
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

            [EmailAddress]
            [StringLength(256)]
            public string? AssociationAdminEmail { get; set; }

            [StringLength(100)]
            public string? AssociationAdminFirstName { get; set; }

            [StringLength(100)]
            public string? AssociationAdminLastName { get; set; }

            [DataType(DataType.Password)]
            [StringLength(100, MinimumLength = 8)]
            public string? AssociationAdminPassword { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Input.Website = NormalizeOptionalUrl(Input.Website, nameof(Input.Website));
            Input.LogoUrl = NormalizeOptionalUrl(Input.LogoUrl, nameof(Input.LogoUrl));

            var wantsAssociationAdmin = !string.IsNullOrWhiteSpace(Input.AssociationAdminEmail)
                || !string.IsNullOrWhiteSpace(Input.AssociationAdminPassword)
                || !string.IsNullOrWhiteSpace(Input.AssociationAdminFirstName)
                || !string.IsNullOrWhiteSpace(Input.AssociationAdminLastName);

            if (wantsAssociationAdmin)
            {
                if (string.IsNullOrWhiteSpace(Input.AssociationAdminEmail))
                {
                    ModelState.AddModelError(nameof(Input.AssociationAdminEmail), "Association admin email is required.");
                }

                if (string.IsNullOrWhiteSpace(Input.AssociationAdminPassword))
                {
                    ModelState.AddModelError(nameof(Input.AssociationAdminPassword), "Association admin password is required.");
                }
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (wantsAssociationAdmin)
            {
                var normalizedAdminEmail = Input.AssociationAdminEmail!.Trim();
                var existingUser = await _userManager.FindByEmailAsync(normalizedAdminEmail);
                if (existingUser != null)
                {
                    ModelState.AddModelError(nameof(Input.AssociationAdminEmail), "Association admin email already exists.");
                    return Page();
                }
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

                if (wantsAssociationAdmin)
                {
                    var adminEmail = Input.AssociationAdminEmail!.Trim();
                    if (!await _roleManager.RoleExistsAsync("AssociationAdmin"))
                    {
                        await _roleManager.CreateAsync(new IdentityRole("AssociationAdmin"));
                    }

                    var associationAdmin = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true,
                        FirstName = string.IsNullOrWhiteSpace(Input.AssociationAdminFirstName) ? null : Input.AssociationAdminFirstName.Trim(),
                        LastName = string.IsNullOrWhiteSpace(Input.AssociationAdminLastName) ? null : Input.AssociationAdminLastName.Trim(),
                        GolfAssociationId = created.Id,
                        UpdatedAt = DateTime.UtcNow,
                        RequirePasswordChange = true
                    };

                    var createAdminResult = await _userManager.CreateAsync(associationAdmin, Input.AssociationAdminPassword!);
                    if (!createAdminResult.Succeeded)
                    {
                        await _associationService.DeleteAssociationAsync(created.Id);

                        foreach (var error in createAdminResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }

                        return Page();
                    }

                    await _userManager.AddToRoleAsync(associationAdmin, "AssociationAdmin");

                    created.AdminUserId = associationAdmin.Id;
                    created.UpdatedAt = DateTime.UtcNow;
                    await _associationService.UpdateAssociationAsync(created.Id, created);
                }

                await _adminAuditService.WriteAsync("Created association", User?.Identity?.Name ?? "anonymous", new Dictionary<string, string?>
                {
                    ["AssociationId"] = created.Id.ToString(),
                    ["AssociationName"] = created.Name,
                    ["AssociationAdminEmail"] = wantsAssociationAdmin ? Input.AssociationAdminEmail : "(none)"
                });
                TempData["SuccessMessage"] = $"Association '{created.Name}' created successfully.";
                return RedirectToPage("/Associations/Details", new { id = created.Id });
            }
            catch (Exception ex)
            {
                await _adminAuditService.WriteAsync("Failed to create association", User?.Identity?.Name ?? "anonymous", new Dictionary<string, string?>
                {
                    ["AssociationName"] = association.Name
                });
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
