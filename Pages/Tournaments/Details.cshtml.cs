using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Tournaments
{
    public class DetailsModel : PageModel
    {
        private readonly ITournamentService _tournamentService;
        private readonly IRegistrationService _registrationService;
        private readonly ILeaderboardService _leaderboardService;

        public DetailsModel(
            ITournamentService tournamentService,
            IRegistrationService registrationService,
            ILeaderboardService leaderboardService)
        {
            _tournamentService = tournamentService;
            _registrationService = registrationService;
            _leaderboardService = leaderboardService;
        }

        public Tournament? Tournament { get; set; }
        public int? AssociationId { get; set; }
        public int RegistrationCount { get; set; }
        public bool CanRegister { get; set; }
        public List<Leaderboard> Leaderboard { get; set; } = new();

        [BindProperty]
        public GuestRegistrationInput Input { get; set; } = new();

        public class GuestRegistrationInput
        {
            [Required]
            [StringLength(120)]
            public string GuestName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            [StringLength(256)]
            public string GuestEmail { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(int id, int? associationId)
        {
            Tournament = await _tournamentService.GetTournamentByIdAsync(id);
            if (Tournament == null)
            {
                return NotFound();
            }

            if (!associationId.HasValue)
            {
                return RedirectToPage(new { id, associationId = Tournament.GolfAssociationId });
            }

            if (associationId.HasValue)
            {
                if (Tournament.GolfAssociationId != associationId.Value)
                {
                    return NotFound();
                }

                AssociationId = associationId.Value;
                if (Tournament.GolfAssociation != null)
                {
                    ViewData["PublicAssociationId"] = Tournament.GolfAssociation.Id;
                    ViewData["PublicAssociationName"] = Tournament.GolfAssociation.Name;
                }
            }

            RegistrationCount = await _registrationService.GetRegistrationCountAsync(id, RegistrationStatus.Registered);
            Leaderboard = (await _leaderboardService.GetTournamentLeaderboardAsync(id)).ToList();
            CanRegister = (!Tournament.RegistrationDeadline.HasValue || Tournament.RegistrationDeadline.Value >= DateTime.UtcNow)
                && RegistrationCount < Tournament.MaxPlayers;

            return Page();
        }

        public async Task<IActionResult> OnPostRegisterAsync(int tournamentId, int? associationId)
        {
            var tournament = await _tournamentService.GetTournamentByIdAsync(tournamentId);
            if (tournament == null)
            {
                return NotFound();
            }

            associationId ??= tournament.GolfAssociationId;

            if (associationId.HasValue && tournament.GolfAssociationId != associationId.Value)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                Tournament = tournament;
                AssociationId = associationId;
                if (associationId.HasValue && tournament.GolfAssociation != null)
                {
                    ViewData["PublicAssociationId"] = tournament.GolfAssociation.Id;
                    ViewData["PublicAssociationName"] = tournament.GolfAssociation.Name;
                }
                RegistrationCount = await _registrationService.GetRegistrationCountAsync(tournamentId, RegistrationStatus.Registered);
                Leaderboard = (await _leaderboardService.GetTournamentLeaderboardAsync(tournamentId)).ToList();
                CanRegister = true;
                return Page();
            }

            if (!await _registrationService.CanGuestRegisterAsync(tournamentId, Input.GuestEmail))
            {
                TempData["ErrorMessage"] = "Registration is not available for this tournament or this email is already registered.";
                return RedirectToPage(new { id = tournamentId, associationId });
            }

            var registration = new Registration
            {
                TournamentId = tournamentId,
                PlayerId = null,
                GuestName = Input.GuestName.Trim(),
                GuestEmail = Input.GuestEmail.Trim(),
                RegistrationFee = tournament.EntryFee,
                Status = RegistrationStatus.Pending,
                PaymentConfirmed = false
            };

            await _registrationService.CreateRegistrationAsync(registration);
            TempData["SuccessMessage"] = "Your registration has been submitted.";
            return RedirectToPage(new { id = tournamentId, associationId });
        }
    }
}
