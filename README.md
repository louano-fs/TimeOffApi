# Time Clock API

An ASP.NET Core API for employee clock-in/out, breaks, current status, time logs,
and daily/weekly summaries.

## Run locally

```powershell
dotnet restore
dotnet run --project TimeOffApi
```

The default database is the local SQLite file `timeclock.db`. Swagger UI is
available at `/swagger` in Development.

Development startup also seeds 50 employees, one administrator, and seven
pending time-off requests. Open `/seed-preview.html` for a screenshot-friendly
view of the seeded database. Development credentials are:

- Administrator: `admin@timeclock.local`
- Example employee: `employee01@timeclock.local`
- Password for all seeded accounts: `Employee!2026`

Register an employee with `POST /api/auth/register`, then use
`POST /api/auth/login` and send the returned token as
`Authorization: Bearer <token>`.

The JWT key in `appsettings.json` is for local development only. Override it in
deployed environments:

```powershell
$env:Jwt__Key = "<a-long-random-secret>"
```

## Bootstrap an administrator

Public registration always creates an Employee and cannot grant administrator
access. To create the initial administrator, set these environment variables for
one startup, then disable bootstrapping:

```powershell
$env:BootstrapAdmin__Enabled = "true"
$env:BootstrapAdmin__Email = "admin@example.com"
$env:BootstrapAdmin__Password = "<a-strong-password>"
dotnet run --project TimeOffApi
```

## Switch to SQL Server

Set `DatabaseProvider` to `SqlServer` and override the matching connection
string. The EF Core model includes provider-specific filtered unique indexes for
active work and break sessions.

```powershell
$env:DatabaseProvider = "SqlServer"
$env:ConnectionStrings__SqlServer = "<connection-string>"
```

For production schema evolution, replace startup `EnsureCreated` with EF Core
migrations and apply migrations in the deployment pipeline.
