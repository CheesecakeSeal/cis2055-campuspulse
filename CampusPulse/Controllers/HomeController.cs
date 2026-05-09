using Microsoft.AspNetCore.Mvc;
using CampusPulse.Data;
using CampusPulse.Models;
using System.Linq;
using CampusPulse.Models.ViewModels;

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

            var allReports = _context.Reports.ToList();

            var trendingPulses = allReports
                .OrderByDescending(r => {
                    double hoursOld = (now - r.DateReported).TotalHours;

                    return (double)r.Upvotes / (hoursOld + 2);
                })
                .Take(5)
                .ToList();

            return View(trendingPulses);
        }

        public IActionResult HallOfFame()
        {
            var leaderboard = _context.Reports
                .Where(r => !string.IsNullOrEmpty(r.ReporterEmail))
                .GroupBy(r => r.ReporterEmail)
                .Select(group => new ReporterStatsViewModel
                {
                    Email = group.Key,
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