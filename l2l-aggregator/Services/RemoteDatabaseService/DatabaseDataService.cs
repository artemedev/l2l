// DatabaseDataService.cs - обновленный для работы с процедурами и статичным адресом БД
using l2l_aggregator.Models;
using l2l_aggregator.Services.Database;
using l2l_aggregator.Services.Database.Repositories.Interfaces;
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
        private readonly DatabaseService _localDatabaseService;
        //private readonly INotificationService _notificationService;
        private bool _isConnectionInitialized = false;

        public DatabaseDataService(
            RemoteDatabaseService remoteDatabaseService,
            DatabaseService localDatabaseService,
            INotificationService notificationService)
        {
            _remoteDatabaseService = remoteDatabaseService;
            _localDatabaseService = localDatabaseService;
            //_notificationService = notificationService;
        }

        // Метод для инициализации подключения (вызывается один раз)
        private bool EnsureConnection()
        {
            if (!_isConnectionInitialized)
            {
                _isConnectionInitialized = _remoteDatabaseService.InitializeConnection();
                if (_isConnectionInitialized)
                {
                    //_notificationService.ShowMessage("Соединение с удаленной БД установлено", NotificationType.Success);
                }
            }
            return _isConnectionInitialized;
        }

        // Принудительная проверка соединения
        public bool TestConnection()
        {
            try
            {
                _isConnectionInitialized = false; // Сбрасываем флаг для повторной проверки
                return EnsureConnection();
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка проверки подключения: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        // ---------------- AUTH ----------------
        public async Task<UserAuthResponse?> Login(string login, string password)
        {
            try
            {
                if (!EnsureConnection())
                {
                    //_notificationService.ShowMessage("Нет подключения к удаленной БД", NotificationType.Error);
                    return null;
                }

                var response = _remoteDatabaseService.Login(login, password);

                if (response?.AUTH_OK == "1")
                {
                    //await _localDatabaseService.UserAuth.SaveUserAuthAsync(response);
                    //_notificationService.ShowMessage($"Добро пожаловать, {response.USER_NAME}!", NotificationType.Success);

                    return response;
                }
                else
                {
                    var errorMsg = response?.ERROR_TEXT ?? "Неверный логин или пароль";
                    //_notificationService.ShowMessage(errorMsg, NotificationType.Error);
                }

                return null;
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка входа: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        // Проверка прав администратора
        public bool CheckAdminRole(string userId)
        {
            try
            {
                if (!EnsureConnection())
                {
                    return false;
                }

                if (long.TryParse(userId, out var userIdLong))
                {
                    return _remoteDatabaseService.CheckAdminRole(userIdLong);
                }

                return false;
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка проверки прав администратора: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        //Регистрация устройства
        public ArmDeviceRegistrationResponse? RegisterDevice(ArmDeviceRegistrationRequest data)
        {
            try
            {
                if (!EnsureConnection())
                {
                    return null;
                }

                var response = _remoteDatabaseService.RegisterDevice(data);
                if (response != null)
                {
                    //_notificationService.ShowMessage($"Устройство '{response.DEVICE_NAME}' зарегистрировано", NotificationType.Success);
                }

                return response;
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка регистрации устройства: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        // ---------------- JOB LIST ----------------
        public ArmJobResponse? GetJobs(string userId)
        {
            try
            {
                if (!EnsureConnection())
                {
                    return null;
                }

                var response = _remoteDatabaseService.GetJobs(userId);
                if (response?.RECORDSET?.Any() == true)
                {
                    //_notificationService.ShowMessage($"Загружено {response.RECORDSET.Count} заданий", NotificationType.Info);
                }
                else
                {
                    //_notificationService.ShowMessage("Задания не найдены", NotificationType.Warning);
                }

                return response;
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка получения заданий: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        // ---------------- JOB DETAILS ----------------
        public ArmJobInfoRecord? GetJobDetails(long docId)
        {
            try
            {
                if (!EnsureConnection())
                {
                    return null;
                }

                var response = _remoteDatabaseService.GetJobDetails(docId);
                if (response != null)
                {
                    //_notificationService.ShowMessage($"Задание {response.DOC_NUM} загружено", NotificationType.Success);
                }

                return response;
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка получения деталей задания: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        public long? GetCurrentJobId()
        {
            try
            {
                if (!EnsureConnection())
                {
                    return null;
                }

                return _remoteDatabaseService.GetCurrentJobId();
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка загрузки незавершенного задания: {ex.Message}", NotificationType.Error);
                return null;
            }
        }
        // Закрытие задания
        public bool CloseJob()
        {
            try
            {
                if (!EnsureConnection())
                {
                    return false;
                }

                var result = _remoteDatabaseService.CloseJob();
                if (result)
                {
                    //_notificationService.ShowMessage("Задание успешно закрыто", NotificationType.Success);
                }

                return result;
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка закрытия задания: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        // ---------------- Получение Sgtin ----------------
        public ArmJobSgtinResponse? GetSgtin(long docId)
        {
            try
            {
                if (!EnsureConnection())
                {
                    return null;
                }

                var response = _remoteDatabaseService.GetSgtin(docId);
                if (response?.RECORDSET?.Any() == true)
                {
                    //_notificationService.ShowMessage($"Загружено {response.RECORDSET.Count} SGTIN кодов", NotificationType.Info);
                }

                return response;
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка получения SGTIN: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        // ---------------- Получение Sscc ----------------
        public ArmJobSsccResponse? GetSscc(long docId)
        {
            try
            {
                if (!EnsureConnection())
                {
                    return null;
                }

                var response = _remoteDatabaseService.GetSscc(docId);
                if (response?.RECORDSET?.Any() == true)
                {
                    //_notificationService.ShowMessage($"Загружено {response.RECORDSET.Count} SSCC кодов", NotificationType.Info);
                }

                return response;
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка получения SSCC: {ex.Message}", NotificationType.Error);
                return null;
            }
        }


        // ---------------- SESSION MANAGEMENT ----------------
        public bool StartAggregationSession()
        {
            try
            {
                if (!EnsureConnection())
                {
                    return false;
                }


                var result = _remoteDatabaseService.StartSession();
                if (result)
                {
                    //_notificationService.ShowMessage($"Сессия агрегации начата", NotificationType.Success);
                }

                return true;
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка начала сессии агрегации: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        public bool CloseAggregationSession()
        {
            try
            {
                if (!EnsureConnection())
                {
                    return false;
                }

                var result = _remoteDatabaseService.CloseSession();
                if (result)
                {
                    //_notificationService.ShowMessage("Сессия агрегации завершена", NotificationType.Success);
                }
                return result;
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка закрытия сессии агрегации: {ex.Message}", NotificationType.Error);
                return false;
            }
        }
        //  ---------------- Логирование агрегации ----------------
        public bool LogAggregationCompleted(string UNID, string SSCCID)
        {
            try
            {
                if (!EnsureConnection())
                {
                    return false;
                }

                var result = _remoteDatabaseService.LogAggregation(UNID, SSCCID);
                if (result)
                {
                    //_notificationService.ShowMessage("Агрегация успешно зарегистрирована", NotificationType.Success);
                }

                return result;
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка логирования агрегации: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        // ---------------- Получение счетчиков ARM ----------------
        public ArmCountersResponse? GetArmCounters()
        {
            try
            {
                if (!EnsureConnection())
                {
                    return null;
                }

                var response = _remoteDatabaseService.GetArmCounters();
                if (response?.RECORDSET?.Any() == true)
                {
                    //_notificationService.ShowMessage($"Загружено {response.RECORDSET.Count} счетчиков ARM", NotificationType.Info);
                }

                return response;
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка получения счетчиков ARM: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        public bool LogAggregationCompletedBatch(List<(string UNID, string SSCCID)> aggregationData)
        {
            try
            {
                if (!EnsureConnection())
                {
                    return false;
                }

                var result = _remoteDatabaseService.LogAggregationBatch(aggregationData);
                if (result)
                {
                    //_notificationService.ShowMessage($"Batch агрегация успешно зарегистрирована ({aggregationData.Count} записей)", NotificationType.Success);
                }

                return result;
            }
            catch (Exception ex)
            {
                //_notificationService.ShowMessage($"Ошибка batch логирования агрегации: {ex.Message}", NotificationType.Error);
                return false;
            }
        }
    }
}