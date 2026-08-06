namespace TimeOffApi.Domain;

public sealed class User
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public int? ManagerId { get; set; }
    public string Timezone { get; set; } = "Asia/Manila";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public ICollection<TimeLog> TimeLogs { get; set; } = [];
    public ICollection<TimeOffRequest> TimeOffRequests { get; set; } = [];
    public User? Manager { get; set; }
    public ICollection<User> DirectReports { get; set; } = [];
}

public enum UserRole
{
    Employee = 0,
    Administrator = 1,
    Manager = 2
}
