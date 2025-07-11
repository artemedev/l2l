using Dapper;
using FirebirdSql.Data.FirebirdClient;
using l2l_aggregator.Models.AggregationModels;
using l2l_aggregator.Services.Database.Repositories.Interfaces;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.Database.Repositories
{
    public class AggregationStateRepository : BaseRepository, IAggregationStateRepository
    {
        public AggregationStateRepository(DatabaseInitializer db) : base(db) { }

        public async Task SaveStateAsync(AggregationState state)
        {
            await WithConnectionAsync(async conn =>
            {
                using var cmd = new FbCommand(@"
        UPDATE OR INSERT INTO AGGREGATION_STATE
        (USERNAME, TASK_ID, TEMPLATE_JSON, PROGRESS_JSON, TASK_INFO_JSON, LAST_UPDATED)
        VALUES (@Username, @TaskId, @TemplateJson, @ProgressJson, @TaskInfoJson, CURRENT_TIMESTAMP)
        MATCHING (USERNAME)", conn);

                cmd.Parameters.Add(new FbParameter("@Username", state.Username));
                cmd.Parameters.Add(new FbParameter("@TaskId", state.TaskId));
                cmd.Parameters.Add(new FbParameter("@TemplateJson", state.TemplateJson));
                cmd.Parameters.Add(new FbParameter("@ProgressJson", state.ProgressJson));
                cmd.Parameters.Add(new FbParameter("@TaskInfoJson", state.TaskInfoJson));

                await cmd.ExecuteNonQueryAsync();
            });
        }

        public Task<AggregationState?> LoadStateAsync(string username) =>
            WithConnectionAsync(conn =>
                conn.QueryFirstOrDefaultAsync<AggregationState>(
                    @"SELECT 
                        USERNAME AS Username, 
                        TASK_ID AS TaskId, 
                        TEMPLATE_JSON AS TemplateJson, 
                        PROGRESS_JSON AS ProgressJson, 
                        TASK_INFO_JSON AS TaskInfoJson, 
                        LAST_UPDATED AS LastUpdated
                        FROM AGGREGATION_STATE 
                        WHERE USERNAME = @username",
                    new { username }));

        public Task ClearStateAsync(string username) =>
            WithConnectionAsync(conn =>
                conn.ExecuteAsync("DELETE FROM AGGREGATION_STATE WHERE USERNAME = @username", new { username }));
    }
}
