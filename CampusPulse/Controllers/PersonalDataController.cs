using CampusPulse.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace CampusPulse.Controllers
{
    [Authorize]
    public class PersonalDataController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IUserDataService _userDataService;

        public PersonalDataController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IUserDataService userDataService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _userDataService = userDataService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Download()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var personalData = await _userDataService.BuildPersonalDataExportAsync(user);

            var json = JsonSerializer.Serialize(personalData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var fileBytes = Encoding.UTF8.GetBytes(json);
            var fileName = $"campuspulse-personal-data-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

            return File(fileBytes, "application/json", fileName);
        }

        [HttpGet]
        public IActionResult Delete()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string password)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Please enter your password.");
                return View("Delete");
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, password);

            if (!passwordValid)
            {
                ModelState.AddModelError(string.Empty, "The password entered is incorrect.");
                return View("Delete");
            }

            await _userDataService.DeleteOrAnonymiseUserDataAsync(user);

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View("Delete");
            }

            await _signInManager.SignOutAsync();

            TempData["SuccessMessage"] = "Your account and personal data have been deleted or anonymised.";

            return RedirectToAction("Index", "Home");
        }
    }
}