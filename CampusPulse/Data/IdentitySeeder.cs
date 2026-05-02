using CampusPulse.Models;
using Microsoft.AspNetCore.Identity;

namespace CampusPulse.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

            string[] roles =
            {
                UserRoles.Reporter,
                UserRoles.Investigator
            };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Testing Account. Probably fine to hardcode it for now. Might want to work on a permanent solution later.
            const string investigatorEmail = "investigator@campuspulse.local";
            const string investigatorPassword = "Investigator123!";

            var investigator = await userManager.FindByEmailAsync(investigatorEmail);

            if (investigator == null)
            {
                investigator = new IdentityUser
                {
                    UserName = investigatorEmail,
                    Email = investigatorEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(investigator, investigatorPassword);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new Exception($"Could not create investigator user: {errors}");
                }
            }

            if (!await userManager.IsInRoleAsync(investigator, UserRoles.Investigator))
            {
                await userManager.AddToRoleAsync(investigator, UserRoles.Investigator);
            }
        }
    }
}