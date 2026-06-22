# Backend Implementation

## Status: Complete ✅

All backend services, data layer, and Razor Pages are implemented and building cleanly (0 errors, 0 warnings as of June 2026).

---

## Domain Models (`Models/DomainModels.cs`)

### Enums
| Enum | Values |
|------|--------|
| `RegistrationStatus` | Pending, Registered, Withdrew, Disqualified |
| `TournamentStatus` | Scheduled, RegistrationOpen, InProgress, Completed, Cancelled |
| `TournamentFormat` | Stroke, Stableford, BestBall, Scramble, Fourball |
| `MediaType` | Video, Photo |

### Core Entities
| Entity | Purpose |
|--------|---------|
| `ApplicationUser` | Extended Identity user with handicap, address, association membership |
| `GolfAssociation` | Multi-tenant association record with branding (ThemeKey, HeroImageUrl, Motto, etc.) |
| `AssociationPlayer` | Active player roster per association (independent of Identity users) |
| `AssociationOfficer` | Officer roles displayed on public homepage |
| `SponsorshipPackage` | Defined sponsorship tiers per association |
| `SponsorshipPayment` | Paid sponsorship records |
| `Tournament` | Tournament with format, flights, status, course, registration limits |
| `Registration` | Player tournament registration with payment tracking |
| `PlayerScore` | Hole-by-hole scores with Stableford points and tiebreaker handicap |
| `Leaderboard` | Real-time tournament standings row |
| `AssociationMedia` | Photo/video gallery items |
| `AssociationSponsor` | Sponsor display records |
| `AdminAuditEvent` | Admin action audit trail |
| `TournamentFlight` | Flight grouping for tournaments |

---

## Services (`Services/`)

### `AssociationService` (`IAssociationService`)
Full CRUD + membership operations. `GetAssociationByIdAsync` eagerly loads all navigation properties including Players, Tournaments, SponsorshipPackages, SponsorshipPayments, OfficersAndMembers, MediaItems, Sponsors.

```csharp
Task<GolfAssociation?> GetAssociationByIdAsync(int id)
Task<IEnumerable<GolfAssociation>> GetAllActiveAssociationsAsync()
Task<GolfAssociation> CreateAssociationAsync(GolfAssociation association)
Task<GolfAssociation?> UpdateAssociationAsync(int id, GolfAssociation association)
Task<bool> DeleteAssociationAsync(int id)
Task<IEnumerable<ApplicationUser>> GetAssociationMembersAsync(int associationId)
Task<bool> AddMemberToAssociationAsync(int associationId, string userId)
Task<bool> RemoveMemberFromAssociationAsync(int associationId, string userId)
Task<IEnumerable<Tournament>> GetAssociationTournamentsAsync(int associationId)
Task<int> GetMemberCountAsync(int associationId)
```

### `LeaderboardService` (`ILeaderboardService`)
Real-time standings, tiebreakers, season and global rankings.

```csharp
Task<IEnumerable<Leaderboard>> GetTournamentLeaderboardAsync(int tournamentId)
Task<Dictionary<int, List<int>>> GetTournamentTiebreakersAsync(int tournamentId)
Task<IEnumerable<AssociationLeaderboardRow>> GetAssociationLeaderboardAsync(int associationId)
Task<IEnumerable<RecentTournamentLeaderboard>> GetRecentTournamentLeaderboardsAsync(int associationId, int tournamentCount = 3, int topN = 5)
Task<IEnumerable<GlobalLeaderboardRow>> GetGlobalLeaderboardAsync(int topN = 10)
Task<Leaderboard?> GetPlayerLeaderboardPositionAsync(int tournamentId, int associationPlayerId)
Task UpdateLeaderboardAsync(int tournamentId)
Task<bool> RecalculateLeaderboardAsync(int tournamentId)
Task<bool> RecalculateAllLeaderboardsAsync()
```

Key DTOs:
- `RecentTournamentLeaderboard` — tournament metadata + `TopEntries` list with `Flight` property for grouping
- `AssociationLeaderboardRow` — season standings row (points, wins, tournaments played)
- `GlobalLeaderboardRow` — cross-association ranking (overall position, player name, wins, avg finish, total score)

### `RegistrationService` (`IRegistrationService`)
```csharp
Task<Registration?> GetRegistrationByIdAsync(int id)
Task<Registration?> GetGuestTournamentRegistrationAsync(int tournamentId, string guestEmail)
Task<IEnumerable<Registration>> GetTournamentRegistrationsAsync(int tournamentId)
Task<Registration> CreateRegistrationAsync(Registration registration)
Task<Registration?> UpdateRegistrationAsync(int id, Registration registration)
Task<bool> ConfirmPaymentAsync(int id, string transactionId)
Task<bool> WithdrawRegistrationAsync(int id, string reason)
Task<bool> CanGuestRegisterAsync(int tournamentId, string guestEmail)
Task<int> GetRegistrationCountAsync(int tournamentId, RegistrationStatus status)
```

### `ScoreService` (`IScoreService`)
```csharp
Task<PlayerScore?> GetScoreByIdAsync(int id)
Task<IEnumerable<PlayerScore>> GetPlayerScoresAsync(int tournamentId, int associationPlayerId)
Task<IEnumerable<PlayerScore>> GetTournamentScoresAsync(int tournamentId)
Task<PlayerScore> RecordScoreAsync(PlayerScore score)
```

### `AuthorizeNetPaymentService` (`IAuthorizeNetPaymentService`)
Per-association API credentials. Processes registration and sponsorship payments.

```csharp
Task<PaymentResult> ProcessPaymentAsync(int associationId, decimal amount, string cardNumber, string expirationDate, string cvv, PaymentBillingAddress billing)
Task<GatewayTransactionDetails?> GetTransactionDetailsAsync(int associationId, string transactionId)
Task<PaymentResult> RefundTransactionAsync(int associationId, string transactionId, decimal amount, string cardLast4)
```

### `AuthorizeNetTransactionService` (`IAuthorizeNetTransactionService`)
```csharp
Task<List<AuthorizeNetTransactionSummary>> GetTransactionsAsync(int? associationId = null)
Task<AuthorizeNetTransactionDetail?> GetTransactionAsync(string sourceType, int sourceId, int? associationId = null)
Task<PaymentResult> RefundTransactionAsync(string sourceType, int sourceId, decimal amount, int? associationId = null)
```

### `AdminAuditService` (`IAdminAuditService`)
```csharp
Task WriteAsync(string action, string actor, IDictionary<string, string?> details)
```

---

## Pages

### Public Pages (`Pages/Associations/`)
| Page | Purpose |
|------|---------|
| `Details.cshtml` | Full association homepage — hero, flight leaders, season standings, stats bar, upcoming events, latest result, gallery, officers, sponsors, join CTA |
| `Tournaments.cshtml` | Public tournament schedule list |
| `Leaderboard.cshtml` | Public tournament leaderboard |
| `Sponsor.cshtml` | Sponsor payment form |

`Details.cshtml.cs` exposes:
- `ActiveMembersCount` — from `AssociationPlayer` records where `IsActive = true`
- `CoursesPlayedCount` — distinct golf courses from completed tournaments
- `LatestResult` — most recent `RecentTournamentLeaderboard` for flight-grouped display
- `FlightLeaders` — `LatestResult.TopEntries` grouped by `Flight` (top 5 per flight)
- `SeasonStandings` — top 5 `AssociationLeaderboardRow` for the current season
- `RecentLeaderboards` — last 3 tournaments, top 20 entries each
- `ViewData["NextTournament*"]` — passed to `_Layout.cshtml` for footer display

### Hub Page (`Pages/Index.cshtml`)
- `AssociationCardStats` — per-association card DTO with `TournamentCount`, `UpcomingCount`, `NextTournament`
- `TotalTournaments` / `TotalAssociations` — rollup counters for stats bar
- `GlobalLeaderboard` — top 10 cross-association players

### Admin Pages (`Pages/Admin/`)
Site admin: associations, users, audit log, registrations, transactions, players.

### Association Admin Pages (`Pages/AssociationAdmin/`)
Association-scoped admin: tournaments, scores, leaderboards, members, players, sponsorships, branding, transactions.

---

## Layout (`Pages/Shared/_Layout.cshtml`)

Reads `ViewData` keys set by association pages:
- `PublicThemeKey` → CSS body class `theme-{key}`
- `PublicAssociationId`, `PublicAssociationName` → header branding
- `IsAssociationPage` → switches between `pub-site` layout and standard layout
- `NextTournamentName/Date/Course/Location/Id` → footer next-event display

Association pages get: `pub-topbar` (announcement) + `pub-header` (sticky nav) + `pub-main` + `pub-footer`.
All other pages get the original `site-header` + `container` + `site-footer`.

---

## CSS Architecture (`wwwroot/css/site.css`)

~44KB total. Original base styles preserved. New sections appended:

**Public site classes** (all prefixed `pub-`):
- Layout: `pub-main`, `pub-container`, `pub-section`, `pub-section--alt`
- Navigation: `pub-topbar`, `pub-header`, `pub-nav`, `pub-nav-toggle`
- Hero: `pub-hero`, `pub-hero__overlay`, `pub-hero__headline`, `pub-hero__flight-row`
- Buttons: `pub-btn`, `pub-btn--ghost`, `pub-btn--outline`, `pub-btn--sm`
- Typography: `pub-eyebrow`, `pub-section-title`, `pub-link`

**Section-specific classes**:
- `flight-section`, `flight-grid`, `flight-card`, `flight-table` — flight leaderboard display
- `season-section`, `player-card`, `player-avatar` — season standings
- `pub-stats-bar`, `pub-stat-item` — association stats display
- `schedule-section`, `schedule-card` — upcoming events
- `result-section`, `result-table` — latest tournament result
- `join-cta` — call-to-action section
- `pub-footer`, `pub-footer__grid` — 4-column footer

**Hub page classes** (prefixed `hub-`):
- `hub-stats-bar`, `hub-stat` — rollup stats
- `hub-assoc-grid`, `hub-assoc-card` — association cards with hover effects
- `hub-badge-upcoming` — upcoming tournament badge
