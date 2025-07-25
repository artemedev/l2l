using Dapper;
using l2l_aggregator.Services.Database.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.Database.Repositories
{
    public class AggregationRepository : BaseRepository, IAggregationRepository
    {
        public AggregationRepository(DatabaseInitializer dbService) : base(dbService) { }

        /// <summary>
        /// Сохраняет данные агрегации в локальную базу данных
        /// </summary>
        public async Task<bool> LogAggregationBatchAsync(List<(string UNID, string CHECK_BAR_CODE)> aggregationData)
        {
            if (aggregationData == null || aggregationData.Count == 0)
                return true;

            try
            {
                return await WithConnectionAsync(async (connection) =>
                {
                    using var transaction = connection.BeginTransaction();
                    try
                    {
                        const string sql = @"
                            INSERT INTO AGGREGATION_LOG (UNID, CHECK_BAR_CODE, CREATED_AT)
                            VALUES (@UNID, @CHECK_BAR_CODE, @CREATED_AT)";

                        var parameters = aggregationData.Select(item => new
                        {
                            UNID = item.UNID,
                            CHECK_BAR_CODE = item.CHECK_BAR_CODE,
                            CREATED_AT = DateTime.Now
                        }).ToList();

                        var result = await connection.ExecuteAsync(sql, parameters, transaction);

                        transaction.Commit();
                        return result > 0;
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не пробрасываем её дальше, чтобы не ломать основной процесс
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения в локальную БД: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получает все записи агрегации из локальной базы данных
        /// </summary>
        public async Task<List<(string UNID, string CHECK_BAR_CODE)>> GetAllAggregationDataAsync()
        {
            try
            {
                return await WithConnectionAsync(async (connection) =>
                {
                    const string sql = @"
                        SELECT UNID, CHECK_BAR_CODE 
                        FROM AGGREGATION_LOG 
                        ORDER BY CREATED_AT DESC";

                    var result = await connection.QueryAsync(sql);

                    var aggregationList = new List<(string UNID, string CHECK_BAR_CODE)>();

                    foreach (var record in result)
                    {
                        var unid = record.UNID?.ToString() ?? "";
                        var checkBarCode = record.CHECK_BAR_CODE?.ToString() ?? "";

                        if (!string.IsNullOrWhiteSpace(unid) && !string.IsNullOrWhiteSpace(checkBarCode))
                        {
                            aggregationList.Add((unid, checkBarCode));
                        }
                    }

                    return aggregationList;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения данных из локальной БД: {ex.Message}");
                return new List<(string, string)>();
            }
        }

        /// <summary>
        /// Очищает все данные агрегации из локальной базы данных
        /// </summary>
        public async Task<bool> ClearAggregationDataAsync()
        {
            try
            {
                return await WithConnectionAsync(async (connection) =>
                {
                    using var transaction = connection.BeginTransaction();
                    try
                    {
                        const string sql = "DELETE FROM AGGREGATION_LOG";
                        await connection.ExecuteAsync(sql, transaction: transaction);

                        transaction.Commit();
                        return true;
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка очистки локальной БД: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получает количество записей агрегации в локальной базе данных
        /// </summary>
        public async Task<int> GetAggregationCountAsync()
        {
            try
            {
                return await WithConnectionAsync(async (connection) =>
                {
                    const string sql = "SELECT COUNT(*) FROM AGGREGATION_LOG";
                    var result = await connection.QueryFirstOrDefaultAsync<int>(sql);
                    return result;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения количества записей из локальной БД: {ex.Message}");
                return 0;
            }
        }
    }
}
