using CampusPulse.Data;
using CampusPulse.Models;
using CampusPulse.Models.Interfaces;
using CampusPulse.Models.Repositories;
using CampusPulse.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Logging providers are configured explicitly so the project logs useful application/security events
// without being flooded by low-value framework logs.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog(settings =>
    {
        settings.SourceName = "CampusPulse";
    });
}

builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("CampusPulse", LogLevel.Information);

// Automatically validates anti-forgery tokens on unsafe HTTP methods such as POST.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")
    ));

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;

        options.User.RequireUniqueEmail = true;

        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    // HttpOnly prevents JavaScript from reading the authentication cookie.
    options.Cookie.HttpOnly = true;

    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;

    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Home/AccessDenied";
});

builder.Services.Configure<FormOptions>(options =>
{
    // Allows oversized upload attempts to reach controller validation instead of immediately failing
    // with a generic 400 error. The actual accepted image size is enforced by ImageUploadService.
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
});

builder.Services.AddScoped<IReportsRepository, ReportsRepository>();
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();
builder.Services.AddScoped<IUserDataService, UserDataService>();
builder.Services.AddScoped<IClaimsTransformation, InvestigatorRoleClaimsTransformation>();
builder.Services.AddScoped<IReportActivityService, ReportActivityService>();

// Email credentials are loaded from configuration/User Secrets. Please check the README for setup details.
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<ICampusPulseNotificationService, CampusPulseNotificationService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // Applying migrations on startup for easier setup.
    var dbContext = services.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    // Seeds roles only, not privileged users or passwords.
    await IdentitySeeder.SeedAsync(services);
}

// Use the friendly error page so users do not see developer exception details.
app.UseExceptionHandler("/Home/Error");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Re-executes status-code errors such as 404 through a custom page.
app.UseStatusCodePagesWithReExecute("/Home/StatusCodePage", "?code={0}");

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages();

app.Run();