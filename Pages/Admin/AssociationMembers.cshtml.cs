using System.ComponentModel.DataAnnotations;
using GolfAssociationCommunity.Data;
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
    public class AssociationMembersModel : PageModel
    {
        private readonly IAssociationService _associationService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAdminAuditService _adminAuditService;

        public AssociationMembersModel(
            IAssociationService associationService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAdminAuditService adminAuditService)
        {
            _associationService = associationService;
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _adminAuditService = adminAuditService;
        }

        public GolfAssociation? Association { get; private set; }
        public List<ApplicationUser> Members { get; private set; } = new();
        public List<Tournament> Tournaments { get; private set; } = new();
        public int MemberCount { get; private set; }

        [BindProperty]
        public AddMemberInput Input { get; set; } = new();

        public class AddMemberInput
        {
            [Required]
            public string UserId { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            return await LoadAsync(id);
        }

        public async Task<IActionResult> OnPostAddAsync(int id)
        {
            if (!ModelState.IsValid)
            {
                return await LoadAsync(id);
            }

            var added = await _associationService.AddMemberToAssociationAsync(id, Input.UserId.Trim());
            TempData["SuccessMessage"] = added
                ? "Member added successfully."
                : "Association or user not found.";

            await _adminAuditService.WriteAsync(
                added ? "Added association member" : "Failed to add association member",
                User?.Identity?.Name ?? "anonymous",
                new Dictionary<string, string?>
                {
                    ["AssociationId"] = id.ToString(),
                    ["TargetUserId"] = Input.UserId.Trim()
                });

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostRemoveAsync(int id, string userId)
        {
            var removed = await _associationService.RemoveMemberFromAssociationAsync(id, userId);

            if (removed)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null && await _userManager.IsInRoleAsync(user, "AssociationAdmin"))
                {
                    await _userManager.RemoveFromRoleAsync(user, "AssociationAdmin");
                }

                var association = await _context.GolfAssociations.FindAsync(id);
                if (association != null && association.AdminUserId == userId)
                {
                    association.AdminUserId = null;
                    association.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["SuccessMessage"] = removed
                ? "Member removed successfully."
                : "Member could not be removed.";

            await _adminAuditService.WriteAsync(
                removed ? "Removed association member" : "Failed to remove association member",
                User?.Identity?.Name ?? "anonymous",
                new Dictionary<string, string?>
                {
                    ["AssociationId"] = id.ToString(),
                    ["TargetUserId"] = userId
                });

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostSetAssociationAdminAsync(int id, string userId)
        {
            var association = await _context.GolfAssociations.FindAsync(id);
            if (association is null)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                TempData["SuccessMessage"] = "User not found.";
                await _adminAuditService.WriteAsync(
                    "Failed to assign association admin",
                    User?.Identity?.Name ?? "anonymous",
                    new Dictionary<string, string?>
                    {
                        ["AssociationId"] = id.ToString(),
                        ["TargetUserId"] = userId
                    });
                return RedirectToPage(new { id });
            }

            if (!await _roleManager.RoleExistsAsync("AssociationAdmin"))
            {
                await _roleManager.CreateAsync(new IdentityRole("AssociationAdmin"));
            }

            if (user.GolfAssociationId != id)
            {
                user.GolfAssociationId = id;
                user.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }

            if (!await _userManager.IsInRoleAsync(user, "AssociationAdmin"))
            {
                await _userManager.AddToRoleAsync(user, "AssociationAdmin");
            }

            association.AdminUserId = userId;
            association.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{user.Email} is now the association admin.";
            await _adminAuditService.WriteAsync(
                "Assigned association admin",
                User?.Identity?.Name ?? "anonymous",
                new Dictionary<string, string?>
                {
                    ["AssociationId"] = id.ToString(),
                    ["TargetUserId"] = userId
                });
            return RedirectToPage(new { id });
        }

        private async Task<IActionResult> LoadAsync(int id)
        {
            Association = await _associationService.GetAssociationByIdAsync(id);
            if (Association is null)
            {
                return NotFound();
            }

            Members = (await _associationService.GetAssociationMembersAsync(id)).ToList();
            Tournaments = (await _associationService.GetAssociationTournamentsAsync(id)).ToList();
            MemberCount = await _associationService.GetMemberCountAsync(id);

            return Page();
        }
    }
}
