# Golf Association Community

A multi-tenant ASP.NET Core web application for managing golf associations, tournaments, player registrations, sponsorships, and real-time leaderboards. Each association gets its own fully branded public site with per-association CSS theming. A central hub page aggregates stats across all associations.

## Features

### 🌐 Per-Association Public Sites
- Each association has a standalone public-facing website at `/Associations/Details/{id}`
- Bold, dark, sports-site aesthetic inspired by professional golf association websites
- 10 selectable CSS themes per association (links-classic, sunrise-tee, sand-wedge, ocean-fairway, pine-bogey, sunset-backnine, masters-azalea, st-andrews, desert-dunes, midnight-drivingrange)
- Dedicated sticky header with nav: HOME / TOURNAMENTS / RESULTS / REGISTER / SPONSORS / ABOUT / YEAR SCHEDULE
- Announcement topbar with current season display
- Footer with next upcoming tournament info

### 📊 Association Hub (Main Index)
- Central landing page listing all active associations
- Per-association stats cards: tournament count, upcoming count, next event
- Rollup stats bar: total associations, total tournaments, total players ranked
- Global cross-association leaderboard (top 10 players)

### 🏌️ Association Management
- Multi-tenant support for independent golf associations
- Association admin portal with full CRUD
- Association branding: logo, hero image/video, motto, tagline, colors, theme key
- Per-association Authorize.Net credentials
- Establishment year, city, state, location tracking
- Media gallery (videos and photos)
- Officers & members section
- Charity/cause section

### 🏆 Tournament Management
- Multiple tournament formats: Stroke, Stableford, Best Ball, Scramble, Fourball
- Tournament lifecycle: Scheduled → Registration Open → In Progress → Completed / Cancelled
- Flights support for tournament grouping
- Venue and golf course tracking
- Registration deadlines and max slot management
- Year schedule view per association

### 📋 Player Registration
- Online tournament registration (members and guests)
- Authorize.Net credit card payment processing at registration
- Guest registration with email-based deduplication
- Registration status tracking: Pending, Registered, Withdrew, Disqualified
- Billing address and card last-4 capture for receipts
- Handicap capture at registration time

### 💳 Payment Processing
- Authorize.Net integration: per-association API credentials
- Registration and sponsorship payment flows
- Refund tracking with original transaction ID reference
- Transaction history per association and per record
- Comprehensive error handling and Serilog logging

### 📈 Scoring & Leaderboards
- Hole-by-hole score recording with tiebreaker hole handicap
- Multiple rounds support
- Automatic Stableford point calculation
- Real-time leaderboard generation and recalculation
- Flight-based leaderboard grouping on association homepage
- Season standings: cross-tournament cumulative rankings per association
- Global leaderboard: cross-association player rankings

### 💰 Sponsorship Management
- Tiered sponsorship packages per association (Bronze, Silver, Gold, Platinum, Title)
- Tournament-level and association-level sponsorships
- Sponsor payment tracking via Authorize.Net
- Sponsor logo and website display on association homepage

### 👥 Player & Member Management
- `AssociationPlayer` model: tracks active players independently of Identity users
- Identity-based membership via `AssociationMembers` join table
- Role-based authorization: Site Admin, Association Admin, Tournament Admin, Player
- Handicap tracking per player
- Association officer roles and display

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core 8.0 Razor Pages |
| Database | SQLite (dev) / SQL Server (prod) with EF Core |
| Authentication | ASP.NET Core Identity |
| Payment Processing | Authorize.Net |
| Logging | Serilog (file sink, daily rolling) |
| ORM | Entity Framework Core 8 |
| CSS | Custom properties + 10 per-association themes |

## Project Structure

```
GolfAssociationCommunity/
├── Models/
│   ├── DomainModels.cs          # All domain entities and enums
│   └── BrandingThemes.cs        # Theme definitions
├── Data/
│   └── ApplicationDbContext.cs  # EF Core context, all configurations
├── Services/
│   ├── AssociationService.cs    # Association CRUD + membership
│   ├── TournamentService.cs     # Tournament management
│   ├── RegistrationService.cs   # Registration + payment workflow
│   ├── ScoreService.cs          # Score recording + Stableford
│   ├── LeaderboardService.cs    # Real-time standings + global/season rankings
│   ├── AuthorizeNetPaymentService.cs    # Payment + refund processing
│   ├── AuthorizeNetTransactionService.cs # Transaction history
│   ├── AdminAuditService.cs     # Admin action audit trail
│   ├── SmtpEmailSender.cs       # Email notifications
│   └── UploadSettings.cs        # File upload configuration
├── Pages/
│   ├── Index.cshtml             # Hub: all associations + rollup stats
│   ├── Associations/
│   │   ├── Details.cshtml       # Public association homepage (themed)
│   │   ├── Tournaments.cshtml   # Public tournament schedule
│   │   ├── Leaderboard.cshtml   # Public tournament leaderboard
│   │   └── Sponsor.cshtml       # Sponsor payment page
│   ├── Admin/                   # Site admin pages
│   └── AssociationAdmin/        # Association admin portal
├── Migrations/                  # EF Core migration history
├── wwwroot/
│   └── css/site.css             # Global styles + all pub-* theme classes
├── Program.cs
└── appsettings.json
```

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- `dotnet-ef` CLI tool
- Authorize.Net sandbox account (for payment testing)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/6Moos/GolfAssociationCommunity.git
   cd GolfAssociationCommunity
   ```

2. **Configure database** — `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Data Source=golfassociation.db"
   }
   ```

3. **Configure Authorize.Net** (can be set per-association in admin portal):
   ```json
   "AuthorizeNet": {
     "ApiLoginId": "YOUR_LOGIN_ID",
     "TransactionKey": "YOUR_TRANSACTION_KEY",
     "IsSandbox": true
   }
   ```

4. **Apply migrations**
   ```bash
   dotnet ef database update
   ```

5. **Run**
   ```bash
   dotnet run
   ```
   Available at `https://localhost:5001`

## Database Migrations

```bash
# Install EF CLI (once)
dotnet tool install --global dotnet-ef

# First run — apply all existing migrations
dotnet ef database update

# After changing a model — create + apply
dotnet ef migrations add <MigrationName>
dotnet ef database update

# Check status
dotnet ef migrations list

# Reset local dev database (destructive)
dotnet ef database drop --force
dotnet ef database update
```

## Theming

Each association can select one of 10 themes in the Association Admin → Branding page. The theme key is stored on `GolfAssociation.ThemeKey` and applied as `<body class="theme-{key} pub-site">` on public association pages.

Available themes:

| Key | Name |
|-----|------|
| `links-classic` | Links Classic (dark green) |
| `sunrise-tee` | Sunrise Tee (warm amber) |
| `sand-wedge` | Sand Wedge (tan/earth) |
| `ocean-fairway` | Ocean Fairway (teal/blue) |
| `pine-bogey` | Pine Bogey (forest green) |
| `sunset-backnine` | Sunset Back Nine (deep red) |
| `masters-azalea` | Masters Azalea (augusta green) |
| `st-andrews` | St Andrews (slate/grey) |
| `desert-dunes` | Desert Dunes (warm sand) |
| `midnight-drivingrange` | Midnight Driving Range (charcoal) |

## Key Service Interfaces

### AssociationService
```csharp
Task<GolfAssociation?> GetAssociationByIdAsync(int id)          // includes Players, Tournaments, Sponsors, Media...
Task<IEnumerable<GolfAssociation>> GetAllActiveAssociationsAsync()
Task<GolfAssociation> CreateAssociationAsync(GolfAssociation a)
Task<GolfAssociation?> UpdateAssociationAsync(int id, GolfAssociation a)
Task<bool> DeleteAssociationAsync(int id)
Task<IEnumerable<ApplicationUser>> GetAssociationMembersAsync(int id)
Task<bool> AddMemberToAssociationAsync(int associationId, string userId)
Task<bool> RemoveMemberFromAssociationAsync(int associationId, string userId)
Task<int> GetMemberCountAsync(int associationId)
```

### LeaderboardService
```csharp
Task<IEnumerable<Leaderboard>> GetTournamentLeaderboardAsync(int tournamentId)
Task<IEnumerable<AssociationLeaderboardRow>> GetAssociationLeaderboardAsync(int associationId)
Task<IEnumerable<RecentTournamentLeaderboard>> GetRecentTournamentLeaderboardsAsync(int associationId, int tournamentCount, int topN)
Task<IEnumerable<GlobalLeaderboardRow>> GetGlobalLeaderboardAsync(int topN)
Task UpdateLeaderboardAsync(int tournamentId)
Task<bool> RecalculateAllLeaderboardsAsync()
```

### RegistrationService
```csharp
Task<Registration?> GetRegistrationByIdAsync(int id)
Task<Registration?> GetGuestTournamentRegistrationAsync(int tournamentId, string guestEmail)
Task<Registration> CreateRegistrationAsync(Registration registration)
Task<bool> ConfirmPaymentAsync(int id, string transactionId)
Task<bool> WithdrawRegistrationAsync(int id, string reason)
Task<bool> CanGuestRegisterAsync(int tournamentId, string guestEmail)
```

## Troubleshooting

**SQLite Error 1: no such table**
```bash
dotnet ef database update
```

**Build fails on `dotnet ef` commands**
```bash
dotnet build   # fix any compile errors first
```

**Schema out of sync in development**
```bash
dotnet ef database drop --force
dotnet ef database update
```
