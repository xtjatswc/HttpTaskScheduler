using Quartz;

namespace HttpTaskScheduler.Job
{
    public class SampleJob : IJob
    {
        private readonly ILogger<SampleJob> _logger;

        public SampleJob(ILogger<SampleJob> logger)
        {
            _logger = logger;
        }

        public Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation($"Sample Job executed at: {DateTimeOffset.Now}");
            return Task.CompletedTask;
        }
    }
}
