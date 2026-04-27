using Microsoft.AspNetCore.Mvc;
using CampusPulse.Models;

namespace CampusPulse.Controllers
{
    public class ReportsController : Controller
    {
        //Shows blank form
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        //Handle the submitted data
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Report report)
        {
            if (ModelState.IsValid)
            {
                //Placeholder to return home, will be modified later
                return RedirectToAction("Index", "Home");
            }
            return View(report);
        }
    }
}