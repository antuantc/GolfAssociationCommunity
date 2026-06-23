using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class TournamentDataModel : AssociationAdminPageModel
    {
        public TournamentDataModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
            : base(userManager, context) { }

        [BindProperty(SupportsGet = true)]
        public int? TournamentId { get; set; }

        public List<Tournament> Tournaments { get; private set; } = new();
        public Tournament? SelectedTournament { get; private set; }

        public int ScoreCount { get; private set; }
        public int RegistrationCount { get; private set; }
        public int LeaderboardCount { get; private set; }
        /// <summary>Players who have no data outside this tournament.</summary>
        public int OrphanPlayerCount { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;
            await LoadAsync();
            return Page();
        }

        // ── Delete scores ────────────────────────────────────────────────────

        public async Task<IActionResult> OnPostDeleteScoresAsync(int tournamentId)
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            if (!await OwnsTournamentAsync(tournamentId)) return NotFound();

            var scores = await Context.PlayerScores
                .Where(s => s.TournamentId == tournamentId)
                .ToListAsync();
            Context.PlayerScores.RemoveRange(scores);

            var lb = await Context.Leaderboards.Where(l => l.TournamentId == tournamentId).ToListAsync();
            Context.Leaderboards.RemoveRange(lb);

            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Deleted {scores.Count} score records and {lb.Count} leaderboard entries.";
            return RedirectToPage(new { TournamentId = tournamentId });
        }

        // ── Delete registrations ─────────────────────────────────────────────

        public async Task<IActionResult> OnPostDeleteRegistrationsAsync(int tournamentId)
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            if (!await OwnsTournamentAsync(tournamentId)) return NotFound();

            var regs = await Context.Registrations
                .Where(r => r.TournamentId == tournamentId)
                .ToListAsync();
            Context.Registrations.RemoveRange(regs);
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Deleted {regs.Count} registrations.";
            return RedirectToPage(new { TournamentId = tournamentId });
        }

        // ── Delete scores + registrations + leaderboard ──────────────────────

        public async Task<IActionResult> OnPostDeleteAllDataAsync(int tournamentId)
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            if (!await OwnsTournamentAsync(tournamentId)) return NotFound();

            var scores = await Context.PlayerScores.Where(s => s.TournamentId == tournamentId).ToListAsync();
            var lb     = await Context.Leaderboards.Where(l => l.TournamentId == tournamentId).ToListAsync();
            var regs   = await Context.Registrations.Where(r => r.TournamentId == tournamentId).ToListAsync();

            Context.PlayerScores.RemoveRange(scores);
            Context.Leaderboards.RemoveRange(lb);
            Context.Registrations.RemoveRange(regs);
            await Context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Reset tournament: deleted {scores.Count} scores, {lb.Count} leaderboard entries, and {regs.Count} registrations.";
            return RedirectToPage(new { TournamentId = tournamentId });
        }

        // ── Delete orphan players ────────────────────────────────────────────
        // Deletes association players whose ONLY tournament data was in this tournament.

        public async Task<IActionResult> OnPostDeleteOrphanPlayersAsync(int tournamentId)
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            if (!await OwnsTournamentAsync(tournamentId)) return NotFound();

            var assocId = CurrentAssociation.Id;

            // Player IDs that were ever tied to this tournament
            var tournamentPlayerIds = await Context.Registrations
                .Where(r => r.TournamentId == tournamentId && r.AssociationPlayerId != null)
                .Select(r => r.AssociationPlayerId!.Value)
                .Union(
                    Context.PlayerScores
                        .Where(s => s.TournamentId == tournamentId)
                        .Select(s => s.AssociationPlayerId))
                .Distinct()
                .ToListAsync();

            // Among those, find ones with NO other tournament data in this association
            var orphans = new List<int>();
            foreach (var pid in tournamentPlayerIds)
            {
                var hasOtherRegs = await Context.Registrations
                    .AnyAsync(r => r.AssociationPlayerId == pid
                        && r.TournamentId != tournamentId
                        && r.Tournament!.GolfAssociationId == assocId);
                var hasOtherScores = await Context.PlayerScores
                    .AnyAsync(s => s.AssociationPlayerId == pid
                        && s.TournamentId != tournamentId
                        && s.Tournament!.GolfAssociationId == assocId);
                if (!hasOtherRegs && !hasOtherScores)
                    orphans.Add(pid);
            }

            var players = await Context.AssociationPlayers
                .Where(p => orphans.Contains(p.Id) && p.GolfAssociationId == assocId)
                .ToListAsync();
            Context.AssociationPlayers.RemoveRange(players);
            await Context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Deleted {players.Count} player(s) with no remaining data in other tournaments.";
            return RedirectToPage(new { TournamentId = tournamentId });
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task<bool> OwnsTournamentAsync(int tournamentId)
        {
            return await Context.Tournaments.AnyAsync(t => t.Id == tournamentId && t.GolfAssociationId == CurrentAssociation.Id);
        }

        private async Task LoadAsync()
        {
            Tournaments = await Context.Tournaments
                .Where(t => t.GolfAssociationId == CurrentAssociation.Id)
                .OrderByDescending(t => t.StartDate)
                .ToListAsync();

            if (!TournamentId.HasValue) return;

            SelectedTournament = Tournaments.FirstOrDefault(t => t.Id == TournamentId.Value);
            if (SelectedTournament == null) return;

            ScoreCount        = await Context.PlayerScores.CountAsync(s => s.TournamentId == TournamentId.Value);
            RegistrationCount = await Context.Registrations.CountAsync(r => r.TournamentId == TournamentId.Value);
            LeaderboardCount  = await Context.Leaderboards.CountAsync(l => l.TournamentId == TournamentId.Value);

            // Count players whose only data is this tournament
            var assocId = CurrentAssociation.Id;
            var tPlayerIds = await Context.Registrations
                .Where(r => r.TournamentId == TournamentId.Value && r.AssociationPlayerId != null)
                .Select(r => r.AssociationPlayerId!.Value)
                .Union(Context.PlayerScores
                    .Where(s => s.TournamentId == TournamentId.Value)
                    .Select(s => s.AssociationPlayerId))
                .Distinct()
                .ToListAsync();

            int orphans = 0;
            foreach (var pid in tPlayerIds)
            {
                var hasOther = await Context.Registrations.AnyAsync(r => r.AssociationPlayerId == pid
                        && r.TournamentId != TournamentId.Value && r.Tournament!.GolfAssociationId == assocId)
                    || await Context.PlayerScores.AnyAsync(s => s.AssociationPlayerId == pid
                        && s.TournamentId != TournamentId.Value && s.Tournament!.GolfAssociationId == assocId);
                if (!hasOther) orphans++;
            }
            OrphanPlayerCount = orphans;
        }
    }
}
