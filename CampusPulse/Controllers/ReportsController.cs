using CampusPulse.Models;
using CampusPulse.Models.Interfaces;
using CampusPulse.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CampusPulse.Controllers
{
    public class ReportsController : Controller
    {
        private readonly IReportsRepository _reportsRepository;
        private readonly UserManager<IdentityUser> _userManager;

        public ReportsController(
            IReportsRepository reportsRepository,
            UserManager<IdentityUser> userManager)
        {
            _reportsRepository = reportsRepository;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            var reports = _reportsRepository.GetAllReports();
            return View(reports);
        }

        public IActionResult Details(int id)
        {
            var report = _reportsRepository.GetReportById(id);

            if (report == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);

            var viewModel = new ReportDetailsViewModel
            {
                Report = report,
                IsAuthenticated = User.Identity?.IsAuthenticated == true,
                IsOwner = report.ReporterId == currentUserId,
                IsInvestigator = User.IsInRole(UserRoles.Investigator)
            };

            return View(viewModel);
        }

        [Authorize]
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Report report)
        {
            report.ReporterId = _userManager.GetUserId(User);
            report.ReporterEmail = User.Identity?.Name ?? string.Empty;

            ModelState.Remove(nameof(Report.ReporterId));
            ModelState.Remove(nameof(Report.ReporterEmail));

            if (ModelState.IsValid)
            {
                report.Date_Reported = DateTime.Now;
                report.Status = "Open";
                report.Upvotes = 0;

                _reportsRepository.CreateReport(report);

                return RedirectToAction(nameof(Index));
            }

            return View(report);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Upvote(int id)
        {
            var report = _reportsRepository.GetReportById(id);

            if (report == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);

            if (report.ReporterId == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot upvote your own report.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (User.IsInRole(UserRoles.Investigator))
            {
                TempData["ErrorMessage"] = "Investigators cannot upvote reports.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _reportsRepository.UpvoteReport(id);

            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = UserRoles.Investigator)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateStatus(int id, string status)
        {
            var allowedStatuses = new List<string>
            {
                "Open",
                "Being Investigated",
                "Resolved",
                "No Action Required"
            };

            if (!allowedStatuses.Contains(status))
            {
                TempData["ErrorMessage"] = "Invalid status selected.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _reportsRepository.UpdateReportStatus(id, status);

            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = UserRoles.Investigator)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveInvestigation(int id, string actionTaken, string? investigatorPhone)
        {
            if (string.IsNullOrWhiteSpace(actionTaken))
            {
                TempData["ErrorMessage"] = "Investigation action details are required.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var investigatorEmail = User.Identity?.Name ?? string.Empty;

            _reportsRepository.AddOrUpdateInvestigation(
                id,
                actionTaken,
                investigatorEmail,
                investigatorPhone
            );

            return RedirectToAction(nameof(Details), new { id });
        }
        private bool UserCanManageReport(Report report)
        {
            var currentUserId = _userManager.GetUserId(User);

            return User.Identity?.IsAuthenticated == true
                && report.ReporterId == currentUserId
                && !User.IsInRole(UserRoles.Investigator);
        }
        [Authorize]
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var report = _reportsRepository.GetReportById(id);

            if (report == null)
            {
                return NotFound();
            }

            if (!UserCanManageReport(report))
            {
                return Forbid();
            }

            return View(report);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Report updatedReport)
        {
            if (id != updatedReport.Id)
            {
                return NotFound();
            }

            var existingReport = _reportsRepository.GetReportById(id);

            if (existingReport == null)
            {
                return NotFound();
            }

            if (!UserCanManageReport(existingReport))
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                existingReport.Title = updatedReport.Title;
                existingReport.Location = updatedReport.Location;
                existingReport.Time_Observed = updatedReport.Time_Observed;
                existingReport.Category = updatedReport.Category;
                existingReport.Description = updatedReport.Description;
                existingReport.ReporterPhone = updatedReport.ReporterPhone;

                // Do not update these here:
                // ReporterId, ReporterEmail, Date_Reported, Status, Upvotes, Investigation

                _reportsRepository.UpdateReport(existingReport);

                return RedirectToAction(nameof(Details), new { id = existingReport.Id });
            }

            return View(updatedReport);
        }

        [Authorize]
        [HttpGet]
        public IActionResult Delete(int id)
        {
            var report = _reportsRepository.GetReportById(id);

            if (report == null)
            {
                return NotFound();
            }

            if (!UserCanManageReport(report))
            {
                return Forbid();
            }

            return View(report);
        }

        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var report = _reportsRepository.GetReportById(id);

            if (report == null)
            {
                return NotFound();
            }

            if (!UserCanManageReport(report))
            {
                return Forbid();
            }

            _reportsRepository.DeleteReport(id);

            return RedirectToAction(nameof(Index));
        }
    }
}