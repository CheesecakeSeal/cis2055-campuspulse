using CampusPulse.Data;
using CampusPulse.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusPulse.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
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

            return View(trendingPulses);
        }

        public IActionResult HallOfFame()
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

            return View(leaderboard);
        }
    }
}