using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

public sealed record DirectReportIdentity(
    int UserId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    bool IsActive)
{
    public string DisplayName => $"{FirstName} {LastName}".Trim();
}

public sealed record DirectReportCandidate(
    string EmployeeNumber,
    string DisplayName);

public abstract record DirectReportResolution;

public sealed record ResolvedDirectReport(DirectReportIdentity Member) : DirectReportResolution;

public sealed record AmbiguousDirectReport(
    IReadOnlyCollection<DirectReportCandidate> Candidates) : DirectReportResolution;

public interface IDirectReportResolver
{
    Task<DirectReportResolution> ResolveAsync(
        ManagerScope scope,
        string employeeReference,
        bool includeInactive,
        CancellationToken cancellationToken);
}

public sealed class DirectReportResolver(
    AppDbContext db,
    IOptions<ManagerAssistantOptions> options) : IDirectReportResolver
{
    private readonly int _maxTeamMembers = options.Value.MaxTeamMembers;

    public async Task<DirectReportResolution> ResolveAsync(
        ManagerScope scope,
        string employeeReference,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var reference = Normalize(employeeReference);
        if (reference.Length == 0)
            throw new ValidationException(
                "INVALID_EMPLOYEE_REFERENCE",
                "An employee number or exact name is required.");

        var directReports = db.Users.AsNoTracking()
            .Where(x => x.ManagerId == scope.ManagerId && x.Role == UserRole.Employee);
        if (!includeInactive)
            directReports = directReports.Where(x => x.IsActive);

        var members = await directReports
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.EmployeeNumber)
            .Take(_maxTeamMembers + 1)
            .Select(x => new DirectReportIdentity(
                x.Id,
                x.EmployeeNumber,
                x.FirstName,
                x.LastName,
                x.IsActive))
            .ToArrayAsync(cancellationToken);
        if (members.Length > _maxTeamMembers)
            throw new ValidationException(
                "TEAM_REPORT_TOO_LARGE",
                $"Team reports may include at most {_maxTeamMembers} employees.");

        var employeeNumberMatch = members
            .SingleOrDefault(x => Normalize(x.EmployeeNumber) == reference);
        if (employeeNumberMatch is not null)
            return new ResolvedDirectReport(employeeNumberMatch);

        var fullNameMatches = members
            .Where(x => Normalize(x.DisplayName) == reference)
            .ToArray();
        if (fullNameMatches.Length == 1)
            return new ResolvedDirectReport(fullNameMatches[0]);
        if (fullNameMatches.Length > 1)
            return Ambiguous(fullNameMatches);

        var firstNameMatches = members
            .Where(x => Normalize(x.FirstName) == reference)
            .ToArray();
        if (firstNameMatches.Length == 1)
            return new ResolvedDirectReport(firstNameMatches[0]);
        if (firstNameMatches.Length > 1)
            return Ambiguous(firstNameMatches);

        throw new NotFoundException(
            "TEAM_MEMBER_NOT_FOUND",
            "The requested team member was not found.");
    }

    private static AmbiguousDirectReport Ambiguous(
        IEnumerable<DirectReportIdentity> members) =>
        new(members
            .Select(x => new DirectReportCandidate(x.EmployeeNumber, x.DisplayName))
            .ToArray());

    private static string Normalize(string? value) =>
        string.Join(
            ' ',
            (value ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .ToUpperInvariant();
}
