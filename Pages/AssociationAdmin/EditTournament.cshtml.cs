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

        [BindProperty]
        public FlightInput NewFlight { get; set; } = new();

        public int TournamentId { get; private set; }
        public List<TournamentFlight> Flights { get; private set; } = new();

        public class TournamentInput
        {
            [Required]
            [StringLength(120)]
            public string Name { get; set; } = string.Empty;

            [StringLength(1000)]
            public string? Description { get; set; }
            [DataType(DataType.DateTime)]
            [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
            public DateTime StartDate { get; set; }
            [DataType(DataType.DateTime)]
            [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
            public DateTime EndDate { get; set; }
            [DataType(DataType.DateTime)]
            [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
            public DateTime? RegistrationDeadline { get; set; }
            public TournamentFormat Format { get; set; }
            public string? Location { get; set; }
            public string? GolfCourse { get; set; }
            public decimal EntryFee { get; set; }
            public bool HasPracticeRound { get; set; }
            [Range(0, 100000)] public decimal PracticeRoundFee { get; set; }
            public int MaxPlayers { get; set; }
            public TournamentStatus Status { get; set; }
        }

        public class FlightInput
        {
            [Required]
            [StringLength(60)]
            public string Name { get; set; } = string.Empty;

            [StringLength(200)]
            public string? Description { get; set; }

            public int DisplayOrder { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            var tournament = await Context.Tournaments
                .Include(t => t.Flights.OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name))
                .FirstOrDefaultAsync(t => t.Id == id && t.GolfAssociationId == CurrentAssociation.Id);
            if (tournament is null)
            {
                return NotFound();
            }

            TournamentId = id;
            Flights = tournament.Flights.ToList();
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
                HasPracticeRound = tournament.HasPracticeRound,
                PracticeRoundFee = tournament.PracticeRoundFee,
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
                Flights = await Context.TournamentFlights
                    .Where(f => f.TournamentId == id)
                    .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name)
                    .ToListAsync();
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
                HasPracticeRound = Input.HasPracticeRound,
                PracticeRoundFee = Input.HasPracticeRound ? Input.PracticeRoundFee : 0,
                MaxPlayers = Input.MaxPlayers,
                Status = Input.Status
            };

            await _tournamentService.UpdateTournamentAsync(id, updateModel);
            await _tournamentService.UpdateTournamentStatusAsync(id, Input.Status);

            TempData["SuccessMessage"] = "Tournament updated successfully.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostAddFlightAsync(int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;

            var tournament = await Context.Tournaments
                .FirstOrDefaultAsync(t => t.Id == id && t.GolfAssociationId == CurrentAssociation.Id);
            if (tournament is null) return NotFound();

            if (string.IsNullOrWhiteSpace(NewFlight.Name))
            {
                TempData["ErrorMessage"] = "Flight name is required.";
                return RedirectToPage(new { id });
            }

            Context.TournamentFlights.Add(new TournamentFlight
            {
                TournamentId = id,
                Name = NewFlight.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(NewFlight.Description) ? null : NewFlight.Description.Trim(),
                DisplayOrder = NewFlight.DisplayOrder
            });
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Flight \"{NewFlight.Name.Trim()}\" added.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostDeleteFlightAsync(int id, int flightId)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;

            var flight = await Context.TournamentFlights
                .FirstOrDefaultAsync(f => f.Id == flightId && f.TournamentId == id);
            if (flight is null) return NotFound();

            var tournament = await Context.Tournaments
                .FirstOrDefaultAsync(t => t.Id == id && t.GolfAssociationId == CurrentAssociation.Id);
            if (tournament is null) return NotFound();

            Context.TournamentFlights.Remove(flight);
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Flight \"{flight.Name}\" removed.";
            return RedirectToPage(new { id });
        }
    }
}
