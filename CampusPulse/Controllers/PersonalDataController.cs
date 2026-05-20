using CampusPulse.Services;
using CampusPulse.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace CampusPulse.Controllers
{
    [Authorize]
    public class PersonalDataController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IUserDataService _userDataService;
        private readonly ILogger<PersonalDataController> _logger;

        public PersonalDataController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IUserDataService userDataService,
            ILogger<PersonalDataController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _userDataService = userDataService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                _logger.LogInformation(
                    "Personal data page opened. UserId: {UserId}; Authenticated: {Authenticated}; Roles: {Roles}; Path: {Path}; TraceId: {TraceId}",
                    _userManager.GetUserId(User) ?? "Anonymous",
                    User.Identity?.IsAuthenticated == true,
                    GetCurrentUserRoles(),
                    HttpContext.Request.Path,
                    HttpContext.TraceIdentifier);

                return View();
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Download()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                {
                    _logger.LogWarning(
                        "Personal data download requested but no user could be resolved. Authenticated: {Authenticated}; Path: {Path}; TraceId: {TraceId}",
                        User.Identity?.IsAuthenticated == true,
                        HttpContext.Request.Path,
                        HttpContext.TraceIdentifier);

                    return Challenge();
                }

                _logger.LogInformation(
                    "Personal data download requested. UserId: {UserId}; Roles: {Roles}; TraceId: {TraceId}",
                    user.Id,
                    GetCurrentUserRoles(),
                    HttpContext.TraceIdentifier);

                var personalData = await _userDataService.BuildPersonalDataExportAsync(user);

                var json = JsonSerializer.Serialize(personalData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var fileBytes = Encoding.UTF8.GetBytes(json);
                var fileName = $"campuspulse-personal-data-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

                _logger.LogInformation(
                    "Personal data export generated successfully. UserId: {UserId}; FileName: {FileName}; FileSizeBytes: {FileSizeBytes}; TraceId: {TraceId}",
                    user.Id,
                    fileName,
                    fileBytes.Length,
                    HttpContext.TraceIdentifier);

                return File(fileBytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(Download));
            }
        }

        [HttpGet]
        public IActionResult Delete()
        {
            try
            {
                _logger.LogInformation(
                    "Account deletion confirmation page opened. UserId: {UserId}; Roles: {Roles}; Path: {Path}; TraceId: {TraceId}",
                    _userManager.GetUserId(User) ?? "Anonymous",
                    GetCurrentUserRoles(),
                    HttpContext.Request.Path,
                    HttpContext.TraceIdentifier);

                return View();
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(Delete));
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string password)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                {
                    _logger.LogWarning(
                        "Account deletion requested but no user could be resolved. Authenticated: {Authenticated}; Path: {Path}; TraceId: {TraceId}",
                        User.Identity?.IsAuthenticated == true,
                        HttpContext.Request.Path,
                        HttpContext.TraceIdentifier);

                    return Challenge();
                }

                _logger.LogWarning(
                    "Account deletion requested. UserId: {UserId}; Roles: {Roles}; TraceId: {TraceId}",
                    user.Id,
                    GetCurrentUserRoles(),
                    HttpContext.TraceIdentifier);

                if (string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogWarning(
                        "Account deletion rejected because password was missing. UserId: {UserId}; TraceId: {TraceId}",
                        user.Id,
                        HttpContext.TraceIdentifier);

                    ModelState.AddModelError(string.Empty, "Please enter your password.");
                    return View("Delete");
                }

                var passwordValid = await _userManager.CheckPasswordAsync(user, password);

                if (!passwordValid)
                {
                    _logger.LogWarning(
                        "Account deletion rejected because password was incorrect. UserId: {UserId}; AccessFailedCount: {AccessFailedCount}; TraceId: {TraceId}",
                        user.Id,
                        user.AccessFailedCount,
                        HttpContext.TraceIdentifier);

                    ModelState.AddModelError(string.Empty, "The password entered is incorrect.");
                    return View("Delete");
                }

                await _userDataService.DeleteOrAnonymiseUserDataAsync(user);

                _logger.LogWarning(
                    "User data anonymisation completed before account deletion. UserId: {UserId}; TraceId: {TraceId}",
                    user.Id,
                    HttpContext.TraceIdentifier);

                var result = await _userManager.DeleteAsync(user);

                if (!result.Succeeded)
                {
                    var errors = string.Join(" | ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));

                    _logger.LogError(
                        "Identity account deletion failed. UserId: {UserId}; Errors: {Errors}; TraceId: {TraceId}",
                        user.Id,
                        errors,
                        HttpContext.TraceIdentifier);

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    return View("Delete");
                }

                await _signInManager.SignOutAsync();

                _logger.LogWarning(
                    "Account deleted successfully and user signed out. DeletedUserId: {UserId}; TraceId: {TraceId}",
                    user.Id,
                    HttpContext.TraceIdentifier);

                TempData["SuccessMessage"] = "Your account and personal data have been deleted or anonymised.";

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                return HandleControllerException(ex, nameof(DeleteConfirmed));
            }
        }

        private IActionResult HandleControllerException(Exception ex, string actionName)
        {
            _logger.LogError(
                ex,
                "Unhandled personal data controller exception. Action: {Action}; UserId: {UserId}; Authenticated: {Authenticated}; Roles: {Roles}; Path: {Path}; TraceId: {TraceId}",
                actionName,
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
    }
}