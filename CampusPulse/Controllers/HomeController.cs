using Microsoft.AspNetCore.Mvc;
using CampusPulse.Data;
using CampusPulse.Models;
using System.Linq;

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
            var trendingIssues = _context.Reports
                .OrderByDescending(r => r.Upvotes)
                .Take(5)
                .ToList();

            return View(trendingIssues);
        }

        public IActionResult HallOfFame()
        {
            return View();
        }
    }
}