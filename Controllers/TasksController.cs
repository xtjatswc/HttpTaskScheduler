using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;

[Authorize]
public class TasksController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly QuartzHostedService _quartzService;
    private readonly UserManager<ApplicationUser> _userManager;

    public TasksController(
        ApplicationDbContext context,
        QuartzHostedService quartzService,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _quartzService = quartzService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string search, int page = 1, int pageSize = 10)
    {
        var user = await _userManager.GetUserAsync(User);
        IQueryable<TaskSchedule> query = _context.TaskSchedules;

        if (!user.IsAdmin)
        {
            query = query.Where(t => t.CreatedBy == user.UserName);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(t => t.Name.Contains(search) || t.Url.Contains(search));
        }

        var tasks = await query
            .OrderByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = await query.CountAsync();

        return View(tasks);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaskSchedule task)
    {
        if (!CronExpression.IsValidExpression(task.CronExpression))
        {
            ModelState.AddModelError("CronExpression", "Invalid cron expression");
            return View(task);
        }

        if (ModelState.IsValid)
        {
            var user = await _userManager.GetUserAsync(User);
            task.CreatedBy = user.UserName;

            _context.Add(task);
            await _context.SaveChangesAsync();

            if (task.IsActive)
            {
                await _quartzService.ScheduleJob(task, HttpContext.RequestAborted);
            }

            return RedirectToAction(nameof(Index));
        }
        return View(task);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var task = await _context.TaskSchedules.FindAsync(id);
        if (task == null)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (!user.IsAdmin && task.CreatedBy != user.UserName)
        {
            return Forbid();
        }

        return View(task);
    }

    // GET: Tasks/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var taskSchedule = await _context.TaskSchedules
            .FirstOrDefaultAsync(m => m.Id == id);

        if (taskSchedule == null)
        {
            return NotFound();
        }

        return View(taskSchedule);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TaskSchedule task)
    {
        if (id != task.Id)
        {
            return NotFound();
        }

        if (!CronExpression.IsValidExpression(task.CronExpression))
        {
            ModelState.AddModelError("CronExpression", "Invalid cron expression");
            return View(task);
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState); // 直接返回错误详情（API风格）
        }

        if (ModelState.IsValid)
        {
            try
            {
                var existingTask = await _context.TaskSchedules.FindAsync(id);
                var user = await _userManager.GetUserAsync(User);

                if (!user.IsAdmin && existingTask.CreatedBy != user.UserName)
                {
                    return Forbid();
                }

                // 更新字段
                existingTask.Name = task.Name;
                existingTask.Url = task.Url;
                existingTask.HttpMethod = task.HttpMethod;
                existingTask.Headers = task.Headers;
                existingTask.Body = task.Body;
                existingTask.CronExpression = task.CronExpression;

                if (existingTask.IsActive != task.IsActive)
                {
                    existingTask.IsActive = task.IsActive;
                    if (task.IsActive)
                    {
                        await _quartzService.ScheduleJob(existingTask, HttpContext.RequestAborted);
                    }
                    else
                    {
                        await _quartzService.UnscheduleJob(existingTask.Id, HttpContext.RequestAborted);
                    }
                }
                else if (task.IsActive)
                {
                    // 如果任务已经是激活状态且配置有变化，需要重新调度
                    await _quartzService.UnscheduleJob(existingTask.Id, HttpContext.RequestAborted);
                    await _quartzService.ScheduleJob(existingTask, HttpContext.RequestAborted);
                }

                _context.Update(existingTask);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TaskScheduleExists(task.Id))
                {
                    return NotFound();
                }
                throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(task);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var task = await _context.TaskSchedules.FindAsync(id);
        if (task == null)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (!user.IsAdmin && task.CreatedBy != user.UserName)
        {
            return Forbid();
        }

        return View(task);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var task = await _context.TaskSchedules.FindAsync(id);
        if (task != null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (!user.IsAdmin && task.CreatedBy != user.UserName)
            {
                return Forbid();
            }

            await _quartzService.UnscheduleJob(id, HttpContext.RequestAborted);
            _context.TaskSchedules.Remove(task);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ToggleActive(int id, string search, int page)
    {
        var task = await _context.TaskSchedules.FindAsync(id);
        if (task == null)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (!user.IsAdmin && task.CreatedBy != user.UserName)
        {
            return Forbid();
        }

        task.IsActive = !task.IsActive;

        if (task.IsActive)
        {
            await _quartzService.ScheduleJob(task, HttpContext.RequestAborted);
        }
        else
        {
            await _quartzService.UnscheduleJob(task.Id, HttpContext.RequestAborted);
        }

        _context.Update(task);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { search, page });
    }

    public async Task<IActionResult> TriggerNow(int id, string search, int page)
    {
        var task = await _context.TaskSchedules.FindAsync(id);
        if (task == null)
        {
            return NotFound();
        }

        if (!task.IsActive)
        {
            return Forbid();
        }

        var user = await _userManager.GetUserAsync(User);
        if (!user.IsAdmin && task.CreatedBy != user.UserName)
        {
            return Forbid();
        }

        await _quartzService.TriggerJob(id, HttpContext.RequestAborted);
        return RedirectToAction(nameof(Index), new { search, page });
    }

    public async Task<IActionResult> NextFireTimes(int id)
    {
        var task = await _context.TaskSchedules.FindAsync(id);
        if (task == null)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (!user.IsAdmin && task.CreatedBy != user.UserName)
        {
            return Forbid();
        }

        var fireTimes = await _quartzService.GetNextFireTimes(task.CronExpression, 15);
        ViewBag.TaskName = task.Name;
        ViewBag.CronExpression = task.CronExpression;
        ViewBag.Description = _quartzService.GetDescription(task.CronExpression);
        return View(fireTimes);
    }

    [AllowAnonymous]
    public async Task<IActionResult> NextCronFireTimes(string cronExpression)
    {
        if (!CronExpression.IsValidExpression(cronExpression))
        {
            return Ok(new { success = false, desc = "cron表达式格式错误！" });
        }

        var fireTimes = await _quartzService.GetNextFireTimes(cronExpression, 15);
        string desc = _quartzService.GetDescription(cronExpression);
        return Ok(new { success = true, desc, list = fireTimes.Select(o => o.ToString("yyyy-MM-dd HH:mm:ss")) });
    }

    public async Task<IActionResult> ExecutionLogs(int id, int page = 1, int pageSize = 20)
    {
        var task = await _context.TaskSchedules.FindAsync(id);
        if (task == null)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (!user.IsAdmin && task.CreatedBy != user.UserName)
        {
            return Forbid();
        }

        var logs = await _context.TaskExecutionLogs
            .Where(l => l.TaskScheduleId == id)
            .OrderByDescending(l => l.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.TaskName = task.Name;
        ViewBag.TaskId = id;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = await _context.TaskExecutionLogs
            .Where(l => l.TaskScheduleId == id)
            .CountAsync();

        return View(logs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearLogs(int id)
    {
        var task = await _context.TaskSchedules.FindAsync(id);
        if (task == null)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (!user.IsAdmin && task.CreatedBy != user.UserName)
        {
            return Forbid();
        }

        var logs = _context.TaskExecutionLogs.Where(l => l.TaskScheduleId == id);
        _context.TaskExecutionLogs.RemoveRange(logs);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(ExecutionLogs), new { id });
    }

    // GET: Tasks/LogDetails/5
    public async Task<IActionResult> LogDetails(int id)
    {
        var log = await _context.TaskExecutionLogs
            .Include(l => l.TaskSchedule)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (log == null)
        {
            return NotFound();
        }

        return PartialView("_LogDetailsModal", log);
    }

    private bool TaskScheduleExists(int id)
    {
        return _context.TaskSchedules.Any(e => e.Id == id);
    }
}