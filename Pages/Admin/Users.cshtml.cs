using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using GolfAssociationCommunity.Services;

namespace GolfAssociationCommunity.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class UsersModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAdminAuditService _adminAuditService;

        public UsersModel(
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

        public List<UserRow> Users { get; private set; } = new();
        public List<AssociationOption> Associations { get; private set; } = new();
        public int PageSize { get; } = 20;
        public int PageNumber { get; private set; } = 1;
        public int TotalPages { get; private set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? RoleFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        public class UserRow
        {
            public string Id { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string AssociationName { get; set; } = "-";
            public bool IsAdmin { get; set; }
            public bool IsAssociationAdmin { get; set; }
            public bool IsLockedOut { get; set; }
        }

        public class AssociationOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostToggleAdminAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                TempData["SuccessMessage"] = "User not found.";
                return RedirectToPage(GetStateRouteValues());
            }

            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                var adminCount = await _userManager.GetUsersInRoleAsync("Admin");
                if (adminCount.Count <= 1)
                {
                    TempData["SuccessMessage"] = "Cannot remove the Admin role from the last Admin account.";
                    await AuditAsync("Prevented Admin role removal from last admin", user);
                    return RedirectToPage(GetStateRouteValues());
                }

                await _userManager.RemoveFromRoleAsync(user, "Admin");
                TempData["SuccessMessage"] = $"Admin role removed from {user.Email}.";
                await AuditAsync("Removed Admin role", user);
            }
            else
            {
                if (await _userManager.IsInRoleAsync(user, "AssociationAdmin"))
                {
                    await _userManager.RemoveFromRoleAsync(user, "AssociationAdmin");
                }

                var associations = await _context.GolfAssociations
                    .Where(a => a.AdminUserId == user.Id)
                    .ToListAsync();

                foreach (var association in associations)
                {
                    association.AdminUserId = null;
                    association.UpdatedAt = DateTime.UtcNow;
                }

                user.GolfAssociationId = null;
                user.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                await _context.SaveChangesAsync();
                await _userManager.AddToRoleAsync(user, "Admin");
                TempData["SuccessMessage"] = $"Admin role granted to {user.Email}.";
                await AuditAsync("Granted Admin role", user);
            }

            return RedirectToPage(GetStateRouteValues());
        }

        public async Task<IActionResult> OnPostToggleAssociationAdminAsync(string userId, int? associationId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                TempData["SuccessMessage"] = "User not found.";
                return RedirectToPage(GetStateRouteValues());
            }

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["SuccessMessage"] = "Admin users cannot be associated to a golf association.";
                await AuditAsync("Prevented association binding for admin user", user);
                return RedirectToPage(GetStateRouteValues());
            }

            if (!await _roleManager.RoleExistsAsync("AssociationAdmin"))
            {
                await _roleManager.CreateAsync(new IdentityRole("AssociationAdmin"));
            }

            if (await _userManager.IsInRoleAsync(user, "AssociationAdmin"))
            {
                await _userManager.RemoveFromRoleAsync(user, "AssociationAdmin");

                var associations = await _context.GolfAssociations
                    .Where(a => a.AdminUserId == user.Id)
                    .ToListAsync();

                foreach (var association in associations)
                {
                    association.AdminUserId = null;
                    association.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Association admin role removed from {user.Email}.";
                await AuditAsync("Removed AssociationAdmin role", user, new Dictionary<string, string?>
                {
                    ["AssociationIds"] = string.Join(',', associations.Select(a => a.Id))
                });
            }
            else
            {
                if (!associationId.HasValue)
                {
                    TempData["SuccessMessage"] = "Select an association before assigning Association Admin.";
                    return RedirectToPage(GetStateRouteValues());
                }

                var association = await _context.GolfAssociations.FindAsync(associationId.Value);
                if (association is null)
                {
                    TempData["SuccessMessage"] = "Selected association not found.";
                    return RedirectToPage(GetStateRouteValues());
                }

                if (!string.IsNullOrWhiteSpace(association.AdminUserId) && association.AdminUserId != user.Id)
                {
                    var previousAdminId = association.AdminUserId;
                    association.AdminUserId = null;

                    var otherAdminAssignments = await _context.GolfAssociations
                        .CountAsync(a => a.AdminUserId == previousAdminId && a.Id != association.Id);

                    if (otherAdminAssignments == 0)
                    {
                        var previousAdmin = await _userManager.FindByIdAsync(previousAdminId);
                        if (previousAdmin != null && await _userManager.IsInRoleAsync(previousAdmin, "AssociationAdmin"))
                        {
                            await _userManager.RemoveFromRoleAsync(previousAdmin, "AssociationAdmin");
                        }
                    }
                }

                user.GolfAssociationId = association.Id;
                user.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                await _userManager.AddToRoleAsync(user, "AssociationAdmin");
                association.AdminUserId = user.Id;
                association.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"{user.Email} is now association admin for {association.Name}.";
                await AuditAsync("Granted AssociationAdmin role", user, new Dictionary<string, string?>
                {
                    ["AssociationId"] = association.Id.ToString(),
                    ["AssociationName"] = association.Name
                });
            }

            return RedirectToPage(GetStateRouteValues());
        }

        public async Task<IActionResult> OnPostToggleLockoutAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                TempData["SuccessMessage"] = "User not found.";
                return RedirectToPage(GetStateRouteValues());
            }

            var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
            if (!isLockedOut && await _userManager.IsInRoleAsync(user, "Admin"))
            {
                var adminCount = await _userManager.GetUsersInRoleAsync("Admin");
                if (adminCount.Count <= 1)
                {
                    TempData["SuccessMessage"] = "Cannot lock the last Admin account.";
                    await AuditAsync("Prevented lockout of last admin", user);
                    return RedirectToPage(GetStateRouteValues());
                }
            }

            await _userManager.SetLockoutEnabledAsync(user, true);

            if (isLockedOut)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                TempData["SuccessMessage"] = $"User unlocked: {user.Email}.";
                await AuditAsync("Unlocked user", user);
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                TempData["SuccessMessage"] = $"User locked: {user.Email}.";
                await AuditAsync("Locked user", user);
            }

            return RedirectToPage(GetStateRouteValues());
        }

        private async Task LoadAsync()
        {
            var users = await _context.Users
                .AsNoTracking()
                .OrderBy(u => u.Email)
                .ToListAsync();

            var associationNames = await _context.GolfAssociations
                .AsNoTracking()
                .ToDictionaryAsync(a => a.Id, a => a.Name);

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

            var rows = new List<UserRow>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

                rows.Add(new UserRow
                {
                    Id = user.Id,
                    Email = user.Email ?? "(no email)",
                    DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
                    AssociationName = user.GolfAssociationId.HasValue && associationNames.TryGetValue(user.GolfAssociationId.Value, out var name)
                        ? name
                        : "-",
                    IsAdmin = roles.Contains("Admin"),
                    IsAssociationAdmin = roles.Contains("AssociationAdmin"),
                    IsLockedOut = isLockedOut
                });
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                rows = rows
                    .Where(r =>
                        r.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        r.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        r.AssociationName.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(RoleFilter))
            {
                rows = RoleFilter switch
                {
                    "Admin" => rows.Where(r => r.IsAdmin).ToList(),
                    "AssociationAdmin" => rows.Where(r => r.IsAssociationAdmin).ToList(),
                    "Member" => rows.Where(r => !r.IsAdmin && !r.IsAssociationAdmin).ToList(),
                    _ => rows
                };
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                rows = StatusFilter switch
                {
                    "Active" => rows.Where(r => !r.IsLockedOut).ToList(),
                    "Locked" => rows.Where(r => r.IsLockedOut).ToList(),
                    _ => rows
                };
            }

            PageNumber = CurrentPage < 1 ? 1 : CurrentPage;
            var totalCount = rows.Count;
            TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)PageSize);
            if (PageNumber > TotalPages)
            {
                PageNumber = TotalPages;
            }

            Users = rows
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }

        private object GetStateRouteValues()
        {
            var search = Search ?? Request.Query["Search"].ToString();
            var roleFilter = RoleFilter ?? Request.Query["RoleFilter"].ToString();
            var statusFilter = StatusFilter ?? Request.Query["StatusFilter"].ToString();
            var pageValue = CurrentPage;

            if (pageValue < 1 && int.TryParse(Request.Query["CurrentPage"], out var parsedPage) && parsedPage > 0)
            {
                pageValue = parsedPage;
            }

            if (pageValue < 1)
            {
                pageValue = 1;
            }

            return new
            {
                Search = search,
                RoleFilter = roleFilter,
                StatusFilter = statusFilter,
                CurrentPage = pageValue
            };
        }

        private async Task AuditAsync(string action, ApplicationUser target, IDictionary<string, string?>? extraDetails = null)
        {
            var details = new Dictionary<string, string?>
            {
                ["TargetUserId"] = target.Id,
                ["TargetEmail"] = target.Email
            };

            if (extraDetails != null)
            {
                foreach (var detail in extraDetails)
                {
                    details[detail.Key] = detail.Value;
                }
            }

            await _adminAuditService.WriteAsync(action, User?.Identity?.Name ?? "anonymous", details);
        }
    }
}
