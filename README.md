# Golf Association Community

A comprehensive ASP.NET Core web application for managing golf associations, tournaments, player registrations, sponsorships, and real-time leaderboards with integrated payment processing via Authorize.Net.

## Features

### 🏌️ Association Management
- Multi-tenant support for independent golf associations
- Association admin capabilities
- Member management and handicap tracking
- Association profile and branding

### 🏆 Tournament Management
- Multiple tournament formats (Stroke, Stableford, Best Ball, Scramble, Fourball)
- Tournament lifecycle management (Scheduled → Registration → In Progress → Completed)
- Tournament admins for data entry
- Venue and course information tracking
- Registration deadlines and slot management

### 📋 Player Registration
- Online tournament registration
- Integrated payment processing via Authorize.Net
- Registration status tracking (Pending, Registered, Withdrew, Disqualified)
- Duplicate registration prevention
- Payment confirmation and transaction tracking

### 💳 Payment Processing
- Authorize.Net integration for secure credit card processing
- Support for both registration fees and sponsorship payments
- Refund capability
- Payment confirmation and transaction ID tracking
- Comprehensive error handling and logging

### 📊 Scoring & Leaderboards
- Hole-by-hole score recording
- Support for multiple rounds
- Automatic Stableford point calculation
- Real-time leaderboard generation
- Score differential tracking
- Leaderboard position management

### 💰 Sponsorship Management
- Tiered sponsorship levels (Bronze, Silver, Gold, Platinum, Title)
- Tournament and association-level sponsorship
- Sponsor payment tracking via Authorize.Net
- Status workflow management

### 👤 User Management
- Extended ASP.NET Identity with golf-specific data
- Role-based authorization (Site Admin, Association Admin, Player)
- User profile with handicap tracking
- Address and contact information management

## Technology Stack

- **Framework**: ASP.NET Core 8.0
- **Database**: SQL Server with Entity Framework Core
- **Authentication**: ASP.NET Identity
- **Payment Processing**: Authorize.Net
- **Logging**: Serilog
- **ORM**: Entity Framework Core

## Project Structure

```
GolfAssociationCommunity/
├── Models/
│   ├── ApplicationUser.cs
│   ├── GolfAssociation.cs
│   ├── Tournament.cs
│   ├── Registration.cs
│   ├── PlayerScore.cs
│   ├── Leaderboard.cs
│   └── Sponsorship.cs
├── Data/
│   └── ApplicationDbContext.cs
├── Services/
│   ├── AuthorizeNetPaymentService.cs
│   ├── AssociationService.cs
│   ├── TournamentService.cs
│   ├── RegistrationService.cs
│   ├── ScoreService.cs
│   └── LeaderboardService.cs
├── Program.cs
├── appsettings.json
└── GolfAssociationCommunity.csproj
```

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- SQL Server (LocalDB or full SQL Server)
- Authorize.Net merchant account

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/6Moos/GolfAssociationCommunity.git
   cd GolfAssociationCommunity
   ```

2. **Configure the database connection**
   
   Update `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=YOUR_SERVER;Database=GolfAssociationCommunity;Trusted_Connection=true;"
   }
   ```

3. **Configure Authorize.Net credentials**
   
   Update `appsettings.json`:
   ```json
   "AuthorizeNet": {
     "ApiLoginId": "YOUR_AUTHORIZE_NET_API_LOGIN_ID",
     "TransactionKey": "YOUR_AUTHORIZE_NET_TRANSACTION_KEY",
     "IsSandbox": true
   }
   ```

4. **Create and apply database migrations**
   ```bash
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

5. **Build and run the application**
   ```bash
   dotnet build
   dotnet run
   ```

   The application will be available at `https://localhost:5001`

## Database Schema

### Key Entities

- **ApplicationUser** - Extended Identity user with golf-specific fields
- **GolfAssociation** - Represents independent golf associations with members
- **Tournament** - Golf tournament with format, status, and registration tracking
- **Registration** - Player registration for tournaments with payment tracking
- **PlayerScore** - Hole-by-hole scoring with Stableford calculations
- **Leaderboard** - Real-time tournament rankings and standings
- **Sponsorship** - Tournament/association sponsorships with payment tracking

## API Services

### Payment Service
```csharp
Task<(bool Success, string TransactionId, string? ErrorMessage)> 
    ProcessRegistrationPaymentAsync(...)

Task<(bool Success, string TransactionId, string? ErrorMessage)> 
    ProcessSponsorshipPaymentAsync(...)

Task<(bool Success, string? ErrorMessage)> 
    RefundTransactionAsync(string transactionId, decimal amount)
```

### Association Service
```csharp
Task<GolfAssociation?> GetAssociationByIdAsync(int id)
Task<GolfAssociation> CreateAssociationAsync(GolfAssociation association)
Task<IEnumerable<ApplicationUser>> GetAssociationMembersAsync(int associationId)
Task<bool> AddMemberToAssociationAsync(int associationId, string userId)
```

### Tournament Service
```csharp
Task<Tournament?> GetTournamentByIdAsync(int id)
Task<IEnumerable<Tournament>> GetUpcomingTournamentsAsync(int associationId)
Task<Tournament> CreateTournamentAsync(Tournament tournament)
Task<bool> UpdateTournamentStatusAsync(int id, TournamentStatus newStatus)
```

### Registration Service
```csharp
Task<Registration?> GetPlayerTournamentRegistrationAsync(int tournamentId, string playerId)
Task<Registration> CreateRegistrationAsync(Registration registration)
Task<bool> ConfirmPaymentAsync(int id, string authorizeNetTransactionId)
Task<bool> WithdrawRegistrationAsync(int id, string reason)
```

### Score Service
```csharp
Task<PlayerScore> RecordScoreAsync(PlayerScore score)
Task<IEnumerable<PlayerScore>> GetPlayerTournamentScoresAsync(int tournamentId, string playerId)
Task<int> CalculateStablefordPointsAsync(int[] holeScores, int[] holePars, int[] handicapStrokes)
```

### Leaderboard Service
```csharp
Task<IEnumerable<Leaderboard>> GetTournamentLeaderboardAsync(int tournamentId)
Task UpdateLeaderboardAsync(int tournamentId)
Task<bool> RecalculateAllLeaderboardsAsync()
```

## Next Steps

1. **Create Controllers** - Implement API or MVC controllers for CRUD operations
2. **Build Views** - Create Razor Pages or Angular/React frontend
3. **Admin Dashboard** - Implement site admin and association admin dashboards
4. **Authentication UI** - Create login/registration pages
5. **Testing** - Add unit and integration tests
6. **Deployment** - Configure for Azure or your hosting platform

## Logging

The application uses Serilog for comprehensive logging:
- Console output in development
- File-based logging with daily rolling files
- Log files located in `logs/` directory
- Configurable log levels in `appsettings.json`

## Security Considerations

- Always use HTTPS in production
- Store Authorize.Net credentials in secure configuration (not in code)
- Implement rate limiting on payment endpoints
- Enable multi-factor authentication for admin accounts
- Regular security audits of payment processing
- GDPR compliance for user data storage

## Contributing

Contributions are welcome! Please follow standard GitHub fork → branch → PR workflow.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For issues, questions, or feature requests, please open an issue on the GitHub repository.

---

**Project Status**: Foundation Complete - Ready for Controller/UI Development
