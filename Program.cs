using HttpTaskScheduler.Job;
using HttpTaskScheduler.Models;
using HttpTaskScheduler.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Configure DbContext with SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// ���Quartz����
builder.Services.AddSingleton<IJobFactory, SingletonJobFactory>();
builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();

// ͬ����ȡ��ע��IScheduler
var scheduler = await builder.Services.BuildServiceProvider()
    .GetRequiredService<ISchedulerFactory>()
    .GetScheduler();
//await scheduler.Start();
builder.Services.AddSingleton(scheduler);

// ע����ҵ
builder.Services.AddSingleton<HttpTaskJob>();

//ʾ��
builder.Services.AddSingleton<SampleJob>();
//builder.Services.AddSingleton(new JobSchedule(
//    jobType: typeof(SampleJob),
//    cronExpression: "0/5 * * * * ?")); // ÿ5��ִ��һ��

//builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
// Register our custom Quartz service
builder.Services.AddHostedService<QuartzHostedService>();
builder.Services.AddScoped<QuartzHostedService>();

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ���� Cookie ��֤
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.LogoutPath = "/Account/Logout";
});


// Add HTTP client factory
builder.Services.AddHttpClient();

builder.Services.AddHttpClient("UnsafeHttpClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
    });

// Add MVC services
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Initialize database
await InitializeDatabase(app);

app.Run();

/*
����Ǩ����������ݿ�
dotnet ef migrations add InitialCreate
dotnet ef database update 
 */
async Task InitializeDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    await db.Database.EnsureCreatedAsync();

    // Create admin user if none exists
    if (!await userManager.Users.AnyAsync())
    {
        var adminUser = new ApplicationUser
        {
            UserName = "admin",
            Email = "admin@example.com",
            IsAdmin = true
        };

        await userManager.CreateAsync(adminUser, "tDPjRYhkFpMidxFk");

        var normalUser = new ApplicationUser
        {
            UserName = "user",
            Email = "user@example.com",
            IsAdmin = false
        };

        await userManager.CreateAsync(normalUser, "5CCeyuLFQqGfO41E");
    }
}