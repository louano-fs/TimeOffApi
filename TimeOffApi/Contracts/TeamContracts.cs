namespace TimeOffApi.Contracts;

public sealed record TeamMemberResponse(
    int UserId,
    int EmployeeId,
    string EmployeeNumber,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive);
