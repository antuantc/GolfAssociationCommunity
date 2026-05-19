using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class RegistrationsModel : AssociationAdminPageModel
    {
        private readonly IRegistrationService _registrationService;

        public RegistrationsModel(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IRegistrationService registrationService)
            : base(userManager, context)
        {
            _registrationService = registrationService;
        }

        public List<Tournament> Tournaments { get; private set; } = new();
        public List<Registration> Registrations { get; private set; } = new();
        public int? SelectedTournamentId { get; private set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            return await LoadPageDataAsync(id);
        }

        public async Task<IActionResult> OnPostConfirmPaymentAsync(int registrationId, int? id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            var registration = await Context.Registrations
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.Id == registrationId && r.Tournament != null && r.Tournament.GolfAssociationId == CurrentAssociation.Id);

            if (registration is null)
            {
                return NotFound();
            }

            var transactionId = $"MANUAL-{DateTime.UtcNow:yyyyMMddHHmmss}-{registrationId}";
            await _registrationService.ConfirmPaymentAsync(registrationId, transactionId);
            TempData["SuccessMessage"] = "Payment confirmed and registration marked as registered.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostSetStatusAsync(int registrationId, RegistrationStatus status, int? id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            var registration = await Context.Registrations
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.Id == registrationId && r.Tournament != null && r.Tournament.GolfAssociationId == CurrentAssociation.Id);

            if (registration is null)
            {
                return NotFound();
            }

            registration.Status = status;
            await _registrationService.UpdateRegistrationAsync(registrationId, registration);
            TempData["SuccessMessage"] = "Registration status updated.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostWithdrawAsync(int registrationId, int? id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            var registration = await Context.Registrations
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.Id == registrationId && r.Tournament != null && r.Tournament.GolfAssociationId == CurrentAssociation.Id);

            if (registration is null)
            {
                return NotFound();
            }

            await _registrationService.WithdrawRegistrationAsync(registrationId, "Withdrawn by association admin");
            TempData["SuccessMessage"] = "Registration withdrawn.";
            return RedirectToPage(new { id });
        }

        private async Task<IActionResult> LoadPageDataAsync(int? tournamentId)
        {
            SelectedTournamentId = tournamentId;
            Tournaments = await Context.Tournaments
                .Where(t => t.GolfAssociationId == CurrentAssociation.Id)
                .OrderByDescending(t => t.StartDate)
                .ToListAsync();

            var query = Context.Registrations
                .Include(r => r.Player)
                .Include(r => r.Tournament)
                .Where(r => r.Tournament != null && r.Tournament.GolfAssociationId == CurrentAssociation.Id);

            if (tournamentId.HasValue)
            {
                query = query.Where(r => r.TournamentId == tournamentId.Value);
            }

            Registrations = await query
                .OrderByDescending(r => r.RegistrationDate)
                .ToListAsync();

            return Page();
        }
    }
}
