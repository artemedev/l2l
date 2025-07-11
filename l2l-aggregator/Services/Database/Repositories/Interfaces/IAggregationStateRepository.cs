using l2l_aggregator.Models.AggregationModels;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.Database.Repositories.Interfaces
{
    public interface IAggregationStateRepository
    {
        Task SaveStateAsync(AggregationState state);
        Task<AggregationState?> LoadStateAsync(string username);
        Task ClearStateAsync(string username);
    }
}
