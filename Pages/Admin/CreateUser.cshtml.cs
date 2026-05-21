using System.ComponentModel.DataAnnotations;
using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class CreateUserModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAdminAuditService _adminAuditService;

        public CreateUserModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAdminAuditService adminAuditService)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _adminAuditService = adminAuditService;
        }

        [BindProperty]
        public CreateUserInput Input { get; set; } = new();

        public List<AssociationOption> Associations { get; private set; } = new();

        public class AssociationOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class CreateUserInput
        {
            [Required]
            [EmailAddress]
            [StringLength(256)]
            public string Email { get; set; } = string.Empty;

            [StringLength(100)]
            public string? FirstName { get; set; }

            [StringLength(100)]
            public string? LastName { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [StringLength(100, MinimumLength = 8)]
            public string Password { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            [Compare(nameof(Password), ErrorMessage = "Password and confirmation do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [Required]
            public string Role { get; set; } = "Member";

            public int? AssociationId { get; set; }
        }

        public async Task OnGetAsync()
        {
            await LoadAssociationsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadAssociationsAsync();

            if (Input.Role == "Admin" && Input.AssociationId.HasValue)
            {
                ModelState.AddModelError(nameof(Input.AssociationId), "Admin users cannot be assigned to an association.");
            }

            if (Input.Role == "AssociationAdmin" && !Input.AssociationId.HasValue)
            {
                ModelState.AddModelError(nameof(Input.AssociationId), "Association Admin users must be assigned to an association.");
            }

            GolfAssociation? association = null;
            if (Input.AssociationId.HasValue)
            {
                association = await _context.GolfAssociations
                    .FirstOrDefaultAsync(a => a.Id == Input.AssociationId.Value && a.IsActive);

                if (association == null)
                {
                    ModelState.AddModelError(nameof(Input.AssociationId), "Selected association was not found.");
                }
            }

            var normalizedEmail = Input.Email.Trim();
            var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
            if (existingUser != null)
            {
                ModelState.AddModelError(nameof(Input.Email), "Email is already used by another account.");
            }

            if (association != null && Input.Role == "AssociationAdmin" && !string.IsNullOrWhiteSpace(association.AdminUserId))
            {
                ModelState.AddModelError(nameof(Input.AssociationId), "The selected association already has an association admin.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = new ApplicationUser
            {
                UserName = normalizedEmail,
                Email = normalizedEmail,
                EmailConfirmed = true,
                FirstName = string.IsNullOrWhiteSpace(Input.FirstName) ? null : Input.FirstName.Trim(),
                LastName = string.IsNullOrWhiteSpace(Input.LastName) ? null : Input.LastName.Trim(),
                GolfAssociationId = Input.Role == "Admin" ? null : Input.AssociationId,
                UpdatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, Input.Password);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return Page();
            }

            if (Input.Role != "Member")
            {
                if (!await _roleManager.RoleExistsAsync(Input.Role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(Input.Role));
                }

                await _userManager.AddToRoleAsync(user, Input.Role);
            }

            if (association != null && Input.Role == "AssociationAdmin")
            {
                association.AdminUserId = user.Id;
                association.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            await _adminAuditService.WriteAsync("Created user", User?.Identity?.Name ?? "anonymous", new Dictionary<string, string?>
            {
                ["TargetUserId"] = user.Id,
                ["TargetEmail"] = user.Email,
                ["Role"] = Input.Role,
                ["AssociationId"] = Input.AssociationId?.ToString()
            });

            TempData["SuccessMessage"] = $"User created: {user.Email}.";
            return RedirectToPage("/Admin/Users");
        }

        private async Task LoadAssociationsAsync()
        {
            Associations = await _context.GolfAssociations
                .AsNoTracking()
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .Select(a => new AssociationOption
                {
                    Id = a.Id,
                    Name = a.Name
                })
                .ToListAsync();
        }
    }
}