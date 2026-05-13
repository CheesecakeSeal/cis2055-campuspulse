using CampusPulse.Models;

namespace CampusPulse.Services
{
    public interface IUserDataService
    {
        Task<object> BuildPersonalDataExportAsync(ApplicationUser user);
        Task DeleteOrAnonymiseUserDataAsync(ApplicationUser user);
    }
}