# Database Configuration Guide

This guide explains how to configure the Golf Association Community application for different database providers and environments.

## Table of Contents

1. [Quick Start](#quick-start)
2. [Configuration Methods](#configuration-methods)
3. [Development Setup](#development-setup)
4. [Production Setup](#production-setup)
5. [Environment-Specific Configurations](#environment-specific-configurations)
6. [Connection String Examples](#connection-string-examples)
7. [Troubleshooting](#troubleshooting)

---

## Quick Start

### Development (SQLite - No Server Required)

```bash
# 1. Ensure you're in the project directory
cd GolfAssociationCommunity

# 2. Create and apply migrations
dotnet ef database update

# 3. Run the application
dotnet run

# The database file (GolfAssociation.db) will be created automatically
```

### Production (SQL Server)

```bash
# 1. Set environment to Production
export ASPNETCORE_ENVIRONMENT=Production

# 2. Configure SQL Server connection
export DatabaseProvider=SqlServer
export ConnectionStrings__DefaultConnection="Server=your-server;Database=GolfAssociation;User Id=user;Password=pass;Encrypt=true;TrustServerCertificate=false;"

# 3. Run migrations
dotnet ef database update

# 4. Run the application
dotnet publish -c Release
```

---

## Configuration Methods

The application supports multiple configuration methods (in order of precedence):

### 1. Environment Variables (Highest Priority)

```bash
# Linux/macOS
export DatabaseProvider=SQLite
export ConnectionStrings__DefaultConnection="Data Source=GolfAssociation.db"

# Windows PowerShell
$env:DatabaseProvider = "SQLite"
$env:ConnectionStrings__DefaultConnection = "Data Source=GolfAssociation.db"

# Windows CMD
set DatabaseProvider=SQLite
set ConnectionStrings__DefaultConnection=Data Source=GolfAssociation.db
```

### 2. Configuration Files

Edit `appsettings.json`, `appsettings.Development.json`, or `appsettings.Production.json`:

```json
{
  "DatabaseProvider": "SQLite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=GolfAssociation.db"
  }
}
```

### 3. User Secrets (Development Only)

```bash
# Initialize secrets
dotnet user-secrets init

# Set database provider
dotnet user-secrets set "DatabaseProvider" "SQLite"

# Set connection string
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=GolfAssociation.db"

# List all secrets
dotnet user-secrets list
```

### 4. Command-Line Arguments

```bash
dotnet run --DatabaseProvider=SQLite
```

---

## Development Setup

### Using SQLite (Recommended for Local Development)

SQLite is the default for development and requires no additional setup.

**Configuration** (`appsettings.Development.json`):
```json
{
  "DatabaseProvider": "SQLite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=GolfAssociation.db"
  }
}
```

**Setup Steps:**
```bash
# 1. Build the project
dotnet build

# 2. Apply migrations (creates the database)
dotnet ef database update

# 3. Run the application
dotnet run

# Application will be available at https://localhost:5001
```

**Database File Location:**
- Created in the application root directory: `GolfAssociation.db`
- Backed up by copying the file to another location
- Cleared by deleting the file (next run will create a new one)

**Advantages:**
- ✅ No database server setup required
- ✅ Easy to reset (just delete the file)
- ✅ Perfect for offline development
- ✅ Minimal resource usage
- ✅ Easy to version control for testing scenarios

### Using SQL Server (Local Development Alternative)

If you want to test against SQL Server locally:

**Prerequisites:**
- SQL Server Express or LocalDB installed
- SQL Server Management Studio (SSMS) for database management

**Configuration** (`appsettings.Development.json`):
```json
{
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=GolfAssociationDev;Trusted_Connection=true;Encrypt=false;"
  }
}
```

**Setup Steps:**
```bash
# 1. Apply migrations
dotnet ef database update

# 2. Run the application
dotnet run
```

---

## Production Setup

### SQL Server (Recommended for Production)

**Prerequisites:**
- SQL Server instance (on-premises, Azure SQL Database, or managed hosting)
- Database credentials with appropriate permissions
- Network connectivity to the database server

**Configuration** (`appsettings.Production.json`):
```json
{
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "DefaultConnection": "Server=your-server.database.windows.net;Database=GolfAssociation;User Id=your_user@your_server;Password=your_password;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;"
  }
}
```

**Setup Steps:**

1. **Prepare the database server:**
   ```sql
   -- Create database user and login
   CREATE LOGIN golf_user WITH PASSWORD = 'YourSecurePassword123!';
   CREATE USER golf_user FOR LOGIN golf_user;
   
   -- Grant permissions to database
   ALTER ROLE db_owner ADD MEMBER golf_user;
   ```

2. **Set environment variables on your server:**
   ```bash
   export ASPNETCORE_ENVIRONMENT=Production
   export DatabaseProvider=SqlServer
   export ConnectionStrings__DefaultConnection="Server=your-server;Database=GolfAssociation;User Id=golf_user;Password=YourSecurePassword123!;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;"
   ```

3. **Apply migrations:**
   ```bash
   dotnet ef database update --context ApplicationDbContext
   ```

4. **Deploy the application:**
   ```bash
   dotnet publish -c Release
   # Run the published application
   ```

### Azure SQL Database

**Connection String Format:**
```
Server=tcp:yourserver.database.windows.net,1433;Initial Catalog=GolfAssociation;Persist Security Info=False;User ID=your_user@yourserver;Password=your_password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

**Configuration Example:**
```bash
export ASPNETCORE_ENVIRONMENT=Production
export DatabaseProvider=SqlServer
export ConnectionStrings__DefaultConnection="Server=tcp:myserver.database.windows.net,1433;Initial Catalog=GolfAssociation;Persist Security Info=False;User ID=golf_user@myserver;Password=MySecurePassword123!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

### SQLite in Production (Lightweight Deployments)

SQLite can be used in production for lighter workloads or edge deployments.

**⚠️ Limitations:**
- Single concurrent writer (limited concurrency)
- Not suitable for high-traffic applications
- File-based locking
- Best for <50 concurrent users

**Configuration:**
```json
{
  "DatabaseProvider": "SQLite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/data/GolfAssociation.db;Cache=Shared"
  }
}
```

**Setup Steps:**
```bash
# 1. Ensure data directory exists on the server
mkdir -p /data

# 2. Set proper permissions
chmod 755 /data

# 3. Apply migrations
dotnet ef database update

# 4. Run the application
dotnet run
```

**Backup Strategy:**
```bash
# Daily backup script
#!/bin/bash
BACKUP_DIR="/backups/golfassociation"
mkdir -p $BACKUP_DIR
cp /data/GolfAssociation.db $BACKUP_DIR/GolfAssociation_$(date +%Y%m%d_%H%M%S).db
# Keep only last 30 days
find $BACKUP_DIR -name "*.db" -mtime +30 -delete
```

---

## Environment-Specific Configurations

### Configuration Precedence

The application loads configurations in this order (each overrides previous):

1. `appsettings.json` (base configuration)
2. `appsettings.{Environment}.json` (e.g., Development, Production)
3. Environment variables
4. User secrets (development only)

### Supported Environments

```bash
# Development (default)
export ASPNETCORE_ENVIRONMENT=Development

# Staging
export ASPNETCORE_ENVIRONMENT=Staging

# Production
export ASPNETCORE_ENVIRONMENT=Production
```

### Example: Development Configuration

**appsettings.Development.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  },
  "DatabaseProvider": "SQLite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=GolfAssociation.db"
  }
}
```

### Example: Staging Configuration

**appsettings.Staging.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "DefaultConnection": "Server=staging-db.example.com;Database=GolfAssociation_Staging;User Id=staging_user;Password=staging_password;Encrypt=true;TrustServerCertificate=false;"
  }
}
```

### Example: Production Configuration

**appsettings.Production.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Error"
    }
  },
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "DefaultConnection": "Server=production-db.example.com;Database=GolfAssociation;User Id=prod_user;Password=prod_password;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;"
  }
}
```

---

## Connection String Examples

### SQLite

**Local Development:**
```
Data Source=GolfAssociation.db
```

**In-Memory (Testing Only):**
```
Data Source=:memory:
```

**Shared Cache:**
```
Data Source=GolfAssociation.db;Cache=Shared
```

### SQL Server

**Local/On-Premises:**
```
Server=myserver.com;Database=GolfAssociation;User Id=sa;Password=password;Encrypt=false;TrustServerCertificate=true;
```

**Windows Authentication (Local):**
```
Server=(localdb)\mssqllocaldb;Database=GolfAssociation;Trusted_Connection=true;Encrypt=false;
```

**Azure SQL Database:**
```
Server=tcp:myserver.database.windows.net,1433;Initial Catalog=GolfAssociation;Persist Security Info=False;User ID=user@myserver;Password=password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

**AWS RDS SQL Server:**
```
Server=myinstance.c9akciq32.us-east-1.rds.amazonaws.com;Database=GolfAssociation;User Id=admin;Password=password;Encrypt=true;TrustServerCertificate=false;
```

---

## Troubleshooting

### Issue: "No connection string found in configuration"

**Solution:**
```bash
# Verify appsettings.json exists and has connection string
cat appsettings.json | grep -A 2 ConnectionStrings

# Or check environment variables
echo $ConnectionStrings__DefaultConnection
```

### Issue: "Database file not found" (SQLite)

**Solution:**
```bash
# The file is created automatically on first run
# If it's not created, try:
dotnet ef database update --verbose

# Check file location
ls -la *.db
```

### Issue: "SQL Server connection timeout"

**Solution:**
```bash
# Verify SQL Server is running
telnet your-server 1433

# Check connection string format
echo $ConnectionStrings__DefaultConnection

# Test with sqlcmd
sqlcmd -S your-server -U your_user -P your_password -Q "SELECT @@VERSION"
```

### Issue: "The instance of entity type X cannot be tracked"

**Solution:** This usually indicates a database context configuration issue. Verify:
- Database provider is correctly set
- Connection string is valid
- Migrations have been applied: `dotnet ef database update`

### Issue: "SQLite disk image is malformed"

**Solution:**
```bash
# SQLite database may be corrupted. Backup and recreate:
mv GolfAssociation.db GolfAssociation.db.bak
dotnet ef database update
```

### Issue: Application starts but won't connect to database

**Solution:**
```bash
# Check logs for detailed error messages
# Verify database exists and is accessible
# Ensure proper permissions are set
# For SQL Server, verify firewall rules allow connection
```

---

## Security Best Practices

1. **Never commit passwords** to version control
   ```bash
   # Use environment variables or user secrets instead
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."
   ```

2. **Use encryption in production**
   ```
   Encrypt=true;TrustServerCertificate=false;
   ```

3. **Limit database user permissions**
   ```sql
   -- Create user with minimal permissions
   CREATE USER app_user FOR LOGIN app_user;
   ALTER ROLE db_datareader ADD MEMBER app_user;
   ALTER ROLE db_datawriter ADD MEMBER app_user;
   ```

4. **Enable database backups**
   - SQL Server: Use SQL Server Agent or managed backups
   - SQLite: Implement regular file backups

5. **Use strong passwords**
   - Minimum 12 characters
   - Mix of uppercase, lowercase, numbers, and symbols

---

## Additional Resources

- [Entity Framework Core Configuration](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- [ASP.NET Core Configuration Providers](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [SQLite Connection Strings](https://www.connectionstrings.com/sqlite/)
- [SQL Server Connection Strings](https://www.connectionstrings.com/sql-server/)
- [Azure SQL Database Connection Strings](https://learn.microsoft.com/en-us/azure/azure-sql/database/connection-strings-quickstart)
