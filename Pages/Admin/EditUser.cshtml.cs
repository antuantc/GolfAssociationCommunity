using System.ComponentModel.DataAnnotations;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class EditUserModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAdminAuditService _adminAuditService;

        public EditUserModel(UserManager<ApplicationUser> userManager, IAdminAuditService adminAuditService)
        {
            _userManager = userManager;
            _adminAuditService = adminAuditService;
        }

        public string UserId { get; private set; } = string.Empty;

        [BindProperty]
        public EditUserInput Input { get; set; } = new();

        public class EditUserInput
        {
            [Required]
            [EmailAddress]
            [StringLength(256)]
            public string Email { get; set; } = string.Empty;

            [StringLength(100)]
            public string? FirstName { get; set; }

            [StringLength(100)]
            public string? LastName { get; set; }

            [DataType(DataType.Password)]
            [StringLength(100, MinimumLength = 8)]
            public string? NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Compare(nameof(NewPassword), ErrorMessage = "Password and confirmation do not match.")]
            public string? ConfirmPassword { get; set; }

            public bool RequirePasswordChangeOnNextLogin { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            UserId = user.Id;
            Input = new EditUserInput
            {
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                RequirePasswordChangeOnNextLogin = user.RequirePasswordChange
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            UserId = user.Id;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var normalizedEmail = Input.Email.Trim();
            var emailOwner = await _userManager.FindByEmailAsync(normalizedEmail);
            if (emailOwner != null && emailOwner.Id != user.Id)
            {
                ModelState.AddModelError(nameof(Input.Email), "Email is already used by another account.");
                return Page();
            }

            user.Email = normalizedEmail;
            user.UserName = normalizedEmail;
            user.FirstName = string.IsNullOrWhiteSpace(Input.FirstName) ? null : Input.FirstName.Trim();
            user.LastName = string.IsNullOrWhiteSpace(Input.LastName) ? null : Input.LastName.Trim();
            user.RequirePasswordChange = Input.RequirePasswordChangeOnNextLogin;
            user.UpdatedAt = DateTime.UtcNow;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                AddErrors(updateResult);
                return Page();
            }

            var passwordChanged = false;
            if (!string.IsNullOrWhiteSpace(Input.NewPassword))
            {
                IdentityResult passwordResult;
                if (await _userManager.HasPasswordAsync(user))
                {
                    var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                    passwordResult = await _userManager.ResetPasswordAsync(user, resetToken, Input.NewPassword);
                }
                else
                {
                    passwordResult = await _userManager.AddPasswordAsync(user, Input.NewPassword);
                }

                if (!passwordResult.Succeeded)
                {
                    AddErrors(passwordResult);
                    return Page();
                }

                passwordChanged = true;
            }

            await _adminAuditService.WriteAsync("Updated user profile", User?.Identity?.Name ?? "anonymous", new Dictionary<string, string?>
            {
                ["TargetUserId"] = user.Id,
                ["TargetEmail"] = user.Email,
                ["PasswordChanged"] = passwordChanged ? "Yes" : "No",
                ["RequirePasswordChange"] = user.RequirePasswordChange ? "Yes" : "No"
            });

            TempData["SuccessMessage"] = passwordChanged
                ? "User profile and password updated successfully."
                : "User profile updated successfully.";

            return RedirectToPage("/Admin/Users");
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
