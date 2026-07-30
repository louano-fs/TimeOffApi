namespace TimeOffApi.Domain;

public sealed class TimeOffRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeOffType Type { get; set; }
    public TimeOffRequestStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public User User { get; set; } = null!;
}

public enum TimeOffType
{
    Vacation,
    Sick,
    Personal
}

public enum TimeOffRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}
