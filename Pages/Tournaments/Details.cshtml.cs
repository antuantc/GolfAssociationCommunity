using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Tournaments
{
    public class DetailsModel : PageModel
    {
        private readonly ITournamentService _tournamentService;
        private readonly IRegistrationService _registrationService;
        private readonly ILeaderboardService _leaderboardService;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(
            ITournamentService tournamentService,
            IRegistrationService registrationService,
            ILeaderboardService leaderboardService,
            UserManager<ApplicationUser> userManager)
        {
            _tournamentService = tournamentService;
            _registrationService = registrationService;
            _leaderboardService = leaderboardService;
            _userManager = userManager;
        }

        public Tournament? Tournament { get; set; }
        public int RegistrationCount { get; set; }
        public bool CanRegister { get; set; }
        public bool IsSignedIn { get; set; }
        public Registration? CurrentRegistration { get; set; }
        public List<Leaderboard> Leaderboard { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Tournament = await _tournamentService.GetTournamentByIdAsync(id);
            if (Tournament == null)
            {
                return NotFound();
            }

            RegistrationCount = await _registrationService.GetRegistrationCountAsync(id, RegistrationStatus.Registered);
            Leaderboard = (await _leaderboardService.GetTournamentLeaderboardAsync(id)).ToList();
            IsSignedIn = User.Identity?.IsAuthenticated ?? false;

            if (IsSignedIn)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    CurrentRegistration = await _registrationService.GetPlayerTournamentRegistrationAsync(id, user.Id);
                    CanRegister = await _registrationService.CanPlayerRegisterAsync(id, user.Id);
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostRegisterAsync(int tournamentId)
        {
            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                return Challenge();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var tournament = await _tournamentService.GetTournamentByIdAsync(tournamentId);
            if (tournament == null)
            {
                return NotFound();
            }

            if (!await _registrationService.CanPlayerRegisterAsync(tournamentId, user.Id))
            {
                TempData["ErrorMessage"] = "Registration is not available for this tournament.";
                return RedirectToPage(new { id = tournamentId });
            }

            var registration = new Registration
            {
                TournamentId = tournamentId,
                PlayerId = user.Id,
                RegistrationFee = tournament.EntryFee,
                Status = RegistrationStatus.Pending,
                PaymentConfirmed = false
            };

            await _registrationService.CreateRegistrationAsync(registration);
            TempData["SuccessMessage"] = "Your registration has been submitted.";
            return RedirectToPage(new { id = tournamentId });
        }

        public async Task<IActionResult> OnPostWithdrawAsync(int registrationId, string reason)
        {
            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                return Challenge();
            }

            var registration = await _registrationService.GetRegistrationByIdAsync(registrationId);
            if (registration == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || registration.PlayerId != user.Id)
            {
                return Forbid();
            }

            await _registrationService.WithdrawRegistrationAsync(registrationId, reason);
            TempData["SuccessMessage"] = "Your registration has been withdrawn.";
            return RedirectToPage(new { id = registration.TournamentId });
        }
    }
}
