using System;

namespace l2l_aggregator.Models.AggregationModels
{
    public class AggregationState
    {
        // Имя пользователя, к которому привязано состояние.
        public string Username { get; set; }
        // Идентификатор текущего задания (DOCID).
        public long TaskId { get; set; }
        // JSON шаблона (FR3 XML), выбранного пользователем.
        public string TemplateJson { get; set; }
        // JSON прогресса агрегации: номер паллеты, коробки, слоя, DM-ячейки и т.д.
        public string ProgressJson { get; set; }
        // Дата и время последнего обновления состояния.

        public string TaskInfoJson { get; set; }
        public DateTime LastUpdated { get; set; }
    }

}
