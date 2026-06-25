using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class PlayersModel : AssociationAdminPageModel
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxFileSizeBytes = 5 * 1024 * 1024;

        private readonly UploadSettings _uploads;

        public PlayersModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context, IOptions<UploadSettings> uploads)
            : base(userManager, context)
        {
            _uploads = uploads.Value;
        }

        [BindProperty(SupportsGet = true)]
        public int? EditId { get; set; }

        [BindProperty]
        public PlayerInput Input { get; set; } = new();

        [BindProperty]
        public IFormFile? Photo { get; set; }

        public List<PlayerRow> Players { get; private set; } = new();
        public List<PlayerRow> ArchivedPlayers { get; private set; } = new();
        public bool IsEditing => EditId.HasValue;

        public class PlayerInput
        {
            [Required]
            [StringLength(160)]
            public string DisplayName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            [StringLength(256)]
            public string Email { get; set; } = string.Empty;

            [Range(-10, 60)]
            public decimal? HandicapIndex { get; set; }

            public string? ExistingPhotoUrl { get; set; }
        }

        public class PlayerRow
        {
            public int Id { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public decimal? HandicapIndex { get; set; }
            public bool IsActive { get; set; }
            public string? PhotoUrl { get; set; }
            public int TournamentCount { get; set; }
            public int ScoreCount { get; set; }
            public int Wins { get; set; }
            public DateTime? LastScoreUpdateUtc { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            await LoadPageDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            if (!ModelState.IsValid)
            {
                await LoadPageDataAsync();
                return Page();
            }

            var normalizedEmail = Input.Email.Trim();
            var duplicate = await Context.AssociationPlayers
                .FirstOrDefaultAsync(player => player.GolfAssociationId == CurrentAssociation.Id
                    && player.Email.ToUpper() == normalizedEmail.ToUpper()
                    && (!EditId.HasValue || player.Id != EditId.Value));

            if (duplicate != null)
            {
                if (!duplicate.IsActive && !EditId.HasValue)
                {
                    duplicate.DisplayName = Input.DisplayName.Trim();
                    duplicate.Email = normalizedEmail;
                    duplicate.HandicapIndex = Input.HandicapIndex;
                    duplicate.IsActive = true;
                    duplicate.UpdatedAt = DateTime.UtcNow;

                    await Context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Existing player restored to the active roster.";
                    return RedirectToPage();
                }

                ModelState.AddModelError(nameof(Input.Email), "A player with this email already exists for the association.");
                await LoadPageDataAsync();
                return Page();
            }

            if (EditId.HasValue)
            {
                var existing = await Context.AssociationPlayers
                    .FirstOrDefaultAsync(player => player.Id == EditId.Value && player.GolfAssociationId == CurrentAssociation.Id);

                if (existing == null)
                {
                    return NotFound();
                }

                existing.DisplayName = Input.DisplayName.Trim();
                existing.Email = normalizedEmail;
                existing.HandicapIndex = Input.HandicapIndex;
                if (Photo != null)
                {
                    var ext = Path.GetExtension(Photo.FileName);
                    if (!AllowedExtensions.Contains(ext))
                    {
                        ModelState.AddModelError(nameof(Photo), "Only image files (jpg, png, gif, webp) are allowed.");
                        await LoadPageDataAsync();
                        return Page();
                    }
                    if (Photo.Length > MaxFileSizeBytes)
                    {
                        ModelState.AddModelError(nameof(Photo), "Photo must be under 5 MB.");
                        await LoadPageDataAsync();
                        return Page();
                    }
                    DeletePhoto(existing.PhotoUrl);
                    existing.PhotoUrl = await SavePhotoAsync(Photo);
                }
                existing.UpdatedAt = DateTime.UtcNow;
                TempData["SuccessMessage"] = "Player updated.";
                await Context.SaveChangesAsync();
                return RedirectToPage(null, null, (object?)null, "player-" + EditId.Value);
            }
            else
            {
                Context.AssociationPlayers.Add(new AssociationPlayer
                {
                    GolfAssociationId = CurrentAssociation.Id,
                    DisplayName = Input.DisplayName.Trim(),
                    Email = normalizedEmail,
                    HandicapIndex = Input.HandicapIndex,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                TempData["SuccessMessage"] = "Player added.";
            }

            await Context.SaveChangesAsync();
            return RedirectToPage(null, null, (object?)null, "active-players");
        }

        public async Task<IActionResult> OnPostDeletePhotoAsync(int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;

            var player = await Context.AssociationPlayers
                .FirstOrDefaultAsync(p => p.Id == id && p.GolfAssociationId == CurrentAssociation.Id);
            if (player == null) return NotFound();

            DeletePhoto(player.PhotoUrl);
            player.PhotoUrl = null;
            player.UpdatedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Photo removed.";
            return RedirectToPage(new { EditId = id });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            var player = await Context.AssociationPlayers
                .FirstOrDefaultAsync(item => item.Id == id && item.GolfAssociationId == CurrentAssociation.Id);

            if (player == null)
            {
                return NotFound();
            }

            Context.AssociationPlayers.Remove(player);
            TempData["SuccessMessage"] = "Player deleted from this association.";

            await Context.SaveChangesAsync();
            return RedirectToPage(null, null, (object?)null, "active-players");
        }

        public async Task<IActionResult> OnPostDeleteSelectedAsync(List<int> ids)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;

            if (ids == null || ids.Count == 0)
            {
                TempData["SuccessMessage"] = "No players selected.";
                return RedirectToPage();
            }

            var players = await Context.AssociationPlayers
                .Where(p => ids.Contains(p.Id) && p.GolfAssociationId == CurrentAssociation.Id)
                .ToListAsync();

            if (players.Count == 0)
            {
                TempData["SuccessMessage"] = "No matching players found.";
                return RedirectToPage();
            }

            Context.AssociationPlayers.RemoveRange(players);
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Deleted {players.Count} player(s) from this association.";
            return RedirectToPage(null, null, (object?)null, "active-players");
        }

        public async Task<IActionResult> OnPostArchiveAsync(int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;

            var player = await Context.AssociationPlayers
                .FirstOrDefaultAsync(p => p.Id == id && p.GolfAssociationId == CurrentAssociation.Id);
            if (player == null) return NotFound();

            player.IsActive = false;
            player.UpdatedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{player.DisplayName} has been archived.";
            return RedirectToPage(null, null, (object?)null, "active-players");
        }

        public async Task<IActionResult> OnPostActivateAsync(int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            var player = await Context.AssociationPlayers
                .FirstOrDefaultAsync(item => item.Id == id && item.GolfAssociationId == CurrentAssociation.Id);

            if (player == null)
            {
                return NotFound();
            }

            player.IsActive = true;
            player.UpdatedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Player restored to the active roster.";
            return RedirectToPage();
        }

        private async Task LoadPageDataAsync()
        {
            var players = await Context.AssociationPlayers
                .Where(player => player.GolfAssociationId == CurrentAssociation.Id)
                .OrderBy(player => player.DisplayName)
                .ThenBy(player => player.Email)
                .ToListAsync();

            if (EditId.HasValue)
            {
                var player = players.FirstOrDefault(item => item.Id == EditId.Value && item.IsActive);
                if (player != null)
                {
                    Input = new PlayerInput
                    {
                        DisplayName = player.DisplayName,
                        Email = player.Email,
                        HandicapIndex = player.HandicapIndex,
                        ExistingPhotoUrl = player.PhotoUrl
                    };
                }
                else
                {
                    EditId = null;
                }
            }

            var playerIds = players.Select(player => player.Id).ToList();

            var tournamentCounts = await Context.Registrations
                .Where(registration => registration.AssociationPlayerId != null
                    && playerIds.Contains(registration.AssociationPlayerId.Value)
                    && registration.Tournament != null
                    && registration.Tournament.GolfAssociationId == CurrentAssociation.Id
                    && registration.Status == RegistrationStatus.Registered)
                .GroupBy(registration => registration.AssociationPlayerId!.Value)
                .Select(group => new { AssociationPlayerId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.AssociationPlayerId, item => item.Count);

            var scoreCounts = await Context.PlayerScores
                .Where(score => playerIds.Contains(score.AssociationPlayerId)
                    && score.Tournament != null
                    && score.Tournament.GolfAssociationId == CurrentAssociation.Id)
                .GroupBy(score => score.AssociationPlayerId)
                .Select(group => new { AssociationPlayerId = group.Key, Count = group.Count(), LastUpdatedAt = group.Max(score => score.UpdatedAt) })
                .ToDictionaryAsync(item => item.AssociationPlayerId, item => new { item.Count, item.LastUpdatedAt });

            var wins = await Context.Leaderboards
                .Where(leaderboard => playerIds.Contains(leaderboard.AssociationPlayerId)
                    && leaderboard.Tournament != null
                    && leaderboard.Tournament.GolfAssociationId == CurrentAssociation.Id
                    && leaderboard.Position == 1)
                .GroupBy(leaderboard => leaderboard.AssociationPlayerId)
                .Select(group => new { AssociationPlayerId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.AssociationPlayerId, item => item.Count);

            var playerRows = players
                .Select(player => new PlayerRow
                {
                    Id = player.Id,
                    DisplayName = player.DisplayName,
                    Email = player.Email,
                    HandicapIndex = player.HandicapIndex,
                    IsActive = player.IsActive,
                    PhotoUrl = player.PhotoUrl,
                    TournamentCount = tournamentCounts.GetValueOrDefault(player.Id),
                    ScoreCount = scoreCounts.TryGetValue(player.Id, out var scoreData) ? scoreData.Count : 0,
                    Wins = wins.GetValueOrDefault(player.Id),
                    LastScoreUpdateUtc = scoreCounts.TryGetValue(player.Id, out scoreData) ? scoreData.LastUpdatedAt : null
                })
                .ToList();

            Players = playerRows
                .Where(player => player.IsActive)
                .ToList();

            ArchivedPlayers = playerRows
                .Where(player => !player.IsActive)
                .ToList();
        }

        private async Task<string?> SavePhotoAsync(IFormFile file)
        {
            var uploadsDir = Path.Combine(_uploads.PhysicalRoot, "players");
            Directory.CreateDirectory(uploadsDir);
            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{CurrentAssociation.Id}_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"{_uploads.RequestPath}/players/{fileName}";
        }

        private void DeletePhoto(string? photoUrl)
        {
            if (string.IsNullOrWhiteSpace(photoUrl)) return;
            var relative = photoUrl.StartsWith(_uploads.RequestPath)
                ? photoUrl.Substring(_uploads.RequestPath.Length).TrimStart('/')
                : photoUrl.TrimStart('/');
            var filePath = Path.Combine(_uploads.PhysicalRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
    }
}
