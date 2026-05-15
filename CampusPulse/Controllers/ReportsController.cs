using CampusPulse.Models;
using CampusPulse.Models.Interfaces;
using CampusPulse.Models.ViewModels;
using CampusPulse.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CampusPulse.Controllers
{
    public class ReportsController : Controller
    {
        private readonly IReportsRepository _reportsRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IImageUploadService _imageUploadService;
        private readonly ICampusPulseNotificationService _notificationService;
        private readonly IReportActivityService _activityService;

        public ReportsController(
            IReportsRepository reportsRepository,
            UserManager<ApplicationUser> userManager,
            IImageUploadService imageUploadService,
            ICampusPulseNotificationService notificationService,
            IReportActivityService activityService)
        {
            _reportsRepository = reportsRepository;
            _userManager = userManager;
            _imageUploadService = imageUploadService;
            _notificationService = notificationService;
            _activityService = activityService;
        }

        public IActionResult Index(string searchString, string location, string category, string status)
        {
            var reports = _reportsRepository.GetAllReports();

            if (!string.IsNullOrEmpty(searchString))
            {
                reports = reports
                    .Where(r => r.Title.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrEmpty(location))
            {
                reports = reports
                    .Where(r => r.Location.Contains(location, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrEmpty(category))
            {
                reports = reports
                    .Where(r => r.Category == category)
                    .ToList();
            }

            if (!string.IsNullOrEmpty(status))
            {
                reports = reports
                    .Where(r => r.Status == status)
                    .ToList();
            }

            var filteredReports = reports
                .OrderByDescending(r => r.Date_Reported)
                .ToList();

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentLocation"] = location;
            ViewData["CurrentCategory"] = category;
            ViewData["CurrentStatus"] = status;

            return View(filteredReports);
        }

        public IActionResult Details(int id)
        {
            var report = _reportsRepository.GetReportById(id);

            if (report == null)
            {
                return NotFound();
            }

            var isAuthenticated = User.Identity?.IsAuthenticated == true;
            var currentUserId = isAuthenticated ? _userManager.GetUserId(User) : null;

            var viewModel = new ReportDetailsViewModel
            {
                Report = report,
                IsAuthenticated = isAuthenticated,

                // A user only owns a report if both IDs exist and match.
                // This prevents anonymised reports with null ReporterId from being treated as owned by anonymous users.
                IsOwner = isAuthenticated
                          && !string.IsNullOrWhiteSpace(currentUserId)
                          && !string.IsNullOrWhiteSpace(report.ReporterId)
                          && report.ReporterId == currentUserId,

                IsInvestigator = User.IsInRole(UserRoles.Investigator),

                HasUpvoted = isAuthenticated
                             && !string.IsNullOrWhiteSpace(currentUserId)
                             && _reportsRepository.HasUserUpvotedReport(id, currentUserId)
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
        [RequestSizeLimit(10 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
        public async Task<IActionResult> Create(Report report, IFormFile? imageFile)
        {
            report.ReporterId = _userManager.GetUserId(User);
            report.ReporterEmail = User.Identity?.Name ?? string.Empty;

            // These fields are assigned server-side and should not be supplied by the browser.
            ModelState.Remove(nameof(Report.ReporterId));
            ModelState.Remove(nameof(Report.ReporterEmail));
            ModelState.Remove(nameof(Report.ImageUrl));

            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadResult = await _imageUploadService.SaveReportImageAsync(imageFile);

                    if (!uploadResult.Success)
                    {
                        ModelState.AddModelError("ImageFile", uploadResult.ErrorMessage ?? "Invalid image upload.");
                        return View(report);
                    }

                    report.ImageUrl = uploadResult.ImageUrl;
                }

                report.Date_Reported = DateTime.Now;
                report.Status = "Open";
                report.Upvotes = 0;

                // Save first so the generated report Id can be used in activity logs and notification links.
                _reportsRepository.CreateReport(report);

                var actor = await GetCurrentActorAsync();

                await _activityService.LogAsync(
                    report.Id,
                    "Created",
                    "Report was submitted.",
                    actor.UserId,
                    actor.DisplayName);

                var reportUrl = Url.Action(
                    nameof(Details),
                    "Reports",
                    new { id = report.Id },
                    Request.Scheme) ?? string.Empty;

                await _notificationService.NotifyInvestigatorsOfNewReportAsync(report, reportUrl);

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

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Challenge();
            }

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

            var nowUpvoted = _reportsRepository.ToggleUpvoteReport(id, currentUserId);

            TempData["SuccessMessage"] = nowUpvoted
                ? "Report upvoted."
                : "Your upvote was removed.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = UserRoles.Investigator)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var existingReport = _reportsRepository.GetReportById(id);

            if (existingReport == null)
            {
                return NotFound();
            }

            var oldStatus = existingReport.Status;

            var allowedStatuses = new List<string>
            {
                "Open",
                "Being Investigated",
                "Resolved",
                "No Action Required"
            };

            // Only allow known statuses
            if (!allowedStatuses.Contains(status))
            {
                TempData["ErrorMessage"] = "Invalid status selected.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (oldStatus == status)
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            _reportsRepository.UpdateReportStatus(id, status);

            var actor = await GetCurrentActorAsync();

            // Record a timeline entry and notify the reporter after the investigator changes status.
            await _activityService.LogAsync(
                id,
                "Status Updated",
                $"Status changed from '{oldStatus}' to '{status}'.",
                actor.UserId,
                actor.DisplayName);

            var updatedReport = _reportsRepository.GetReportById(id);

            if (updatedReport != null)
            {
                var reportUrl = Url.Action(
                    nameof(Details),
                    "Reports",
                    new { id = updatedReport.Id },
                    Request.Scheme) ?? string.Empty;

                await _notificationService.NotifyReporterOfStatusChangeAsync(updatedReport, reportUrl);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = UserRoles.Investigator)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveInvestigation(int id, string actionTaken, string? investigatorPhone)
        {
            if (string.IsNullOrWhiteSpace(actionTaken))
            {
                TempData["ErrorMessage"] = "Investigation action details are required.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var reportBeforeUpdate = _reportsRepository.GetReportById(id);

            if (reportBeforeUpdate == null)
            {
                return NotFound();
            }

            var hadInvestigation = reportBeforeUpdate.Investigation != null;

            var investigatorId = _userManager.GetUserId(User) ?? string.Empty;
            var investigatorEmail = User.Identity?.Name ?? string.Empty;

            _reportsRepository.AddOrUpdateInvestigation(
                id,
                actionTaken,
                investigatorId,
                investigatorEmail,
                investigatorPhone
            );

            var actor = await GetCurrentActorAsync();

            await _activityService.LogAsync(
                id,
                hadInvestigation ? "Investigation Updated" : "Investigation Added",
                hadInvestigation
                    ? "Investigation details were updated."
                    : "An investigation entry was added.",
                actor.UserId,
                actor.DisplayName);

            var updatedReport = _reportsRepository.GetReportById(id);

            if (updatedReport != null)
            {
                var reportUrl = Url.Action(
                    nameof(Details),
                    "Reports",
                    new { id = updatedReport.Id },
                    Request.Scheme) ?? string.Empty;

                await _notificationService.NotifyReporterOfInvestigationUpdateAsync(updatedReport, reportUrl);
            }

            return RedirectToAction(nameof(Details), new { id });
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
        [RequestSizeLimit(10 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
        public async Task<IActionResult> Edit(int id, Report updatedReport, IFormFile? imageFile)
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

            ModelState.Remove(nameof(Report.ReporterId));
            ModelState.Remove(nameof(Report.ReporterEmail));
            ModelState.Remove(nameof(Report.ImageUrl));

            if (ModelState.IsValid)
            {
                existingReport.Title = updatedReport.Title;
                existingReport.Location = updatedReport.Location;
                existingReport.Time_Observed = updatedReport.Time_Observed;
                existingReport.Category = updatedReport.Category;
                existingReport.Description = updatedReport.Description;
                existingReport.ReporterPhone = updatedReport.ReporterPhone;

                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadResult = await _imageUploadService.SaveReportImageAsync(imageFile);

                    if (!uploadResult.Success)
                    {
                        ModelState.AddModelError("ImageFile", uploadResult.ErrorMessage ?? "Invalid image upload.");
                        return View(existingReport);
                    }

                    _imageUploadService.DeleteImage(existingReport.ImageUrl);
                    existingReport.ImageUrl = uploadResult.ImageUrl;
                }

                _reportsRepository.UpdateReport(existingReport);

                var actor = await GetCurrentActorAsync();

                await _activityService.LogAsync(
                    existingReport.Id,
                    "Edited",
                    "Report details were edited.",
                    actor.UserId,
                    actor.DisplayName);

                return RedirectToAction(nameof(Details), new { id = existingReport.Id });
            }

            updatedReport.ImageUrl = existingReport.ImageUrl;
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

            _imageUploadService.DeleteImage(report.ImageUrl);
            _reportsRepository.DeleteReport(id);

            return RedirectToAction(nameof(Index));
        }

        private bool UserCanManageReport(Report report)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            var currentUserId = _userManager.GetUserId(User);

            return !string.IsNullOrWhiteSpace(currentUserId)
                   && !string.IsNullOrWhiteSpace(report.ReporterId)
                   && report.ReporterId == currentUserId
                   && !User.IsInRole(UserRoles.Investigator);
        }

        private async Task<(string? UserId, string DisplayName)> GetCurrentActorAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return (null, "Unknown user");
            }

            return (user.Id, user.DisplayName ?? user.UserName ?? "Unknown user");
        }
    }
}