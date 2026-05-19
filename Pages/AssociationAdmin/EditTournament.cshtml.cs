using System.ComponentModel.DataAnnotations;
using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class EditTournamentModel : AssociationAdminPageModel
    {
        private readonly ITournamentService _tournamentService;

        public EditTournamentModel(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ITournamentService tournamentService)
            : base(userManager, context)
        {
            _tournamentService = tournamentService;
        }

        [BindProperty]
        public TournamentInput Input { get; set; } = new();

        public int TournamentId { get; private set; }

        public class TournamentInput
        {
            [Required]
            [StringLength(120)]
            public string Name { get; set; } = string.Empty;

            [StringLength(1000)]
            public string? Description { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public DateTime? RegistrationDeadline { get; set; }
            public TournamentFormat Format { get; set; }
            public string? Location { get; set; }
            public string? GolfCourse { get; set; }
            public decimal EntryFee { get; set; }
            public int MaxPlayers { get; set; }
            public TournamentStatus Status { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            var tournament = await Context.Tournaments.FirstOrDefaultAsync(t => t.Id == id && t.GolfAssociationId == CurrentAssociation.Id);
            if (tournament is null)
            {
                return NotFound();
            }

            TournamentId = id;
            Input = new TournamentInput
            {
                Name = tournament.Name,
                Description = tournament.Description,
                StartDate = tournament.StartDate,
                EndDate = tournament.EndDate,
                RegistrationDeadline = tournament.RegistrationDeadline,
                Format = tournament.Format,
                Location = tournament.Location,
                GolfCourse = tournament.GolfCourse,
                EntryFee = tournament.EntryFee,
                MaxPlayers = tournament.MaxPlayers,
                Status = tournament.Status
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            if (Input.EndDate < Input.StartDate)
            {
                ModelState.AddModelError(string.Empty, "End date must be on or after start date.");
            }

            if (!ModelState.IsValid)
            {
                TournamentId = id;
                return Page();
            }

            var tournament = await Context.Tournaments.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.GolfAssociationId == CurrentAssociation.Id);
            if (tournament is null)
            {
                return NotFound();
            }

            var updateModel = new Tournament
            {
                Name = Input.Name.Trim(),
                Description = Input.Description,
                StartDate = Input.StartDate,
                EndDate = Input.EndDate,
                RegistrationDeadline = Input.RegistrationDeadline,
                Format = Input.Format,
                Location = Input.Location,
                GolfCourse = Input.GolfCourse,
                EntryFee = Input.EntryFee,
                MaxPlayers = Input.MaxPlayers,
                Status = Input.Status
            };

            await _tournamentService.UpdateTournamentAsync(id, updateModel);
            await _tournamentService.UpdateTournamentStatusAsync(id, Input.Status);

            TempData["SuccessMessage"] = "Tournament updated successfully.";
            return RedirectToPage(new { id });
        }
    }
}
