// Models/TaskSchedule.cs
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

public class TaskSchedule
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string Url { get; set; }

    public string HttpMethod { get; set; } = "GET";
    public string? Headers { get; set; }
    public string? Body { get; set; }

    [Required]
    public string CronExpression { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public bool IsActive { get; set; } = true;
}

// Models/TaskExecutionLog.cs
public class TaskExecutionLog
{
    public int Id { get; set; }
    public int TaskScheduleId { get; set; }
    public TaskSchedule TaskSchedule { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsSuccess { get; set; }
    public string? Response { get; set; }
    public string? ErrorMessage { get; set; }
}

// Models/ApplicationUser.cs
public class ApplicationUser : IdentityUser
{
    public bool IsAdmin { get; set; }
}