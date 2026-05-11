using Microsoft.AspNetCore.Identity;

namespace CampusPulse.Services
{
    public interface IUserDataService
    {
        Task<object> BuildPersonalDataExportAsync(IdentityUser user);
        Task DeleteOrAnonymiseUserDataAsync(IdentityUser user);
    }
}