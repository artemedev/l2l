using System.Collections.Generic;

namespace l2l_aggregator.Services.ControllerService
{
    public static class PlcErrorDecoder
    {
        public static List<string> DecodeNonFatalErrors(ushort word)
        {
            var messages = new List<string>();

            if ((word & (1 << 0)) != 0)
                messages.Add("Превышен лимит ответа ПК (1500ms).");

            if ((word & (1 << 1)) != 0)
                messages.Add("Превышен лимит ответа ПЛК (логика ПК).");

            if ((word & (1 << 2)) != 0)
                messages.Add("Нет сигнала готовности от принтера.");

            if (messages.Count == 0)
                messages.Add("Не критических ошибок не обнаружено.");

            return messages;
        }

        public static List<string> DecodeFatalErrors(ushort word)
        {
            var messages = new List<string>();

            if ((word & (1 << 0)) != 0)
                messages.Add("Авария сети питания 220В.");
            if ((word & (1 << 1)) != 0)
                messages.Add("Сработал нижний концевой выключатель (SW_1).");
            if ((word & (1 << 2)) != 0)
                messages.Add("Сработал верхний концевой выключатель (SW_2).");
            if ((word & (1 << 3)) != 0)
                messages.Add("Недостаточное расстояние от камеры до верхнего слоя короба.");
            if ((word & (1 << 4)) != 0)
                messages.Add("Низкий уровень освещенности.");
            if ((word & (1 << 5)) != 0)
                messages.Add("Нарушена цепь безопасности (кнопка / обрыв).");
            if ((word & (1 << 6)) != 0)
                messages.Add("Датчик Home сработал слишком рано (>10 мм).");
            if ((word & (1 << 7)) != 0)
                messages.Add("Стрела прошла Home >10 мм, датчик не сработал.");
            if ((word & (1 << 8)) != 0)
                messages.Add("Истек таймер позиционирования, позиция не достигнута.");
            if ((word & (1 << 9)) != 0)
                messages.Add("Zero остался активен после таймера позиционирования.");
            if ((word & (1 << 10)) != 0)
                messages.Add("Zero остался активен при выходе за RetreatPosition.");
            if ((word & (1 << 11)) != 0)
                messages.Add("При движении вниз Zero не сработал после таймера.");
            if ((word & (1 << 12)) != 0)
                messages.Add("Zero не сработал при превышении <-10 мм.");
            if ((word & (1 << 13)) != 0)
                messages.Add("Zero сработал слишком рано при движении вниз.");
            if ((word & (1 << 14)) != 0)
                messages.Add("Home не сработал при превышении >10 мм от Zero.");

            if (messages.Count == 0)
                messages.Add("Критических ошибок не обнаружено.");

            return messages;
        }
    }
}
