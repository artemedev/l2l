using l2l_aggregator.Models;
using l2l_aggregator.Services.Database;
using l2l_aggregator.Services.Notification.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace l2l_aggregator.Services
{
    public class DatabaseDataService
    {
        private readonly RemoteDatabaseService _remoteDatabaseService;
        private readonly INotificationService _notificationService;
        private bool _isConnectionInitialized = false;

        public DatabaseDataService(
            RemoteDatabaseService remoteDatabaseService,
            INotificationService notificationService)
        {
            _remoteDatabaseService = remoteDatabaseService;
            _notificationService = notificationService;
        }

        // Метод для инициализации подключения (вызывается один раз)
        private async Task<bool> EnsureConnection()
        {
            if (!_isConnectionInitialized)
            {
                _isConnectionInitialized = await _remoteDatabaseService.InitializeConnection();
                if (_isConnectionInitialized)
                {
                    _notificationService.ShowMessage("Соединение с удаленной БД установлено", NotificationType.Success);
                }
            }
            return _isConnectionInitialized;
        }

        // Принудительная проверка соединения
        public async Task<bool> TestConnection()
        {
            try
            {
                _isConnectionInitialized = false; // Сбрасываем флаг для повторной проверки
                return await EnsureConnection();
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка проверки подключения: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        // ---------------- AUTH ----------------
        public async Task<UserAuthResponse?> Login(string login, string password)
        {
            try
            {
                if (!await EnsureConnection())
                {
                    _notificationService.ShowMessage("Нет подключения к удаленной БД", NotificationType.Error);
                    return null;
                }

                var response = await _remoteDatabaseService.Login(login, password);

                if (response?.AUTH_OK == "1")
                {
                    _notificationService.ShowMessage($"Добро пожаловать, {response.USER_NAME}!", NotificationType.Success);

                    return response;
                }
                else
                {
                    var errorMsg = response?.ERROR_TEXT ?? "Неверный логин или пароль";
                    _notificationService.ShowMessage(errorMsg, NotificationType.Error);
                }

                return null;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка входа: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        // Проверка прав администратора
        public async Task<bool> CheckAdminRole(string userId)
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return false;
                }

                if (long.TryParse(userId, out var userIdLong))
                {
                    return await _remoteDatabaseService.CheckAdminRole(userIdLong);
                }

                return false;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка проверки прав администратора: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        //Регистрация устройства
        public async Task<ArmDeviceRegistrationResponse?> RegisterDevice(ArmDeviceRegistrationRequest data)
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return null;
                }

                var response = await _remoteDatabaseService.RegisterDevice(data);
                if (response != null)
                {
                    _notificationService.ShowMessage($"Устройство '{response.DEVICE_NAME}' зарегистрировано", NotificationType.Success);
                }

                return response;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка регистрации устройства: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        // ---------------- JOB LIST ----------------
        public async Task<ArmJobResponse?> GetJobs(string userId)
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return null;
                }

                var response = await _remoteDatabaseService.GetJobs(userId);
                if (response?.RECORDSET?.Any() == true)
                {
                    _notificationService.ShowMessage($"Загружено {response.RECORDSET.Count} заданий", NotificationType.Info);
                }
                else
                {
                    _notificationService.ShowMessage("Задания не найдены", NotificationType.Warning);
                }

                return response;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка получения заданий: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        // ---------------- JOB DETAILS ----------------
        public async Task<ArmJobInfoRecord?> GetJobDetails(long docId)
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return null;
                }

                var response = await _remoteDatabaseService.GetJobDetails(docId);
                if (response != null)
                {
                    _notificationService.ShowMessage($"Задание {response.DOC_NUM} загружено", NotificationType.Success);
                }

                return response;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка получения деталей задания: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        public async Task<long?> GetCurrentJobId()
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return null;
                }

                return await _remoteDatabaseService.GetCurrentJobId();
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка загрузки незавершенного задания: {ex.Message}", NotificationType.Error);
                return null;
            }
        }
        // Закрытие задания
        public async Task<bool> CloseJob()
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return false;
                }

                var result = await _remoteDatabaseService.CloseJob();
                if (result)
                {
                    _notificationService.ShowMessage("Задание успешно закрыто", NotificationType.Success);
                }

                return result;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка закрытия задания: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        // ---------------- Получение Sgtin ----------------
        public async Task<ArmJobSgtinResponse?> GetSgtin(long docId)
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return null;
                }

                var response = await _remoteDatabaseService.GetSgtin(docId);
                if (response?.RECORDSET?.Any() == true)
                {
                    _notificationService.ShowMessage($"Загружено {response.RECORDSET.Count} SGTIN кодов", NotificationType.Info);
                }

                return response;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка получения SGTIN: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        // ---------------- Получение Sscc ----------------
        public async Task<ArmJobSsccResponse?> GetSscc(long docId)
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return null;
                }

                var response = await _remoteDatabaseService.GetSscc(docId);
                if (response?.RECORDSET?.Any() == true)
                {
                    _notificationService.ShowMessage($"Загружено {response.RECORDSET.Count} SSCC кодов", NotificationType.Info);
                }

                return response;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка получения SSCC: {ex.Message}", NotificationType.Error);
                return null;
            }
        }


        // ---------------- SESSION MANAGEMENT ----------------
        public async Task<bool> StartAggregationSession()
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return false;
                }


                var result = await _remoteDatabaseService.StartSession();
                if (result)
                {
                    _notificationService.ShowMessage($"Сессия агрегации начата", NotificationType.Success);
                }

                return true;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка начала сессии агрегации: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        public async Task<bool> CloseAggregationSession()
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return false;
                }

                var result = await _remoteDatabaseService.CloseSession();
                if (result)
                {
                    _notificationService.ShowMessage("Сессия агрегации завершена", NotificationType.Success);
                }
                return result;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка закрытия сессии агрегации: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        // ---------------- Получение счетчиков ARM ----------------
        public async Task<ArmCountersResponse?> GetArmCounters()
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return null;
                }

                var response = await _remoteDatabaseService.GetArmCounters();
                if (response?.RECORDSET?.Any() == true)
                {
                    _notificationService.ShowMessage($"Загружено {response.RECORDSET.Count} счетчиков ARM", NotificationType.Info);
                }

                return response;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка получения счетчиков ARM: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        public async Task<bool> LogAggregationCompletedBatch(List<(string UNID, string CHECK_BAR_CODE)> aggregationData)
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return false;
                }

                var result = await _remoteDatabaseService.LogAggregationBatch(aggregationData);
                // Затем сохраняем в локальную БД (асинхронно, не блокируем основной процесс)
                
                if (result)
                {
                    _notificationService.ShowMessage($"Batch агрегация успешно зарегистрирована ({aggregationData.Count} записей)", NotificationType.Success);
                }

                return result;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка batch логирования агрегации: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        // ---------------- Поиск кода SSCC ----------------
        public async Task<ArmJobSsccRecord?> FindSsccCode(string code)
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return null;
                }

                return await _remoteDatabaseService.FindSsccCode(code);
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка поиска SSCC кода: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        // ---------------- Поиск кода UN (SGTIN) ----------------
        public async Task<ArmJobSgtinRecord?> FindUnCode(string code)
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return null;
                }

                return await _remoteDatabaseService.FindUnCode(code);
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка поиска UN кода: {ex.Message}", NotificationType.Error);
                return null;
            }
        }
        // ---------------- Получение агрегированных UN кодов ----------------
        public async Task<List<string>> GetAggregatedUnCodes()
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return new List<string>();
                }

                var response = await _remoteDatabaseService.GetAggregatedUnCodes();
                return response ?? new List<string>();
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка получения агрегированных кодов: {ex.Message}", NotificationType.Error);
                return new List<string>();
            }
        }

        // ---------------- Разагрегация коробки ----------------
        public async Task<bool> ClearBoxAggregation(string checkBarCode)
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return false;
                }

                var result = await _remoteDatabaseService.ClearBoxAggregation(checkBarCode);
                if (result)
                {
                    _notificationService.ShowMessage("Разагрегация коробки выполнена успешно", NotificationType.Success);
                }

                return result;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка разагрегации коробки: {ex.Message}", NotificationType.Error);
                return false;
            }
        }
        // ---------------- Получение количества агрегированных коробов ----------------
        public async Task<int> GetAggregatedBoxesCount()
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return 0;
                }

                return await _remoteDatabaseService.GetAggregatedBoxesCount();
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка получения количества агрегированных коробов: {ex.Message}", NotificationType.Error);
                return 0;
            }
        }

        // ---------------- Резервирование свободного короба ----------------
        public async Task<ArmJobSsccRecord?> ReserveFreeBox()
        {
            try
            {
                if (!await EnsureConnection())
                {
                    return null;
                }

                var response = await _remoteDatabaseService.ReserveFreeBox();
                if (response != null)
                {
                    _notificationService.ShowMessage($"Зарезервирован короб: {response.CHECK_BAR_CODE}", NotificationType.Success);
                }
                else
                {
                    _notificationService.ShowMessage("Нет доступных свободных коробов", NotificationType.Warning);
                }

                return response;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка резервирования свободного короба: {ex.Message}", NotificationType.Error);
                return null;
            }
        }
    }
}