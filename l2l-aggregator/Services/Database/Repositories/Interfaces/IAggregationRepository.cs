using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.Database.Repositories.Interfaces
{
    public interface IAggregationRepository
    {
        /// <summary>
        /// Сохраняет данные агрегации в локальную базу данных
        /// </summary>
        /// <param name="aggregationData">Список данных агрегации (UNID, CHECK_BAR_CODE)</param>
        /// <returns>True если сохранение прошло успешно</returns>
        Task<bool> LogAggregationBatchAsync(List<(string UNID, string CHECK_BAR_CODE)> aggregationData);

        /// <summary>
        /// Получает все записи агрегации из локальной базы данных
        /// </summary>
        /// <returns>Список всех записей агрегации</returns>
        Task<List<(string UNID, string CHECK_BAR_CODE)>> GetAllAggregationDataAsync();

        /// <summary>
        /// Очищает все данные агрегации из локальной базы данных
        /// </summary>
        /// <returns>True если очистка прошла успешно</returns>
        Task<bool> ClearAggregationDataAsync();

        /// <summary>
        /// Получает количество записей агрегации в локальной базе данных
        /// </summary>
        /// <returns>Количество записей</returns>
        Task<int> GetAggregationCountAsync();
    }

}
