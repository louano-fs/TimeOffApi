namespace TimeOffApi.Services;

internal sealed record ExportMember(
    int UserId,
    int EmployeeId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    bool IsActive);

internal sealed record ExportWorkSession(
    int SourceSessionId,
    int UserId,
    string EmployeeNumber,
    string EmployeeName,
    DateOnly ShiftDate,
    DateTime Start,
    DateTime End,
    string Status,
    string StoredTimezone,
    int ElapsedSeconds,
    int BreakSeconds,
    int WorkedSeconds);

internal sealed record ExportBreak(
    int SourceBreakId,
    int SourceSessionId,
    int UserId,
    string EmployeeNumber,
    string EmployeeName,
    DateTime Start,
    DateTime End,
    string Status,
    int DurationSeconds);

internal sealed record TimeLogExportData(
    string ReportType,
    string PreparedFor,
    string? PreparedForEmployeeNumber,
    string ReportingTimezone,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime AsOf,
    int ExcludedInactiveCount,
    IReadOnlyList<ExportMember> Members,
    IReadOnlyList<ExportWorkSession> WorkSessions,
    IReadOnlyList<ExportBreak> Breaks);
