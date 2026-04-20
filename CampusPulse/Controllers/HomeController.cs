using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using CampusPulse.Models;

namespace CampusPulse.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    public IActionResult Index()
    {
    // Temporary "Fake" data to test your design
    var reports = new List<Report>
    {
        new Report { 
            Id = 1, 
            Title = "Broken Tiles", 
            Location = "Quad", 
            Category = "Safety", 
            Status = "Open", 
            Upvotes = 15,
            Description = "Tiles are loose near the canteen.", // Added this
            ReporterEmail = "student1@um.edu.mt"            // Added this
        },
        new Report { 
            Id = 2, 
            Title = "Elevator Down", 
            Location = "Library", 
            Category = "Accessibility", 
            Status = "Being Investigated", 
            Upvotes = 42,
            Description = "Main elevator is stuck on Level 2.", 
            ReporterEmail = "staff@um.edu.mt"
        },
        new Report { 
            Id = 3, 
            Title = "Dim Lights", 
            Location = "Car Park 4", 
            Category = "Safety", 
            Status = "Resolved", 
            Upvotes = 5,
            Description = "Lights flickering in the corner.",
            ReporterEmail = "security@um.edu.mt"
        }
    };

    return View(reports);
    }
}
