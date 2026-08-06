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

Development startup also seeds 50 employees, one manager, one administrator, and seven
pending time-off requests. Open `/seed-preview.html` for a screenshot-friendly
view of the seeded database. Development credentials are:

- Administrator: `admin@timeclock.local`
- Manager (assigned employees 1001-1010): `manager@timeclock.local`
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

## Manager teams

Managers use the same personal time-clock dashboard as employees and receive an
additional **Team** section. `GET /api/team` returns only users whose `ManagerId`
matches the authenticated manager, and `GET /api/team/{userId}/time-logs`
returns that direct report's paged work sessions. Selecting **View logs** opens
the dedicated `/team/{userId}/time-logs` frontend route.

For now, manager roles and reporting assignments are provisioned directly in
the user store. Public registration remains employee-only. The development seed
creates `manager@timeclock.local` and assigns employees 1001 through 1010.

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
