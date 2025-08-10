using l2l_aggregator.Models.Configuration;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.Configuration
{
    public interface IConfigurationFileService
    {
        Task<DeviceConfiguration> LoadConfigurationAsync();
        Task SaveConfigurationAsync(DeviceConfiguration configuration);
        Task<string?> GetConfigValueAsync(string key);
        Task SetConfigValueAsync(string key, string? value);
    }
}
