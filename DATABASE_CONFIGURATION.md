# Database Configuration Guide

This guide explains how to configure the Golf Association Community application for different environments.

## Quick Start

### Development (SQLite — no server required)

```bash
cd GolfAssociationCommunity
dotnet ef database update
dotnet run
# SQLite database file (golfassociation.db) is created automatically
```

### Production (SQL Server)

```powershell
# Windows PowerShell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:DatabaseProvider = "SqlServer"
$env:ConnectionStrings__DefaultConnection = "Server=your-server;Database=GolfAssociation;User Id=user;Password=pass;Encrypt=true;TrustServerCertificate=false;"
dotnet ef database update
dotnet publish -c Release
```

---

## Configuration Methods (Precedence Order)

### 1. Environment Variables (highest priority)

```powershell
# Windows PowerShell
$env:DatabaseProvider = "SQLite"
$env:ConnectionStrings__DefaultConnection = "Data Source=golfassociation.db"
```

```bash
# Linux / macOS
export DatabaseProvider=SQLite
export ConnectionStrings__DefaultConnection="Data Source=golfassociation.db"
```

### 2. appsettings.json / appsettings.{Environment}.json

```json
{
  "DatabaseProvider": "SQLite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=golfassociation.db"
  }
}
```

---

## Connection String Examples

### SQLite (development default)
```json
"DefaultConnection": "Data Source=golfassociation.db"
```

### SQL Server (production)
```json
"DefaultConnection": "Server=myserver.database.windows.net;Database=GolfAssociation;User Id=sa;Password=...;Encrypt=true;TrustServerCertificate=false;"
```

### SQL Server (Windows auth)
```json
"DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=GolfAssociation;Integrated Security=true;TrustServerCertificate=true;"
```

---

## Per-Association Authorize.Net Credentials

Each association stores its own Authorize.Net API credentials in the database (`GolfAssociation.AuthorizeNetApiLoginId` / `GolfAssociation.AuthorizeNetTransactionKey` / `GolfAssociation.AuthorizeNetIsSandbox`). These are set in the Association Admin → Settings page.

A fallback global credential can be set in `appsettings.json`:

```json
"AuthorizeNet": {
  "ApiLoginId": "YOUR_GLOBAL_LOGIN_ID",
  "TransactionKey": "YOUR_GLOBAL_TRANSACTION_KEY",
  "IsSandbox": true
}
```

---

## Migration Commands

```bash
# Install EF CLI tool (one time)
dotnet tool install --global dotnet-ef

# Apply all pending migrations (creates the database on first run)
dotnet ef database update

# Create a new migration after model changes
dotnet ef migrations add <MigrationName>

# List all migrations and their status
dotnet ef migrations list

# Reset local development database (DESTRUCTIVE — dev only)
dotnet ef database drop --force
dotnet ef database update
```

---

## Current Migration History

| Migration | Description |
|-----------|-------------|
| `20260519184533_InitialCreate` | Initial schema |
| `20260520011137_AddAdminAuditEvents` | Admin audit trail |
| `20260520012522_AddRequirePasswordChangeFlag` | Force-change-password flag |
| `20260521174856_AddGuestTournamentRegistrations` | Guest (non-member) registrations |
| `20260521182807_AddRegistrationCardLast4` | Card last-4 on registration |
| `20260521183154_AddRegistrationBillingAddress` | Billing address on registration |
| `20260522023452_AddRegistrationHandicap` | Handicap captured at registration |
| `20260522025019_AddSponsorshipPackages` | Association sponsorship tiers |
| `20260522133445_AddSponsorshipPayments` | Sponsorship payment records |
| `20260522144015_AddAssociationThemeKey` | Per-association CSS theme key |
| `20260522204938_AddAssociationAuthorizeNetSettings` | Per-association payment credentials |
| `20260522214936_AddAssociationPlayers` | AssociationPlayer roster model |
| `20260522215602_RemoveLegacyIdentityPlayerLinks` | Clean up old player-identity join |
| `20260522221525_AddPlayerScoreTiebreakerHoleHandicap` | Tiebreaker hole handicap on scores |
| `20260523031418_AddAuthorizeNetRefundTracking` | Refund transaction ID tracking |
| `20260523034915_AddAssociationOfficers` | Officers & members section |
| *(subsequent)* | Additional schema refinements |

---

## Troubleshooting

### SQLite Error 1: no such table
The database file exists but migrations have not been applied.
```bash
dotnet ef database update
```

### `dotnet ef` not recognized
```bash
dotnet tool install --global dotnet-ef
```

### Build fails when running `dotnet ef`
EF CLI builds the project first. Fix any compile errors:
```bash
dotnet build
```

### Schema out of sync (dev)
Delete the database file and re-apply all migrations:
```bash
dotnet ef database drop --force
dotnet ef database update
```

### Missing tables after adding a new model
Create and apply a new migration:
```bash
dotnet ef migrations add AddMyNewEntity
dotnet ef database update
```
