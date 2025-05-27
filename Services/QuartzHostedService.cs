using CronExpressionDescriptor;
using HttpTaskScheduler.Job;
using HttpTaskScheduler.Models;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Spi;

public class QuartzHostedService : IHostedService
{
    private readonly ILogger<QuartzHostedService> _logger;
    private readonly IServiceProvider _services;
    private readonly IScheduler _scheduler;
    private readonly IJobFactory _jobFactory;
    private readonly IEnumerable<JobSchedule> _jobSchedules;


    public QuartzHostedService(
        IEnumerable<JobSchedule> jobSchedules,
        IScheduler scheduler,
        ILogger<QuartzHostedService> logger,
        IServiceProvider services,
        IJobFactory jobFactory)
    {
        _scheduler = scheduler;
        _logger = logger;
        _services = services;
        _jobFactory = jobFactory;
        _jobSchedules = jobSchedules;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _scheduler.JobFactory = _jobFactory;

        foreach (var jobSchedule in _jobSchedules)
        {
            var job = CreateJob(jobSchedule);
            var trigger = CreateTrigger(jobSchedule);

            await _scheduler.ScheduleJob(job, trigger, cancellationToken);
        }

        using (var scope = _services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            // 从数据库加载所有活动任务
            var activeTasks = await dbContext.TaskSchedules
                .Where(t => t.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var task in activeTasks)
            {
                await ScheduleJob(task, cancellationToken);
            }
        }
        await _scheduler.Start(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _scheduler?.Shutdown(cancellationToken);
    }

    public async Task ScheduleJob(TaskSchedule task, CancellationToken cancellationToken)
    {
        var jobKey = new JobKey($"task_{task.Id}");
        var triggerKey = new TriggerKey($"trigger_{task.Id}");

        var job = JobBuilder.Create<HttpTaskJob>()
            .WithIdentity(jobKey)
            .UsingJobData("taskId", task.Id)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .WithCronSchedule(task.CronExpression, x => x.InTimeZone(TimeZoneInfo.Local))
            .Build();

        await _scheduler.ScheduleJob(job, trigger, cancellationToken);
    }

    private static IJobDetail CreateJob(JobSchedule schedule)
    {
        var jobType = schedule.JobType;
        return JobBuilder
            .Create(jobType)
            .WithIdentity(jobType.FullName)
            .WithDescription(jobType.Name)
            .Build();
    }

    private static ITrigger CreateTrigger(JobSchedule schedule)
    {
        return TriggerBuilder
            .Create()
            .WithIdentity($"{schedule.JobType.FullName}.trigger")
            .WithCronSchedule(schedule.CronExpression)
            .WithDescription(schedule.CronExpression)
            .Build();
    }

    public async Task UnscheduleJob(int taskId, CancellationToken cancellationToken)
    {
        var jobKey = new JobKey($"task_{taskId}");
        await _scheduler.DeleteJob(jobKey, cancellationToken);
    }

    public async Task TriggerJob(int taskId, CancellationToken cancellationToken)
    {
        var jobKey = new JobKey($"task_{taskId}");
        await _scheduler.TriggerJob(jobKey, cancellationToken);
    }

    public async Task<List<DateTimeOffset>> GetNextFireTimes(string cronExpression, int count)
    {
        var trigger = TriggerBuilder.Create()
            .WithCronSchedule(cronExpression)
            .Build();

        var fireTimes = new List<DateTimeOffset>();
        var nextFireTime = trigger.GetFireTimeAfter(DateTimeOffset.Now);

        for (int i = 0; i < count && nextFireTime.HasValue; i++)
        {
            fireTimes.Add(nextFireTime.Value.ToLocalTime());
            nextFireTime = trigger.GetFireTimeAfter(nextFireTime.Value);
        }

        return fireTimes;
    }

    public string GetDescription(string cronExpression)
    {
        var options = new Options
        {
            Locale = "zh-CN"
        };
        return ExpressionDescriptor.GetDescription(cronExpression, options);
    }

}