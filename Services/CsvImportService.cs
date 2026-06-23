using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace GolfAssociationCommunity.Services
{
    public class ImportResult
    {
        public string Type { get; set; } = "";
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public int TotalRows => Inserted + Updated + Skipped + Errors.Count;
    }

    public interface ICsvImportService
    {
        Task<ImportResult> ImportPlayersAsync(Stream csv, int associationId);
        Task<ImportResult> ImportScoresAsync(Stream csv, int associationId);
        Task<ImportResult> ImportRegistrationsAsync(Stream csv, int associationId);
        Task<ImportResult> ImportLeaderboardAsync(Stream csv, int associationId);
        string GetTemplate(string type);
    }

    public class CsvImportService : ICsvImportService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILeaderboardService _leaderboardService;

        public CsvImportService(ApplicationDbContext db, ILeaderboardService leaderboardService)
        {
            _db = db;
            _leaderboardService = leaderboardService;
        }

        // ── Players ──────────────────────────────────────────────────────────

        public async Task<ImportResult> ImportPlayersAsync(Stream csv, int associationId)
        {
            var result = new ImportResult { Type = "Players" };
            var (_, rows, parseError) = ParseCsv(csv);
            if (parseError != null) { result.Errors.Add(parseError); return result; }

            var existing = await _db.AssociationPlayers
                .Where(p => p.GolfAssociationId == associationId)
                .ToListAsync();
            var byEmail = existing.ToDictionary(p => p.Email.ToLowerInvariant());

            var tournaments = await _db.Tournaments
                .Where(t => t.GolfAssociationId == associationId)
                .ToListAsync();
            var tournamentsByName = tournaments.ToDictionary(t => t.Name.ToLowerInvariant());

            var now = DateTime.UtcNow;
            // Track (player, tournamentId) pairs that need registrations
            var registrationPairs = new List<(AssociationPlayer Player, int TournamentId, string? Flight)>();

            foreach (var (row, i) in rows.Select((r, i) => (r, i + 2)))
            {
                var name = Get(row, "DisplayName", "Name", "FullName");
                var email = Get(row, "Email");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
                {
                    result.Errors.Add($"Row {i}: DisplayName and Email are required.");
                    continue;
                }

                var handicapRaw = Get(row, "HandicapIndex", "Handicap");
                decimal? handicap = decimal.TryParse(handicapRaw, out var h) ? h : null;
                var isActiveRaw = Get(row, "IsActive", "Active");
                bool isActive = string.IsNullOrWhiteSpace(isActiveRaw) || isActiveRaw.Equals("true", StringComparison.OrdinalIgnoreCase) || isActiveRaw == "1";
                var tname = Get(row, "TournamentName", "Tournament");
                var flight = Get(row, "Flight");

                var key = email.ToLowerInvariant();
                AssociationPlayer player;
                if (byEmail.TryGetValue(key, out var existingPlayer))
                {
                    existingPlayer.DisplayName = name;
                    existingPlayer.HandicapIndex = handicap;
                    existingPlayer.IsActive = isActive;
                    existingPlayer.UpdatedAt = now;
                    result.Updated++;
                    player = existingPlayer;
                }
                else
                {
                    var newPlayer = new AssociationPlayer
                    {
                        GolfAssociationId = associationId,
                        DisplayName = name,
                        Email = email.Trim(),
                        HandicapIndex = handicap,
                        IsActive = isActive,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _db.AssociationPlayers.Add(newPlayer);
                    byEmail[key] = newPlayer;
                    result.Inserted++;
                    player = newPlayer;
                }

                if (!string.IsNullOrWhiteSpace(tname) && tournamentsByName.TryGetValue(tname.ToLowerInvariant(), out var tournament))
                    registrationPairs.Add((player, tournament.Id, flight));
            }
            await _db.SaveChangesAsync();

            // Auto-register players for specified tournaments
            if (registrationPairs.Count > 0)
            {
                var tournamentIds = registrationPairs.Select(p => p.TournamentId).Distinct().ToList();
                var existingRegs = await _db.Registrations
                    .Where(r => tournamentIds.Contains(r.TournamentId) && r.AssociationPlayerId != null)
                    .ToListAsync();
                var regIndex = existingRegs.ToLookup(r => $"{r.TournamentId}:{r.AssociationPlayerId}");

                int registered = 0;
                foreach (var (rPlayer, tId, flight) in registrationPairs)
                {
                    if (rPlayer.Id == 0) continue; // EF hasn't assigned Id yet — shouldn't happen after SaveChanges
                    var regKey = $"{tId}:{rPlayer.Id}";
                    var existing2 = regIndex[regKey].FirstOrDefault();
                    if (existing2 != null)
                    {
                        if (existing2.Status != RegistrationStatus.Registered)
                        {
                            existing2.Status = RegistrationStatus.Registered;
                            existing2.UpdatedAt = now;
                        }
                        if (!string.IsNullOrWhiteSpace(flight)) existing2.Flight = flight;
                    }
                    else
                    {
                        _db.Registrations.Add(new Registration
                        {
                            TournamentId = tId,
                            AssociationPlayerId = rPlayer.Id,
                            Status = RegistrationStatus.Registered,
                            PaymentConfirmed = true,
                            RegistrationDate = now,
                            UpdatedAt = now,
                            Flight = string.IsNullOrWhiteSpace(flight) ? null : flight
                        });
                        registered++;
                    }
                }
                await _db.SaveChangesAsync();
                if (registered > 0)
                    result.Warnings.Add($"Auto-registered {registered} player(s) for tournament(s).");
            }
            return result;
        }

        // ── Scores ───────────────────────────────────────────────────────────

        public async Task<ImportResult> ImportScoresAsync(Stream csv, int associationId)
        {
            var result = new ImportResult { Type = "Scores" };
            var (_, rows, parseError) = ParseCsv(csv);
            if (parseError != null) { result.Errors.Add(parseError); return result; }

            var tournaments = await _db.Tournaments
                .Where(t => t.GolfAssociationId == associationId)
                .ToListAsync();
            var players = await _db.AssociationPlayers
                .Where(p => p.GolfAssociationId == associationId)
                .ToListAsync();
            var playersByEmail = players.ToDictionary(p => p.Email.ToLowerInvariant());
            var tournamentsByName = tournaments.ToDictionary(t => t.Name.ToLowerInvariant());
            var existingScores = await _db.PlayerScores
                .Where(s => s.Tournament!.GolfAssociationId == associationId)
                .ToListAsync();
            var scoreIndex = existingScores
                .ToLookup(s => $"{s.TournamentId}:{s.AssociationPlayerId}:{s.Round}:{s.HoleNumber}");

            var affectedTournaments = new HashSet<int>();
            var now = DateTime.UtcNow;

            // Auto-create any players not yet in this association
            {
                var neededEmails = rows
                    .Select(r => Get(r, "PlayerEmail", "Email")?.Trim().ToLowerInvariant())
                    .Where(e => !string.IsNullOrWhiteSpace(e) && !playersByEmail.ContainsKey(e!))
                    .Distinct()
                    .ToList();

                if (neededEmails.Count > 0)
                {
                    var nameByEmail = rows
                        .Where(r => !string.IsNullOrWhiteSpace(Get(r, "PlayerEmail", "Email")))
                        .GroupBy(r => Get(r, "PlayerEmail", "Email")!.Trim().ToLowerInvariant())
                        .ToDictionary(g => g.Key, g => Get(g.First(), "PlayerName", "Name") ?? g.Key);

                    foreach (var email in neededEmails)
                    {
                        var newPlayer = new AssociationPlayer
                        {
                            GolfAssociationId = associationId,
                            DisplayName = nameByEmail.GetValueOrDefault(email, email),
                            Email = email,
                            IsActive = true,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        _db.AssociationPlayers.Add(newPlayer);
                        playersByEmail[email] = newPlayer;
                    }
                    await _db.SaveChangesAsync();
                    result.Warnings.Add($"Auto-created {neededEmails.Count} player(s) not found in this association.");
                }
            }

            foreach (var (row, i) in rows.Select((r, i) => (r, i + 2)))
            {
                // Resolve tournament
                Tournament? tournament = null;
                var tidRaw = Get(row, "TournamentId");
                var tname = Get(row, "TournamentName", "Tournament");
                if (int.TryParse(tidRaw, out var tid))
                    tournament = tournaments.FirstOrDefault(t => t.Id == tid);
                else if (!string.IsNullOrWhiteSpace(tname))
                    tournamentsByName.TryGetValue(tname.ToLowerInvariant(), out tournament);
                if (tournament == null) { result.Errors.Add($"Row {i}: Tournament not found ('{tname ?? tidRaw}')."); continue; }

                // Resolve player
                var playerEmail = Get(row, "PlayerEmail", "Email");
                if (!playersByEmail.TryGetValue(playerEmail?.ToLowerInvariant() ?? "", out var player))
                {
                    result.Errors.Add($"Row {i}: Player email missing or invalid ('{playerEmail}').");
                    continue;
                }

                if (!int.TryParse(Get(row, "Round"), out var round)) round = 1;
                if (!int.TryParse(Get(row, "HoleNumber", "Hole"), out var hole))
                { result.Errors.Add($"Row {i}: HoleNumber is required."); continue; }
                if (!int.TryParse(Get(row, "Score"), out var score))
                { result.Errors.Add($"Row {i}: Score is required."); continue; }
                if (!int.TryParse(Get(row, "HolePar", "Par"), out var par)) par = 4;
                if (!int.TryParse(Get(row, "HandicapStrokes"), out var hdcpStrokes)) hdcpStrokes = 0;
                int.TryParse(Get(row, "TiebreakerHoleHandicap"), out var tiebreakerHole);

                var scoreKey = $"{tournament.Id}:{player.Id}:{round}:{hole}";
                var existing2 = scoreIndex[scoreKey].FirstOrDefault();
                if (existing2 != null)
                {
                    existing2.Score = score;
                    existing2.HolePar = par;
                    existing2.HandicapStrokes = hdcpStrokes;
                    if (tiebreakerHole > 0) existing2.TiebreakerHoleHandicap = tiebreakerHole;
                    existing2.UpdatedAt = now;
                    result.Updated++;
                }
                else
                {
                    _db.PlayerScores.Add(new PlayerScore
                    {
                        TournamentId = tournament.Id,
                        AssociationPlayerId = player.Id,
                        Round = round,
                        HoleNumber = hole,
                        Score = score,
                        HolePar = par,
                        HandicapStrokes = hdcpStrokes,
                        TiebreakerHoleHandicap = tiebreakerHole > 0 ? tiebreakerHole : null,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                    result.Inserted++;
                }
                affectedTournaments.Add(tournament.Id);
            }
            await _db.SaveChangesAsync();

            // Auto-register players who have scores but no Registered registration,
            // and upsert round-total (HoleNumber=0) entries so the leaderboard calculator
            // can sum them correctly.
            foreach (var tournamentId in affectedTournaments)
            {
                // Load all non-tiebreaker, non-total hole scores for round-total computation
                var holeScores = await _db.PlayerScores
                    .Where(s => s.TournamentId == tournamentId && s.HoleNumber > 0)
                    .ToListAsync();

                // Ensure a Registered registration for every player that has ANY score entry
                var scoredPlayerIds = await _db.PlayerScores
                    .Where(s => s.TournamentId == tournamentId)
                    .Select(s => s.AssociationPlayerId)
                    .Distinct()
                    .ToListAsync();
                var existingRegs = await _db.Registrations
                    .Where(r => r.TournamentId == tournamentId && r.AssociationPlayerId != null)
                    .ToListAsync();
                var registeredPlayerIds = existingRegs.Select(r => r.AssociationPlayerId!.Value).ToHashSet();

                foreach (var pid in scoredPlayerIds)
                {
                    if (!registeredPlayerIds.Contains(pid))
                    {
                        _db.Registrations.Add(new Registration
                        {
                            TournamentId = tournamentId,
                            AssociationPlayerId = pid,
                            Status = RegistrationStatus.Registered,
                            PaymentConfirmed = true,
                            RegistrationDate = now,
                            UpdatedAt = now
                        });
                        registeredPlayerIds.Add(pid);
                    }
                    else
                    {
                        // Ensure existing registration is in Registered status
                        var reg = existingRegs.First(r => r.AssociationPlayerId == pid);
                        if (reg.Status != RegistrationStatus.Registered)
                        {
                            reg.Status = RegistrationStatus.Registered;
                            reg.UpdatedAt = now;
                        }
                    }
                }
                await _db.SaveChangesAsync();

                // Upsert round-total (HoleNumber=0) entries by summing hole scores per player+round
                var roundTotals = holeScores
                    .GroupBy(s => new { s.AssociationPlayerId, s.Round })
                    .Select(g => new { g.Key.AssociationPlayerId, g.Key.Round, Total = g.Sum(s => s.Score) });

                var existingTotals = await _db.PlayerScores
                    .Where(s => s.TournamentId == tournamentId && s.HoleNumber == PlayerScore.RoundTotalEntryHoleNumber)
                    .ToListAsync();
                var totalIndex = existingTotals.ToDictionary(s => $"{s.AssociationPlayerId}:{s.Round}");

                foreach (var rt in roundTotals)
                {
                    var key = $"{rt.AssociationPlayerId}:{rt.Round}";
                    if (totalIndex.TryGetValue(key, out var existing3))
                    {
                        existing3.Score = rt.Total;
                        existing3.UpdatedAt = now;
                    }
                    else
                    {
                        _db.PlayerScores.Add(new PlayerScore
                        {
                            TournamentId = tournamentId,
                            AssociationPlayerId = rt.AssociationPlayerId,
                            Round = rt.Round,
                            HoleNumber = PlayerScore.RoundTotalEntryHoleNumber,
                            Score = rt.Total,
                            CreatedAt = now,
                            UpdatedAt = now
                        });
                    }
                }
                await _db.SaveChangesAsync();
            }

            // Recalculate leaderboards for affected tournaments
            foreach (var tournamentId in affectedTournaments)
            {
                try { await _leaderboardService.RecalculateLeaderboardAsync(tournamentId); }
                catch { result.Warnings.Add($"Leaderboard recalculation failed for tournament ID {tournamentId}."); }
            }
            return result;
        }

        // ── Registrations ────────────────────────────────────────────────────

        public async Task<ImportResult> ImportRegistrationsAsync(Stream csv, int associationId)
        {
            var result = new ImportResult { Type = "Registrations" };
            var (_, rows, parseError) = ParseCsv(csv);
            if (parseError != null) { result.Errors.Add(parseError); return result; }

            var tournaments = await _db.Tournaments
                .Where(t => t.GolfAssociationId == associationId)
                .ToListAsync();
            var players = await _db.AssociationPlayers
                .Where(p => p.GolfAssociationId == associationId)
                .ToListAsync();
            var playersByEmail = players.ToDictionary(p => p.Email.ToLowerInvariant());
            var tournamentsByName = tournaments.ToDictionary(t => t.Name.ToLowerInvariant());
            var existing = await _db.Registrations
                .Where(r => r.Tournament!.GolfAssociationId == associationId && r.AssociationPlayerId != null)
                .ToListAsync();
            var regIndex = existing.ToDictionary(r => $"{r.TournamentId}:{r.AssociationPlayerId}");
            var now = DateTime.UtcNow;

            // Auto-create any players not yet in this association
            {
                var neededEmails = rows
                    .Select(r => Get(r, "PlayerEmail", "Email")?.Trim().ToLowerInvariant())
                    .Where(e => !string.IsNullOrWhiteSpace(e) && !playersByEmail.ContainsKey(e!))
                    .Distinct()
                    .ToList();

                if (neededEmails.Count > 0)
                {
                    var nameByEmail = rows
                        .Where(r => !string.IsNullOrWhiteSpace(Get(r, "PlayerEmail", "Email")))
                        .GroupBy(r => Get(r, "PlayerEmail", "Email")!.Trim().ToLowerInvariant())
                        .ToDictionary(g => g.Key, g => Get(g.First(), "PlayerName", "GuestName", "Name") ?? g.Key);

                    foreach (var email in neededEmails)
                    {
                        var newPlayer = new AssociationPlayer
                        {
                            GolfAssociationId = associationId,
                            DisplayName = nameByEmail.GetValueOrDefault(email, email),
                            Email = email,
                            IsActive = true,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        _db.AssociationPlayers.Add(newPlayer);
                        playersByEmail[email] = newPlayer;
                    }
                    await _db.SaveChangesAsync();
                    result.Warnings.Add($"Auto-created {neededEmails.Count} player(s) not found in this association.");
                }
            }

            foreach (var (row, i) in rows.Select((r, i) => (r, i + 2)))
            {
                Tournament? tournament = null;
                var tname = Get(row, "TournamentName", "Tournament");
                var tidRaw = Get(row, "TournamentId");
                if (int.TryParse(tidRaw, out var tid))
                    tournament = tournaments.FirstOrDefault(t => t.Id == tid);
                else if (!string.IsNullOrWhiteSpace(tname))
                    tournamentsByName.TryGetValue(tname.ToLowerInvariant(), out tournament);
                if (tournament == null) { result.Errors.Add($"Row {i}: Tournament not found."); continue; }

                var playerEmail = Get(row, "PlayerEmail", "Email");
                playersByEmail.TryGetValue(playerEmail?.ToLowerInvariant() ?? "", out var player);

                var guestName = Get(row, "PlayerName", "GuestName", "Name");
                var guestEmail = Get(row, "GuestEmail") ?? playerEmail ?? "";

                decimal? handicap = decimal.TryParse(Get(row, "Handicap", "HandicapIndex"), out var hv) ? hv : null;
                var flight = Get(row, "Flight") ?? "";
                var statusRaw = Get(row, "Status") ?? "Registered";
                var status = Enum.TryParse<RegistrationStatus>(statusRaw, true, out var s) ? s : RegistrationStatus.Registered;

                var regKey = player != null ? $"{tournament.Id}:{player.Id}" : null;
                Registration? reg = regKey != null ? regIndex.GetValueOrDefault(regKey) : null;

                if (reg != null)
                {
                    reg.Flight = string.IsNullOrWhiteSpace(flight) ? reg.Flight : flight;
                    reg.Status = status;
                    if (handicap.HasValue) reg.Handicap = handicap;
                    reg.UpdatedAt = now;
                    result.Updated++;
                }
                else
                {
                    var newReg = new Registration
                    {
                        TournamentId = tournament.Id,
                        AssociationPlayerId = player?.Id,
                        GuestName = player != null ? player.DisplayName : (guestName ?? ""),
                        GuestEmail = player != null ? player.Email : guestEmail,
                        Handicap = handicap,
                        Flight = flight,
                        Status = status,
                        RegistrationDate = now,
                        UpdatedAt = now,
                        RegistrationFee = tournament.EntryFee
                    };
                    _db.Registrations.Add(newReg);
                    result.Inserted++;
                }
            }
            await _db.SaveChangesAsync();
            return result;
        }

        // ── Leaderboard ──────────────────────────────────────────────────────

        public async Task<ImportResult> ImportLeaderboardAsync(Stream csv, int associationId)
        {
            var result = new ImportResult { Type = "Leaderboard" };
            var (_, rows, parseError) = ParseCsv(csv);
            if (parseError != null) { result.Errors.Add(parseError); return result; }

            var tournaments = await _db.Tournaments
                .Where(t => t.GolfAssociationId == associationId)
                .ToListAsync();
            var players = await _db.AssociationPlayers
                .Where(p => p.GolfAssociationId == associationId)
                .ToListAsync();
            var playersByEmail = players.ToDictionary(p => p.Email.ToLowerInvariant());
            var tournamentsByName = tournaments.ToDictionary(t => t.Name.ToLowerInvariant());
            var existing = await _db.Leaderboards
                .Where(l => l.Tournament!.GolfAssociationId == associationId)
                .ToListAsync();
            var lbIndex = existing.ToDictionary(l => $"{l.TournamentId}:{l.AssociationPlayerId}");
            var now = DateTime.UtcNow;

            foreach (var (row, i) in rows.Select((r, i) => (r, i + 2)))
            {
                Tournament? tournament = null;
                var tname = Get(row, "TournamentName", "Tournament");
                var tidRaw = Get(row, "TournamentId");
                if (int.TryParse(tidRaw, out var tid))
                    tournament = tournaments.FirstOrDefault(t => t.Id == tid);
                else if (!string.IsNullOrWhiteSpace(tname))
                    tournamentsByName.TryGetValue(tname.ToLowerInvariant(), out tournament);
                if (tournament == null) { result.Errors.Add($"Row {i}: Tournament not found."); continue; }

                var playerEmail = Get(row, "PlayerEmail", "Email");
                if (!playersByEmail.TryGetValue(playerEmail?.ToLowerInvariant() ?? "", out var player))
                { result.Errors.Add($"Row {i}: Player not found ('{playerEmail}')."); continue; }

                if (!int.TryParse(Get(row, "Position", "Pos"), out var pos)) pos = 0;
                if (!int.TryParse(Get(row, "TotalScore", "Score"), out var total)) total = 0;
                if (!int.TryParse(Get(row, "ScoreDifferential", "Differential"), out var diff)) diff = 0;
                var flight = Get(row, "Flight") ?? "";

                var lbKey = $"{tournament.Id}:{player.Id}";
                if (lbIndex.TryGetValue(lbKey, out var lb))
                {
                    lb.Position = pos;
                    lb.TotalScore = total;
                    lb.ScoreDifferential = diff;
                    if (!string.IsNullOrWhiteSpace(flight)) lb.Flight = flight;
                    lb.UpdatedAt = now;
                    result.Updated++;
                }
                else
                {
                    _db.Leaderboards.Add(new Leaderboard
                    {
                        TournamentId = tournament.Id,
                        AssociationPlayerId = player.Id,
                        Position = pos,
                        TotalScore = total,
                        ScoreDifferential = diff,
                        Flight = string.IsNullOrWhiteSpace(flight) ? null : flight,
                        UpdatedAt = now
                    });
                    result.Inserted++;
                }
            }
            await _db.SaveChangesAsync();
            return result;
        }

        // ── Templates ────────────────────────────────────────────────────────

        public string GetTemplate(string type) => type.ToLowerInvariant() switch
        {
            "players" => "DisplayName,Email,HandicapIndex,IsActive,TournamentName,Flight\nJohn Smith,john@example.com,12.5,true,2026 Championship,A\nJane Doe,jane@example.com,,true,,",
            "scores" => "TournamentName,PlayerEmail,Round,HoleNumber,Score,HolePar,HandicapStrokes,TiebreakerHoleHandicap\n2026 Tournament,john@example.com,1,0,85,72,5,\n2026 Tournament,john@example.com,1,1,4,4,1,",
            "registrations" => "TournamentName,PlayerEmail,PlayerName,Handicap,Flight,Status\n2026 Tournament,john@example.com,John Smith,12.5,A,Registered\n2026 Tournament,jane@example.com,Jane Doe,18,B,Registered",
            "leaderboard" => "TournamentName,PlayerEmail,Position,TotalScore,ScoreDifferential,Flight\n2026 Tournament,john@example.com,1,72,0,A\n2026 Tournament,jane@example.com,2,75,3,A",
            _ => ""
        };

        // ── CSV Parser ───────────────────────────────────────────────────────

        private static (string[] headers, List<Dictionary<string, string>> rows, string? error) ParseCsv(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var lines = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
                lines.Add(line);

            if (lines.Count == 0)
                return ([], [], "The file is empty.");

            var headers = SplitCsvLine(lines[0]).Select(h => h.Trim()).ToArray();
            if (headers.Length == 0)
                return ([], [], "No headers found in the first row.");

            var rows = new List<Dictionary<string, string>>();
            for (int i = 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var values = SplitCsvLine(lines[i]);
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < headers.Length; j++)
                    row[headers[j]] = j < values.Length ? values[j].Trim() : "";
                rows.Add(row);
            }
            return (headers, rows, null);
        }

        private static string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    { current.Append('"'); i++; }
                    else inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                { result.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        private static string? Get(Dictionary<string, string> row, params string[] keys)
        {
            foreach (var k in keys)
                if (row.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
            return null;
        }
    }
}
