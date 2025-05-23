using Quartz;
using System.Text;

namespace HttpTaskScheduler.Job
{
    // HttpTaskJob.cs
    public class HttpTaskJob : IJob
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HttpTaskJob> _logger;
        private readonly IServiceProvider _services;

        public HttpTaskJob(
            IHttpClientFactory httpClientFactory,
            IServiceProvider services,
            ILogger<HttpTaskJob> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _services = services;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var taskId = context.JobDetail.JobDataMap.GetInt("taskId");

            using var scope = _services.CreateScope();
            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var task = await dbContext.TaskSchedules.FindAsync(taskId);

            if (task == null)
            {
                _logger.LogWarning($"Task with ID {taskId} not found");
                return;
            }

            var log = new TaskExecutionLog
            {
                TaskScheduleId = task.Id,
                StartTime = DateTime.Now,
                IsSuccess = false
            };

            try
            {
                var client = _httpClientFactory.CreateClient("UnsafeHttpClient");

                var request = new HttpRequestMessage(new HttpMethod(task.HttpMethod), task.Url);

                if (!string.IsNullOrEmpty(task.Headers))
                {
                    // 按换行符拆分（支持不同环境下的换行符）
                    var headerLines = task.Headers.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in headerLines)
                    {
                        // 按第一个冒号拆分键值（考虑值中可能包含冒号的情况）
                        var colonIndex = line.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            var key = line.Substring(0, colonIndex).Trim();
                            var value = line.Substring(colonIndex + 1).Trim();

                            // 添加前检查是否已存在该header
                            if (!request.Headers.Contains(key))
                            {
                                request.Headers.Add(key, value);
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(task.Body) && task.HttpMethod != "GET")
                {
                    request.Content = new StringContent(task.Body, Encoding.UTF8, "application/json");
                }

                var response = await client.SendAsync(request);
                log.IsSuccess = response.IsSuccessStatusCode;
                log.Response = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                log.ErrorMessage = ex.Message;
                _logger.LogError(ex, $"Error executing task {task.Id}");
            }
            finally
            {
                log.EndTime = DateTime.Now;
                dbContext.TaskExecutionLogs.Add(log);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
