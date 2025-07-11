using Dapper;
using l2l_aggregator.Models;
using l2l_aggregator.Services.Database.Repositories.Interfaces;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.Database.Repositories
{
    // ========================== Конфиги настроек сервера, сканера, принтера, конроллера ==========================
    public class ConfigRepository : BaseRepository, IConfigRepository
    {
        public ConfigRepository(DatabaseInitializer dbService) : base(dbService) { }

        public Task<string?> GetConfigValueAsync(string key) =>
            WithConnectionAsync(conn =>
                conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT CONFIG_VALUE FROM CONFIG_INFO WHERE CONFIG_KEY = @key",
                    new { key }));

        public Task SetConfigValueAsync(string key, string? value) =>
            WithConnectionAsync(conn =>
                conn.ExecuteAsync(
                    @"UPDATE OR INSERT INTO CONFIG_INFO 
                  (CONFIG_KEY, CONFIG_VALUE) 
                  VALUES (@key, @value) MATCHING (CONFIG_KEY);",
                    new { key, value }));
    }
}
