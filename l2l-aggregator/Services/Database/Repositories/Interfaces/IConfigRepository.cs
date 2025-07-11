using System.Threading.Tasks;

namespace l2l_aggregator.Services.Database.Repositories.Interfaces
{
    public interface IConfigRepository
    {
        Task<string?> GetConfigValueAsync(string key);
        Task SetConfigValueAsync(string key, string value);
    }
}
