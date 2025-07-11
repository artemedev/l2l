using l2l_aggregator.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.Database.Repositories.Interfaces
{
    public interface IUserAuthRepository
    {
        Task SaveUserAuthAsync(UserAuthResponse response);
        Task<List<UserAuthResponse>> GetUserAuthAsync();
        Task<string?> GetLastUserIdAsync();
        Task<bool> ValidateAdminUserAsync(string username, string password);
    }
}
