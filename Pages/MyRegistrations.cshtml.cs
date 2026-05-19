using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages
{
    [Authorize]
    public class MyRegistrationsModel : PageModel
    {
        private readonly IRegistrationService _registrationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public MyRegistrationsModel(
            IRegistrationService registrationService,
            UserManager<ApplicationUser> userManager)
        {
            _registrationService = registrationService;
            _userManager = userManager;
        }

        public List<Registration> Registrations { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                Registrations = (await _registrationService.GetPlayerRegistrationsAsync(user.Id)).ToList();
            }
        }

        public async Task<IActionResult> OnPostWithdrawAsync(int registrationId, string reason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var registration = await _registrationService.GetRegistrationByIdAsync(registrationId);
            if (registration == null || registration.PlayerId != user.Id)
            {
                return Forbid();
            }

            await _registrationService.WithdrawRegistrationAsync(registrationId, reason);
            TempData["SuccessMessage"] = "Registration withdrawn successfully.";
            return RedirectToPage();
        }
    }
}
