namespace TimeOffApi.Domain;

public sealed class TimeLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? ParentTimeLogId { get; set; }
    public DateTime ShiftDate { get; set; }
    public DateTime Start { get; set; }
    public DateTime? End { get; set; }
    public TimeLogType Type { get; set; }
    public string Timezone { get; set; } = "Asia/Manila";
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public User User { get; set; } = null!;
    public TimeLog? ParentTimeLog { get; set; }
    public ICollection<TimeLog> Breaks { get; set; } = [];
}

public enum TimeLogType
{
    Work,
    Break
}
