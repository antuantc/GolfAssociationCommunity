using System.ComponentModel.DataAnnotations;
using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class CreateTournamentModel : AssociationAdminPageModel
    {
        private readonly ITournamentService _tournamentService;

        public CreateTournamentModel(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ITournamentService tournamentService)
            : base(userManager, context)
        {
            _tournamentService = tournamentService;
        }

        [BindProperty]
        public TournamentInput Input { get; set; } = new();

        public class TournamentInput
        {
            [Required]
            [StringLength(120)]
            public string Name { get; set; } = string.Empty;

            [StringLength(1000)]
            public string? Description { get; set; }

            [DataType(DataType.DateTime)]
            public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(14);

            [DataType(DataType.DateTime)]
            public DateTime EndDate { get; set; } = DateTime.UtcNow.AddDays(15);

            [DataType(DataType.DateTime)]
            public DateTime? RegistrationDeadline { get; set; }

            public TournamentFormat Format { get; set; } = TournamentFormat.Stroke;

            [StringLength(150)]
            public string? Location { get; set; }

            [StringLength(150)]
            public string? GolfCourse { get; set; }

            [Range(0, 100000)]
            public decimal EntryFee { get; set; }

            [Range(1, 10000)]
            public int MaxPlayers { get; set; } = 100;

            public TournamentStatus Status { get; set; } = TournamentStatus.Scheduled;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
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
                return Page();
            }

            var tournament = new Tournament
            {
                GolfAssociationId = CurrentAssociation.Id,
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

            var created = await _tournamentService.CreateTournamentAsync(tournament);
            TempData["SuccessMessage"] = "Tournament created successfully.";
            return RedirectToPage("/AssociationAdmin/EditTournament", new { id = created.Id });
        }
    }
}
