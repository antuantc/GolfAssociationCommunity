using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class RegistrationsModel : AssociationAdminPageModel
    {
        public RegistrationsModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
            : base(userManager, context)
        {
        }

        [BindProperty(SupportsGet = true)]
        public int? TournamentId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? EditId { get; set; }

        [BindProperty]
        public RegistrationInput Input { get; set; } = new();

        public List<Tournament> Tournaments { get; private set; } = new();
        public Tournament? SelectedTournament { get; private set; }
        public List<RegistrationRow> Registrations { get; private set; } = new();
        public List<TournamentFlight> Flights { get; private set; } = new();
        public List<PlayerOption> AssocPlayers { get; private set; } = new();

        public bool IsEditing => EditId.HasValue;
        public bool ShowForm   { get; private set; }
        public int ActiveCount    => Registrations.Count(r => r.Status == RegistrationStatus.Registered);
        public int PendingCount   => Registrations.Count(r => r.Status == RegistrationStatus.Pending);
        public int ConfirmedCount => Registrations.Count(r => r.PaymentConfirmed && r.Status == RegistrationStatus.Registered);
        public int CancelledCount => Registrations.Count(r => r.Status == RegistrationStatus.Cancelled || r.Status == RegistrationStatus.Withdrew);

        public class PlayerOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }

        public class RegistrationInput
        {
            public bool IsGuest { get; set; }
            public int? AssociationPlayerId { get; set; }

            [StringLength(200)]
            public string? GuestName { get; set; }

            [StringLength(256)]
            public string? GuestEmail { get; set; }

            [Range(-10, 60)]
            public decimal? Handicap { get; set; }

            public RegistrationStatus Status { get; set; } = RegistrationStatus.Registered;
            public bool PaymentConfirmed { get; set; }

            [Range(0, 100000)]
            public decimal RegistrationFee { get; set; }

            public string? Flight { get; set; }
        }

        public class RegistrationRow
        {
            public int Id { get; set; }
            public string PlayerName { get; set; } = string.Empty;
            public string PlayerEmail { get; set; } = string.Empty;
            public bool IsGuest { get; set; }
            public decimal? Handicap { get; set; }
            public RegistrationStatus Status { get; set; }
            public bool PaymentConfirmed { get; set; }
            public decimal RegistrationFee { get; set; }
            public string? Flight { get; set; }
            public DateTime RegistrationDate { get; set; }
            public string? CardLast4 { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;
            await LoadPageDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;

            if (!TournamentId.HasValue)
            {
                TempData["SuccessMessage"] = "No tournament selected.";
                return RedirectToPage();
            }

            if (Input.IsGuest)
            {
                if (string.IsNullOrWhiteSpace(Input.GuestName))
                    ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.GuestName)}", "Guest name is required.");
            }
            else
            {
                if (!Input.AssociationPlayerId.HasValue)
                    ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.AssociationPlayerId)}", "Select a player.");
            }

            if (!ModelState.IsValid)
            {
                ShowForm = true;
                await LoadPageDataAsync();
                return Page();
            }

            var tournament = await Context.Tournaments
                .FirstOrDefaultAsync(t => t.Id == TournamentId.Value && t.GolfAssociationId == CurrentAssociation.Id);
            if (tournament == null) return NotFound();

            var flightName = string.IsNullOrWhiteSpace(Input.Flight) ? null : Input.Flight.Trim();
            int? flightId = null;
            if (flightName != null)
            {
                var match = await Context.TournamentFlights
                    .FirstOrDefaultAsync(f => f.TournamentId == TournamentId.Value && f.Name == flightName);
                flightId = match?.Id;
            }

            if (EditId.HasValue)
            {
                var reg = await Context.Registrations
                    .FirstOrDefaultAsync(r => r.Id == EditId.Value && r.TournamentId == TournamentId.Value);
                if (reg == null) return NotFound();

                if (Input.IsGuest)
                {
                    reg.AssociationPlayerId = null;
                    reg.GuestName = (Input.GuestName ?? string.Empty).Trim();
                    reg.GuestEmail = (Input.GuestEmail ?? string.Empty).Trim();
                }
                else
                {
                    reg.AssociationPlayerId = Input.AssociationPlayerId;
                    reg.GuestName = string.Empty;
                    reg.GuestEmail = string.Empty;
                }
                reg.Handicap = Input.Handicap;
                reg.Status = Input.Status;
                reg.PaymentConfirmed = Input.PaymentConfirmed;
                reg.RegistrationFee = Input.RegistrationFee;
                reg.Flight = flightName;
                reg.TournamentFlightId = flightId;
                reg.UpdatedAt = DateTime.UtcNow;
                TempData["SuccessMessage"] = "Registration updated.";
            }
            else
            {
                if (!Input.IsGuest && Input.AssociationPlayerId.HasValue)
                {
                    var duplicate = await Context.Registrations
                        .AnyAsync(r => r.TournamentId == TournamentId.Value
                            && r.AssociationPlayerId == Input.AssociationPlayerId.Value);
                    if (duplicate)
                    {
                        ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.AssociationPlayerId)}",
                            "This player already has a registration for this tournament.");
                        ShowForm = true;
                        await LoadPageDataAsync();
                        return Page();
                    }
                }

                Context.Registrations.Add(new Registration
                {
                    TournamentId = TournamentId.Value,
                    AssociationPlayerId = Input.IsGuest ? null : Input.AssociationPlayerId,
                    GuestName = Input.IsGuest ? (Input.GuestName ?? string.Empty).Trim() : string.Empty,
                    GuestEmail = Input.IsGuest ? (Input.GuestEmail ?? string.Empty).Trim() : string.Empty,
                    Handicap = Input.Handicap,
                    Status = Input.Status,
                    PaymentConfirmed = Input.PaymentConfirmed,
                    RegistrationFee = Input.RegistrationFee,
                    Flight = flightName,
                    TournamentFlightId = flightId,
                    RegistrationDate = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                TempData["SuccessMessage"] = "Registration added.";
            }

            await Context.SaveChangesAsync();
            return RedirectToPage(new { TournamentId });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;

            var reg = await Context.Registrations
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.Id == id
                    && r.Tournament != null
                    && r.Tournament.GolfAssociationId == CurrentAssociation.Id);
            if (reg == null) return NotFound();

            Context.Registrations.Remove(reg);
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Registration deleted.";
            return RedirectToPage(new { TournamentId });
        }

        public async Task<IActionResult> OnPostConfirmPaymentAsync(int registrationId)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;
            var reg = await LoadRegistrationAsync(registrationId);
            if (reg == null) return NotFound();
            reg.PaymentConfirmed = true;
            if (reg.Status == RegistrationStatus.Pending) reg.Status = RegistrationStatus.Registered;
            reg.UpdatedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Payment confirmed.";
            return RedirectToPage(new { TournamentId });
        }

        public async Task<IActionResult> OnPostCancelRegistrationAsync(int registrationId)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;
            var reg = await LoadRegistrationAsync(registrationId);
            if (reg == null) return NotFound();
            reg.Status = RegistrationStatus.Cancelled;
            reg.UpdatedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Registration cancelled.";
            return RedirectToPage(new { TournamentId });
        }

        public async Task<IActionResult> OnPostRestoreAsync(int registrationId)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;
            var reg = await LoadRegistrationAsync(registrationId);
            if (reg == null) return NotFound();
            reg.Status = RegistrationStatus.Registered;
            reg.UpdatedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Registration restored.";
            return RedirectToPage(new { TournamentId });
        }

        private async Task<Registration?> LoadRegistrationAsync(int id)
        {
            return await Context.Registrations
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.Id == id
                    && r.Tournament != null
                    && r.Tournament.GolfAssociationId == CurrentAssociation.Id);
        }

        private async Task LoadPageDataAsync()
        {
            Tournaments = await Context.Tournaments
                .Where(t => t.GolfAssociationId == CurrentAssociation.Id)
                .OrderByDescending(t => t.StartDate)
                .ToListAsync();

            if (!TournamentId.HasValue)
                TournamentId = Tournaments.FirstOrDefault()?.Id;

            if (!TournamentId.HasValue) return;

            SelectedTournament = Tournaments.FirstOrDefault(t => t.Id == TournamentId.Value);
            if (SelectedTournament == null) return;

            Flights = await Context.TournamentFlights
                .Where(f => f.TournamentId == TournamentId.Value)
                .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name)
                .ToListAsync();

            AssocPlayers = await Context.AssociationPlayers
                .Where(p => p.GolfAssociationId == CurrentAssociation.Id && p.IsActive)
                .OrderBy(p => p.DisplayName)
                .Select(p => new PlayerOption { Id = p.Id, Name = p.DisplayName, Email = p.Email })
                .ToListAsync();

            if (EditId.HasValue)
            {
                var reg = await Context.Registrations
                    .Include(r => r.TournamentFlight)
                    .FirstOrDefaultAsync(r => r.Id == EditId.Value && r.TournamentId == TournamentId.Value);
                if (reg != null)
                {
                    Input = new RegistrationInput
                    {
                        IsGuest = !reg.AssociationPlayerId.HasValue,
                        AssociationPlayerId = reg.AssociationPlayerId,
                        GuestName = reg.GuestName,
                        GuestEmail = reg.GuestEmail,
                        Handicap = reg.Handicap,
                        Status = reg.Status,
                        PaymentConfirmed = reg.PaymentConfirmed,
                        RegistrationFee = reg.RegistrationFee,
                        Flight = reg.TournamentFlight?.Name ?? reg.Flight
                    };
                }
                else
                {
                    EditId = null;
                }
            }

            var regs = await Context.Registrations
                .Where(r => r.TournamentId == TournamentId.Value)
                .Include(r => r.AssociationPlayer)
                .Include(r => r.TournamentFlight)
                .OrderBy(r => r.AssociationPlayer != null ? r.AssociationPlayer.DisplayName : r.GuestName)
                .ToListAsync();

            Registrations = regs.Select(r => new RegistrationRow
            {
                Id = r.Id,
                PlayerName = r.AssociationPlayerId.HasValue
                    ? (r.AssociationPlayer?.DisplayName ?? "Unknown Member")
                    : (string.IsNullOrWhiteSpace(r.GuestName) ? "Guest" : r.GuestName),
                PlayerEmail = r.AssociationPlayerId.HasValue
                    ? (r.AssociationPlayer?.Email ?? string.Empty)
                    : r.GuestEmail,
                IsGuest = !r.AssociationPlayerId.HasValue,
                Handicap = r.Handicap,
                Status = r.Status,
                PaymentConfirmed = r.PaymentConfirmed,
                RegistrationFee = r.RegistrationFee,
                Flight = r.TournamentFlight?.Name ?? r.Flight,
                RegistrationDate = r.RegistrationDate,
                CardLast4 = r.CardLast4
            }).ToList();
        }
    }
}

