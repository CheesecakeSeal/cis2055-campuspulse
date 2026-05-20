using CampusPulse.Models;
using CampusPulse.Models.Interfaces;
using CampusPulse.Models.ViewModels;
using CampusPulse.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CampusPulse.Controllers
{
    public class ReportsController : Controller
    {
        private readonly IReportsRepository _reportsRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IImageUploadService _imageUploadService;
        private readonly ICampusPulseNotificationService _notificationService;
        private readonly IReportActivityService _activityService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            IReportsRepository reportsRepository,
            UserManager<ApplicationUser> userManager,
            IImageUploadService imageUploadService,
            ICampusPulseNotificationService notificationService,
            IReportActivityService activityService,
            ILogger<ReportsController> logger)
        {
            _reportsRepository = reportsRepository;
            _userManager = userManager;
            _imageUploadService = imageUploadService;
            _notificationService = notificationService;
            _activityService = activityService;
            _logger = logger;
        }

        public IActionResult Index(string searchString, string location, string category, string status)
        {
            try
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
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(Index));
            }
        }

        public IActionResult Details(int id)
        {
            try
            {
                var report = _reportsRepository.GetReportById(id);

                if (report == null)
                {
                    _logger.LogWarning(
                        "Details requested for non-existent report. ReportId: {ReportId}; UserId: {UserId}; Authenticated: {Authenticated}; Path: {Path}; TraceId: {TraceId}",
                        id,
                        _userManager.GetUserId(User) ?? "Anonymous",
                        User.Identity?.IsAuthenticated == true,
                        HttpContext.Request.Path,
                        HttpContext.TraceIdentifier);

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
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(Details), id);
            }
        }

        [Authorize]
        [HttpGet]
        public IActionResult Create()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(Create));
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(10 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
        public async Task<IActionResult> Create(Report report, IFormFile? imageFile)
        {
            try
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
                            _logger.LogWarning(
                                "Report creation image upload rejected. UserId: {UserId}; FileName: {FileName}; FileSize: {FileSize}; ContentType: {ContentType}; Reason: {Reason}; TraceId: {TraceId}",
                                _userManager.GetUserId(User) ?? "Anonymous",
                                imageFile.FileName,
                                imageFile.Length,
                                imageFile.ContentType,
                                uploadResult.ErrorMessage,
                                HttpContext.TraceIdentifier);

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

                    _logger.LogInformation(
                        "Report created successfully. ReportId: {ReportId}; UserId: {UserId}; Category: {Category}; HasImage: {HasImage}; TraceId: {TraceId}",
                        report.Id,
                        actor.UserId,
                        report.Category,
                        !string.IsNullOrWhiteSpace(report.ImageUrl),
                        HttpContext.TraceIdentifier);

                    var reportUrl = Url.Action(
                        nameof(Details),
                        "Reports",
                        new { id = report.Id },
                        Request.Scheme) ?? string.Empty;

                    await _notificationService.NotifyInvestigatorsOfNewReportAsync(report, reportUrl);

                    return RedirectToAction(nameof(Index));
                }

                _logger.LogWarning(
                    "Report creation failed validation. UserId: {UserId}; ModelStateErrors: {ModelStateErrors}; TraceId: {TraceId}",
                    _userManager.GetUserId(User) ?? "Anonymous",
                    GetModelStateErrorSummary(),
                    HttpContext.TraceIdentifier);

                return View(report);
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(Create));
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Upvote(int id)
        {
            try
            {
                var report = _reportsRepository.GetReportById(id);

                if (report == null)
                {
                    _logger.LogWarning(
                        "Upvote requested for non-existent report. ReportId: {ReportId}; UserId: {UserId}; TraceId: {TraceId}",
                        id,
                        _userManager.GetUserId(User) ?? "Anonymous",
                        HttpContext.TraceIdentifier);

                    return RedirectToAction(nameof(Index));
                }

                var currentUserId = _userManager.GetUserId(User);

                if (string.IsNullOrWhiteSpace(currentUserId))
                {
                    _logger.LogWarning(
                        "Upvote requested by authenticated request with no resolved user ID. ReportId: {ReportId}; Path: {Path}; TraceId: {TraceId}",
                        id,
                        HttpContext.Request.Path,
                        HttpContext.TraceIdentifier);

                    return Challenge();
                }

                if (report.ReporterId == currentUserId)
                {
                    _logger.LogWarning(
                        "User attempted to upvote own report. ReportId: {ReportId}; UserId: {UserId}; TraceId: {TraceId}",
                        id,
                        currentUserId,
                        HttpContext.TraceIdentifier);

                    TempData["ErrorMessage"] = "You cannot upvote your own report.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (User.IsInRole(UserRoles.Investigator))
                {
                    _logger.LogWarning(
                        "Investigator attempted to upvote report. ReportId: {ReportId}; UserId: {UserId}; Roles: {Roles}; TraceId: {TraceId}",
                        id,
                        currentUserId,
                        GetCurrentUserRoles(),
                        HttpContext.TraceIdentifier);

                    TempData["ErrorMessage"] = "Investigators cannot upvote reports.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var nowUpvoted = _reportsRepository.ToggleUpvoteReport(id, currentUserId);

                _logger.LogInformation(
                    "Report upvote toggled. ReportId: {ReportId}; UserId: {UserId}; NowUpvoted: {NowUpvoted}; TraceId: {TraceId}",
                    id,
                    currentUserId,
                    nowUpvoted,
                    HttpContext.TraceIdentifier);

                TempData["SuccessMessage"] = nowUpvoted
                    ? "Report upvoted."
                    : "Your upvote was removed.";

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(Upvote), id);
            }
        }

        [Authorize(Roles = UserRoles.Investigator)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            try
            {
                var existingReport = _reportsRepository.GetReportById(id);

                if (existingReport == null)
                {
                    _logger.LogWarning(
                        "Status update requested for non-existent report. ReportId: {ReportId}; UserId: {UserId}; RequestedStatus: {Status}; TraceId: {TraceId}",
                        id,
                        _userManager.GetUserId(User) ?? "Anonymous",
                        status,
                        HttpContext.TraceIdentifier);

                    return RedirectToAction(nameof(Index));
                }

                var oldStatus = existingReport.Status;

                var allowedStatuses = new List<string>
                {
                    "Open",
                    "Being Investigated",
                    "Resolved",
                    "No Action Required"
                };

                // Only allow known statuses.
                if (!allowedStatuses.Contains(status))
                {
                    _logger.LogWarning(
                        "Invalid status update rejected. ReportId: {ReportId}; UserId: {UserId}; RequestedStatus: {Status}; TraceId: {TraceId}",
                        id,
                        _userManager.GetUserId(User) ?? "Anonymous",
                        status,
                        HttpContext.TraceIdentifier);

                    TempData["ErrorMessage"] = "Invalid status selected.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (oldStatus == status)
                {
                    _logger.LogInformation(
                        "Status update ignored because status was unchanged. ReportId: {ReportId}; UserId: {UserId}; Status: {Status}; TraceId: {TraceId}",
                        id,
                        _userManager.GetUserId(User) ?? "Anonymous",
                        status,
                        HttpContext.TraceIdentifier);

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

                _logger.LogInformation(
                    "Report status updated successfully. ReportId: {ReportId}; UserId: {UserId}; OldStatus: {OldStatus}; NewStatus: {NewStatus}; TraceId: {TraceId}",
                    id,
                    actor.UserId,
                    oldStatus,
                    status,
                    HttpContext.TraceIdentifier);

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
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(UpdateStatus), id);
            }
        }

        [Authorize(Roles = UserRoles.Investigator)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveInvestigation(int id, string actionTaken, string? investigatorPhone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(actionTaken))
                {
                    _logger.LogWarning(
                        "Investigation save rejected because action details were empty. ReportId: {ReportId}; UserId: {UserId}; TraceId: {TraceId}",
                        id,
                        _userManager.GetUserId(User) ?? "Anonymous",
                        HttpContext.TraceIdentifier);

                    TempData["ErrorMessage"] = "Investigation action details are required.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var reportBeforeUpdate = _reportsRepository.GetReportById(id);

                if (reportBeforeUpdate == null)
                {
                    _logger.LogWarning(
                        "Investigation save requested for non-existent report. ReportId: {ReportId}; UserId: {UserId}; TraceId: {TraceId}",
                        id,
                        _userManager.GetUserId(User) ?? "Anonymous",
                        HttpContext.TraceIdentifier);

                    return RedirectToAction(nameof(Index));
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

                _logger.LogInformation(
                    "Investigation saved successfully. ReportId: {ReportId}; UserId: {UserId}; WasUpdate: {WasUpdate}; HasInvestigatorPhone: {HasInvestigatorPhone}; TraceId: {TraceId}",
                    id,
                    actor.UserId,
                    hadInvestigation,
                    !string.IsNullOrWhiteSpace(investigatorPhone),
                    HttpContext.TraceIdentifier);

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
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(SaveInvestigation), id);
            }
        }

        [Authorize]
        [HttpGet]
        public IActionResult Edit(int id)
        {
            try
            {
                var report = _reportsRepository.GetReportById(id);

                if (report == null)
                {
                    _logger.LogWarning(
                        "Edit GET requested for non-existent report. ReportId: {ReportId}; UserId: {UserId}; Path: {Path}; TraceId: {TraceId}",
                        id,
                        _userManager.GetUserId(User) ?? "Anonymous",
                        HttpContext.Request.Path,
                        HttpContext.TraceIdentifier);

                    return RedirectToAction(nameof(Index));
                }

                if (!UserCanManageReport(report))
                {
                    return ObfuscateReportAccessFailure(id, nameof(Edit));
                }

                _logger.LogInformation(
                    "Edit GET allowed. ReportId: {ReportId}; UserId: {UserId}; TraceId: {TraceId}",
                    id,
                    _userManager.GetUserId(User),
                    HttpContext.TraceIdentifier);

                return View(report);
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(Edit), id);
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(10 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
        public async Task<IActionResult> Edit(int id, Report updatedReport, IFormFile? imageFile)
        {
            try
            {
                if (id != updatedReport.Id)
                {
                    _logger.LogWarning(
                        "Edit POST route/body ID mismatch. RouteReportId: {RouteReportId}; BodyReportId: {BodyReportId}; UserId: {UserId}; TraceId: {TraceId}",
                        id,
                        updatedReport.Id,
                        _userManager.GetUserId(User) ?? "Anonymous",
                        HttpContext.TraceIdentifier);

                    return RedirectToAction(nameof(Index));
                }

                var existingReport = _reportsRepository.GetReportById(id);

                if (existingReport == null)
                {
                    _logger.LogWarning(
                        "Edit POST requested for non-existent report. ReportId: {ReportId}; UserId: {UserId}; TraceId: {TraceId}",
                        id,
                        _userManager.GetUserId(User) ?? "Anonymous",
                        HttpContext.TraceIdentifier);

                    return RedirectToAction(nameof(Index));
                }

                if (!UserCanManageReport(existingReport))
                {
                    return ObfuscateReportAccessFailure(id, "Edit POST");
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
                            _logger.LogWarning(
                                "Report edit image upload rejected. ReportId: {ReportId}; UserId: {UserId}; FileName: {FileName}; FileSize: {FileSize}; ContentType: {ContentType}; Reason: {Reason}; TraceId: {TraceId}",
                                id,
                                _userManager.GetUserId(User) ?? "Anonymous",
                                imageFile.FileName,
                                imageFile.Length,
                                imageFile.ContentType,
                                uploadResult.ErrorMessage,
                                HttpContext.TraceIdentifier);

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

                    _logger.LogInformation(
                        "Report edited successfully. ReportId: {ReportId}; UserId: {UserId}; HasNewImage: {HasNewImage}; TraceId: {TraceId}",
                        existingReport.Id,
                        _userManager.GetUserId(User),
                        imageFile != null && imageFile.Length > 0,
                        HttpContext.TraceIdentifier);

                    return RedirectToAction(nameof(Details), new { id = existingReport.Id });
                }

                _logger.LogWarning(
                    "Report edit failed validation. ReportId: {ReportId}; UserId: {UserId}; ModelStateErrors: {ModelStateErrors}; TraceId: {TraceId}",
                    id,
                    _userManager.GetUserId(User) ?? "Anonymous",
                    GetModelStateErrorSummary(),
                    HttpContext.TraceIdentifier);

                updatedReport.ImageUrl = existingReport.ImageUrl;
                return View(updatedReport);
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(Edit), id);
            }
        }

        [Authorize]
        [HttpGet]
        public IActionResult Delete(int id)
        {
            try
            {
                var report = _reportsRepository.GetReportById(id);

                if (report == null)
                {
                    _logger.LogWarning(
                        "Delete GET requested for non-existent report. ReportId: {ReportId}; UserId: {UserId}; Path: {Path}; TraceId: {TraceId}",
                        id,
                        _userManager.GetUserId(User) ?? "Anonymous",
                        HttpContext.Request.Path,
                        HttpContext.TraceIdentifier);

                    return RedirectToAction(nameof(Index));
                }

                if (!UserCanManageReport(report))
                {
                    return ObfuscateReportAccessFailure(id, nameof(Delete));
                }

                _logger.LogInformation(
                    "Delete GET allowed. ReportId: {ReportId}; UserId: {UserId}; TraceId: {TraceId}",
                    id,
                    _userManager.GetUserId(User),
                    HttpContext.TraceIdentifier);

                return View(report);
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(Delete), id);
            }
        }

        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            try
            {
                var report = _reportsRepository.GetReportById(id);

                if (report == null)
                {
                    _logger.LogWarning(
                        "Delete POST requested for non-existent report. ReportId: {ReportId}; UserId: {UserId}; TraceId: {TraceId}",
                        id,
                        _userManager.GetUserId(User) ?? "Anonymous",
                        HttpContext.TraceIdentifier);

                    return RedirectToAction(nameof(Index));
                }

                if (!UserCanManageReport(report))
                {
                    return ObfuscateReportAccessFailure(id, "Delete POST");
                }

                var userId = _userManager.GetUserId(User);

                _imageUploadService.DeleteImage(report.ImageUrl);
                _reportsRepository.DeleteReport(id);

                _logger.LogWarning(
                    "Report deleted by owner. ReportId: {ReportId}; UserId: {UserId}; HadImage: {HadImage}; TraceId: {TraceId}",
                    id,
                    userId,
                    !string.IsNullOrWhiteSpace(report.ImageUrl),
                    HttpContext.TraceIdentifier);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(DeleteConfirmed), id);
            }
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

        private IActionResult ObfuscateReportAccessFailure(int reportId, string attemptedAction)
        {
            _logger.LogWarning(
                "Obfuscated report access-control failure. Action: {Action}; ReportId: {ReportId}; UserId: {UserId}; Authenticated: {Authenticated}; IsInvestigator: {IsInvestigator}; Roles: {Roles}; Path: {Path}; TraceId: {TraceId}",
                attemptedAction,
                reportId,
                _userManager.GetUserId(User) ?? "Anonymous",
                User.Identity?.IsAuthenticated == true,
                User.IsInRole(UserRoles.Investigator),
                GetCurrentUserRoles(),
                HttpContext.Request.Path,
                HttpContext.TraceIdentifier);

            return RedirectToAction(nameof(Index));
        }

        private IActionResult HandleControllerException(Exception ex, string actionName, int? reportId = null)
        {
            _logger.LogError(
                ex,
                "Unhandled controller exception. Action: {Action}; ReportId: {ReportId}; UserId: {UserId}; Authenticated: {Authenticated}; Roles: {Roles}; Path: {Path}; TraceId: {TraceId}",
                actionName,
                reportId,
                _userManager.GetUserId(User) ?? "Anonymous",
                User.Identity?.IsAuthenticated == true,
                GetCurrentUserRoles(),
                HttpContext.Request.Path,
                HttpContext.TraceIdentifier);

            return View("Error");
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

        private string GetModelStateErrorSummary()
        {
            var errors = ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .Select(entry =>
                    $"{entry.Key}: {string.Join(", ", entry.Value!.Errors.Select(error => error.ErrorMessage))}");

            return string.Join(" | ", errors);
        }
    }
}