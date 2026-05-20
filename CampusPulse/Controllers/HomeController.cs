using CampusPulse.Data;
using CampusPulse.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusPulse.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            AppDbContext context,
            ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            try
            {
                var now = DateTime.Now;

                var trendingPulses = _context.Reports
                    .AsEnumerable()
                    .OrderByDescending(r =>
                    {
                        double hoursOld = (now - r.Date_Reported).TotalHours;
                        return (double)r.Upvotes / (hoursOld + 2);
                    })
                    .Take(5)
                    .ToList();

                _logger.LogInformation(
                    "Home page loaded. TrendingCount: {TrendingCount}; UserId: {UserId}; Authenticated: {Authenticated}; Roles: {Roles}; TraceId: {TraceId}",
                    trendingPulses.Count,
                    GetCurrentUserId(),
                    User.Identity?.IsAuthenticated == true,
                    GetCurrentUserRoles(),
                    HttpContext.TraceIdentifier);

                return View(trendingPulses);
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(Index));
            }
        }

        public IActionResult HallOfFame()
        {
            try
            {
                var currentYear = DateTime.Now.Year;

                var leaderboard = _context.Reports
                    .Include(r => r.Reporter)
                    .Where(r => r.Date_Reported.Year == currentYear
                                && r.ReporterId != null
                                && r.Reporter != null
                                && r.Reporter.DisplayName != null)
                    .GroupBy(r => new
                    {
                        r.ReporterId,
                        r.Reporter!.DisplayName
                    })
                    .Select(group => new ReporterStatsViewModel
                    {
                        DisplayName = group.Key.DisplayName!,
                        ReportsSubmitted = group.Count(),
                        TotalUpvotes = group.Sum(r => r.Upvotes)
                    })
                    .OrderByDescending(stats => stats.ReportsSubmitted)
                    .ThenByDescending(stats => stats.TotalUpvotes)
                    .ToList();

                _logger.LogInformation(
                    "Hall of Fame loaded. Year: {Year}; LeaderboardCount: {LeaderboardCount}; UserId: {UserId}; Authenticated: {Authenticated}; Roles: {Roles}; TraceId: {TraceId}",
                    currentYear,
                    leaderboard.Count,
                    GetCurrentUserId(),
                    User.Identity?.IsAuthenticated == true,
                    GetCurrentUserRoles(),
                    HttpContext.TraceIdentifier);

                return View(leaderboard);
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(HallOfFame));
            }
        }

        public IActionResult StatusCodePage(int code)
        {
            try
            {
                Response.StatusCode = code;
                ViewData["StatusCode"] = code;

                _logger.LogWarning(
                    "Status code page displayed. StatusCode: {StatusCode}; UserId: {UserId}; Authenticated: {Authenticated}; Roles: {Roles}; Path: {Path}; TraceId: {TraceId}",
                    code,
                    GetCurrentUserId(),
                    User.Identity?.IsAuthenticated == true,
                    GetCurrentUserRoles(),
                    HttpContext.Request.Path,
                    HttpContext.TraceIdentifier);

                return View("StatusCode");
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(StatusCodePage));
            }
        }

        public IActionResult AccessDenied()
        {
            try
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;

                _logger.LogWarning(
                    "Access denied page displayed. UserId: {UserId}; Authenticated: {Authenticated}; Roles: {Roles}; Path: {Path}; TraceId: {TraceId}",
                    GetCurrentUserId(),
                    User.Identity?.IsAuthenticated == true,
                    GetCurrentUserRoles(),
                    HttpContext.Request.Path,
                    HttpContext.TraceIdentifier);

                return View();
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(AccessDenied));
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            Response.StatusCode = StatusCodes.Status500InternalServerError;

            _logger.LogError(
                "Friendly error page displayed. UserId: {UserId}; Authenticated: {Authenticated}; Roles: {Roles}; Path: {Path}; TraceId: {TraceId}",
                GetCurrentUserId(),
                User.Identity?.IsAuthenticated == true,
                GetCurrentUserRoles(),
                HttpContext.Request.Path,
                HttpContext.TraceIdentifier);

            return View();
        }

        private IActionResult HandleControllerException(Exception ex, string actionName)
        {
            _logger.LogError(
                ex,
                "Unhandled home controller exception. Action: {Action}; UserId: {UserId}; Authenticated: {Authenticated}; Roles: {Roles}; Path: {Path}; TraceId: {TraceId}",
                actionName,
                GetCurrentUserId(),
                User.Identity?.IsAuthenticated == true,
                GetCurrentUserRoles(),
                HttpContext.Request.Path,
                HttpContext.TraceIdentifier);

            return View("Error");
        }

        private string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Anonymous";
        }

        private string GetCurrentUserRoles()
        {
            var roles = User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct()
                .ToList();

            return roles.Any()
                ? string.Join(",", roles)
                : "None";
        }
    }
}