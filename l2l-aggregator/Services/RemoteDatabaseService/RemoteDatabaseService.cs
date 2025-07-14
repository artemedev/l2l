using Dapper;
using FirebirdSql.Data.FirebirdClient;
using l2l_aggregator.Models;
using l2l_aggregator.Services.Database.Repositories.Interfaces;
using l2l_aggregator.Services.Notification.Interface;
using MD.Marking.Codes;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Transactions;

namespace l2l_aggregator.Services.Database
{
    public class RemoteDatabaseService
    {
        private readonly IConfigRepository _configRepository;
        private readonly INotificationService _notificationService;
        private long? _currentSessionId;
        private long? _currentDeviceId;
        private readonly string _connectionString;
        private IConfiguration _configuration;
        public RemoteDatabaseService(IConfigRepository configRepository, IConfiguration configuration, INotificationService notificationService)
        {
            _configRepository = configRepository;
            _configuration = configuration;
            _notificationService = notificationService;
            _connectionString = _configuration.GetConnectionString("FirebirdDatabase");
        }

        public async Task<bool> InitializeConnectionAsync()
        {
            try
            {
                _notificationService.ShowMessage($"Подключение к БД", NotificationType.Info);
                return await TestConnectionAsync();
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка инициализации подключения к БД: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                return false;

            try
            {
                using (FbConnection connection = new FbConnection(_connectionString))
                {
                    await connection.OpenAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка подключения к удаленной БД: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        private async Task<T> WithConnectionAsync<T>(Func<FbConnection, Task<T>> action)
        {
            using var connection = new FbConnection(_connectionString);
            await connection.OpenAsync();
            return await action(connection);
        }

        private async Task WithConnectionAsync(Func<FbConnection, Task> action)
        {
            using var connection = new FbConnection(_connectionString);
            await connection.OpenAsync();
            await action(connection);
        }

        // ---------------- AUTH ----------------
        public async Task<UserAuthResponse?> LoginAsync(string login, string password)
        {
            try
            {
                return await WithConnectionAsync(async conn =>
                {
                    var sql = "SELECT * FROM MARK_ARM_USER_AUTH(@USER_IDENT, @USER_PASSWD)";

                    var result = await conn.QueryFirstOrDefaultAsync(sql, new
                    {
                        USER_IDENT = login,
                        USER_PASSWD = password
                    });

                    if (result != null)
                    {
                        return new UserAuthResponse
                        {
                            USERID = result.USERID?.ToString(),
                            USER_NAME = result.USER_NAME,
                            PERSONID = result.PERSONID?.ToString(),
                            PERSON_NAME = result.PERSON_NAME,
                            PERSON_DELETE_FLAG = result.PERSON_DELETE_FLAG?.ToString(),
                            AUTH_OK = result.AUTH_OK?.ToString(),
                            ERROR_TEXT = result.ERROR_TEXT,
                            NEED_CHANGE_FLAG = result.NEED_CHANGE_FLAG?.ToString(),
                            EXPIRATION_DATE = result.EXPIRATION_DATE?.ToString("dd.MM.yyyy HH:mm:ss"),
                            REC_TYPE = result.REC_TYPE?.ToString()
                        };
                    }

                    return null;
                });
            }
            catch
            {
                throw;
            }
        }

        // ---------------- Проверка прав администратора ----------------
        public async Task<bool> CheckAdminRoleAsync(long userId)
        {
            try
            {
                return await WithConnectionAsync(async conn =>
                {
                    var sql = "SELECT * FROM ACL_CHECK_ADMIN_ROLE(@USERID)";

                    var result = await conn.QueryFirstOrDefaultAsync(sql, new { USERID = userId });

                    return result?.RES == 1;
                });
            }
            catch
            {
                throw;
            }
        }

        // ---------------- Регистрация устройства ----------------
        public async Task<ArmDeviceRegistrationResponse?> RegisterDeviceAsync(ArmDeviceRegistrationRequest data)
        {
            try
            {
                return await WithConnectionAsync(async conn =>
                {
                    var sql = @"SELECT * FROM MARK_ARM_DEVICE_REGISTER(
                        @NAME, @MAC_ADDRESS, @SERIAL_NUMBER, @NET_ADDRESS, 
                        @KERNEL_VERSION, @HARDWARE_VERSION, @SOFTWARE_VERSION, 
                        @FIRMWARE_VERSION, @DEVICE_TYPE)";

                    var result = await conn.QueryFirstOrDefaultAsync(sql, new
                    {
                        NAME = data.NAME,
                        MAC_ADDRESS = data.MAC_ADDRESS,
                        SERIAL_NUMBER = data.SERIAL_NUMBER,
                        NET_ADDRESS = data.NET_ADDRESS,
                        KERNEL_VERSION = data.KERNEL_VERSION,
                        HARDWARE_VERSION = data.HARDWARE_VERSION,
                        SOFTWARE_VERSION = data.SOFTWARE_VERSION,
                        FIRMWARE_VERSION = data.FIRMWARE_VERSION,
                        DEVICE_TYPE = data.DEVICE_TYPE
                    });

                    if (result != null)
                    {
                        _currentDeviceId = result.DEVICEID;

                        return new ArmDeviceRegistrationResponse
                        {
                            DEVICEID = result.DEVICEID?.ToString(),
                            DEVICE_NAME = result.DEVICE_NAME,
                            LICENSE_DATA = result.LICENSE_DATA?.ToString(),
                            SETTINGS_DATA = result.SETTINGS_DATA?.ToString()
                        };
                    }

                    return null;
                });
            }
            catch
            {
                throw;
            }
        }

        // ---------------- Загрузка списка задач ----------------
        public async Task<ArmJobResponse?> GetJobsAsync(string userId)
        {
            try
            {
                return await WithConnectionAsync(async conn =>
                {
                    var sql = "SELECT * FROM MARK_ARM_JOB_GET";

                    var records = await conn.QueryAsync(sql);

                    var armJobRecords = records.Select(r => new ArmJobRecord
                    {
                        DOCID = r.DOCID,
                        RESOURCEID = r.RESOURCEID,
                        SERIESID = r.SERIESID,
                        RES_BOXID = r.RES_BOXID,
                        DOC_ORDER = r.DOC_ORDER,
                        DOCDATE = r.DOCDATE?.ToString("dd.MM.yyyy"),
                        MOVEDATE = r.MOVEDATE?.ToString("dd.MM.yyyy"),
                        BUHDATE = r.BUHDATE?.ToString("dd.MM.yyyy"),
                        FIRMID = r.FIRMID,
                        DOC_NUM = r.DOC_NUM,
                        DEPART_NAME = r.DEPART_NAME,
                        RESOURCE_NAME = r.RESOURCE_NAME,
                        RESOURCE_ARTICLE = r.RESOURCE_ARTICLE,
                        SERIES_NAME = r.SERIES_NAME,
                        RES_BOX_NAME = r.RES_BOX_NAME,
                        GTIN = r.GTIN,
                        EXPIRE_DATE_VAL = r.EXPIRE_DATE_VAL?.ToString("dd.MM.yyyy"),
                        MNF_DATE_VAL = r.MNF_DATE_VAL?.ToString("dd.MM.yyyy"),
                        DOC_TYPE = r.DOC_TYPE,
                        AGREGATION_CODE = r.AGREGATION_CODE,
                        AGREGATION_TYPE = r.AGREGATION_TYPE,
                        CRYPTO_CODE_FLAG = r.CRYPTO_CODE_FLAG,
                        ERROR_FLAG = r.ERROR_FLAG,
                        FIRM_NAME = r.FIRM_NAME,
                        QTY = r.QTY,
                        AGGR_FLAG = r.AGGR_FLAG,
                        UN_TEMPLATEID = r.UN_TEMPLATEID,
                        UN_RESERVE_DOCID = r.UN_RESERVE_DOCID
                    }).ToList();

                    return new ArmJobResponse { RECORDSET = armJobRecords };
                });
            }
            catch
            {
                throw;
            }
        }
        // ---------------- Загрузка задания в бд ----------------
        public async Task<bool> LoadJobAsync(long jobId)
        {
            try
            {
                return await WithConnectionAsync(async conn =>
                {
                    using var transaction = await conn.BeginTransactionAsync();
                    try
                    {
                        var sql = "EXECUTE PROCEDURE ARM_JOB_LOAD(@JOBID)";

                        var result = await conn.QueryFirstOrDefaultAsync<int>(sql, new { JOBID = jobId }, transaction);

                        await transaction.CommitAsync();
                        return result == 1;
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch
            {
                throw;
            }
        }

        // ---------------- Загрузка задания ----------------
        public async Task<ArmJobInfoRecord?> GetJobDetailsAsync(long docId)
        {
            try
            {
                // Проверяем, есть ли уже загруженное задание
                var currentJobId = await GetCurrentJobIdAsync();

                //если выбранное задание и задание которое загруженно не равны то закрываем предыдущее задание
                if (currentJobId != docId)
                {
                    await CloseJobAsync();
                }
                // Если текущее задание не совпадает с запрашиваемым, загружаем новое
                if (currentJobId != docId)
                {
                    await LoadJobAsync(docId);
                }
                //else
                //{
                //    await StartSessionAsync();
                //}

                return await WithConnectionAsync(async conn =>
                {
                    // Теперь получаем данные из таблицы ARM_TASK, куда они загружены после ARM_JOB_LOAD
                    var sql = @"SELECT * FROM ARM_TASK WHERE DOCID = @DOCID";

                    var record = await conn.QueryFirstOrDefaultAsync(sql, new { DOCID = docId });

                    if (record != null)
                    {
                        return new ArmJobInfoRecord
                        {
                            // Nullable long/int поля
                            DOCID = record.DOCID as long?,
                            RESOURCEID = record.RESOURCEID as long?,
                            SERIESID = record.SERIESID as long?,
                            RES_BOXID = record.RES_BOXID as long?,
                            DOC_ORDER = record.DOC_ORDER as int?,
                            FIRMID = record.FIRMID as long?,

                            // String поля с проверкой на null
                            DOCDATE = record.DOCDATE?.ToString("dd.MM.yyyy"),
                            MOVEDATE = record.MOVEDATE?.ToString("dd.MM.yyyy"),
                            BUHDATE = record.BUHDATE?.ToString("dd.MM.yyyy"),
                            DOC_NUM = record.DOC_NUM?.ToString(),
                            DEPART_NAME = record.DEPART_NAME?.ToString(),
                            RESOURCE_NAME = record.RESOURCE_NAME?.ToString(),
                            RESOURCE_ARTICLE = record.RESOURCE_ARTICLE?.ToString(),
                            SERIES_NAME = record.SERIES_NAME?.ToString(),
                            RES_BOX_NAME = record.RES_BOX_NAME?.ToString(),
                            GTIN = record.GTIN?.ToString(),
                            EXPIRE_DATE_VAL = record.EXPIRE_DATE_VAL?.ToString("dd.MM.yyyy"),
                            MNF_DATE_VAL = record.MNF_DATE_VAL?.ToString("dd.MM.yyyy"),

                            // Специальные типы согласно модели
                            DOC_TYPE = record.DOC_TYPE?.ToString(), // string? в модели
                            AGREGATION_CODE = record.AGREGATION_CODE as int?, // int? в модели
                            AGREGATION_TYPE = record.AGREGATION_TYPE?.ToString(), // string? в модели
                            CRYPTO_CODE_FLAG = record.CRYPTO_CODE_FLAG as short?,
                            FIRM_NAME = record.FIRM_NAME?.ToString(),
                            QTY = record.QTY as int?,
                            AGGR_FLAG = record.AGGR_FLAG as short?,

                            // Nullable long поля
                            UN_TEMPLATEID = record.UN_TEMPLATEID as long?,
                            UN_RESERVE_DOCID = record.UN_RESERVE_DOCID as long?,

                            // byte[] поля с правильной обработкой
                            UN_TEMPLATE = ConvertToByteArray(record.UN_TEMPLATE),
                            UN_TEMPLATE_FR = ConvertToByteArray(record.UN_TEMPLATE_FR),

                            // Дополнительные поля из ARM_TASK
                            IN_BOX_QTY = record.IN_BOX_QTY as int?,
                            IN_INNER_BOX_QTY = record.IN_INNER_BOX_QTY as int?,
                            INNER_BOX_FLAG = record.INNER_BOX_FLAG as short?,
                            INNER_BOX_AGGR_FLAG = record.INNER_BOX_AGGR_FLAG as short?,
                            INNER_BOX_QTY = record.INNER_BOX_QTY as int?,
                            IN_PALLET_BOX_QTY = record.IN_PALLET_BOX_QTY as int?,
                            LAST_PACKAGE_LOCATION_INFO = record.LAST_PACKAGE_LOCATION_INFO?.ToString(),
                            PALLET_NOT_USE_FLAG = record.PALLET_NOT_USE_FLAG as short?,
                            PALLET_AGGR_FLAG = record.PALLET_AGGR_FLAG as short?,
                            AGREGATION_TYPEID = record.AGREGATION_TYPEID as long?,
                            SERIES_SYS_NUM = record.SERIES_SYS_NUM as int?, // int? в модели
                            LAYERS_QTY = record.LAYERS_QTY as int?,
                            LAYER_ROW_QTY = record.LAYER_ROW_QTY as int?,
                            LAYER_ROWS_QTY = record.LAYER_ROWS_QTY as int?,
                            PACK_HEIGHT = record.PACK_HEIGHT as int?,
                            PACK_WIDTH = record.PACK_WIDTH as int?,
                            PACK_LENGTH = record.PACK_LENGTH as int?,
                            PACK_WEIGHT = record.PACK_WEIGHT as int?,
                            PACK_CODE_POSITION = record.PACK_CODE_POSITION?.ToString(), // string? в модели
                            BOX_TEMPLATEID = record.BOX_TEMPLATEID as long?,
                            BOX_RESERVE_DOCID = record.BOX_RESERVE_DOCID as long?,
                            BOX_TEMPLATE = ConvertToByteArray(record.BOX_TEMPLATE),
                            PALLETE_TEMPLATEID = record.PALLETE_TEMPLATEID as long?,
                            PALLETE_RESERVE_DOCID = record.PALLETE_RESERVE_DOCID as long?,
                            PALLETE_TEMPLATE = ConvertToByteArray(record.PALLETE_TEMPLATE),
                            INT_BOX_TEMPLATEID = record.INT_BOX_TEMPLATEID as long?,
                            INT_BOX_RESERVE_DOCID = record.INT_BOX_RESERVE_DOCID as long?,
                            INT_BOX_TEMPLATE = ConvertToByteArray(record.INT_BOX_TEMPLATE),
                            LOAD_START_TIME = record.LOAD_START_TIME?.ToString("dd.MM.yyyy HH:mm:ss"),
                            LOAD_FINISH_TIME = record.LOAD_FINISH_TIME?.ToString("dd.MM.yyyy HH:mm:ss"),
                            EXPIRE_DATE = record.EXPIRE_DATE?.ToString(),
                            MNF_DATE = record.MNF_DATE?.ToString()
                        };
                    }

                    return null;
                });
            }
            catch
            {
                throw;
            }
        }
        // ---------------- Проверка есть ли текущее загруженное задание ----------------
        public async Task<long?> GetCurrentJobIdAsync()
        {
            try
            {
                return await WithConnectionAsync(async conn =>
                {
                    var sql = "SELECT * FROM JOBID_GET";

                    var result = await conn.QueryFirstOrDefaultAsync(sql);

                    return result?.JOBID as long?;
                });
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка получения текущего задания: {ex.Message}", NotificationType.Error);
                throw;
            }
        }

        // ---------------- Закрытие задания ----------------
        public async Task<bool> CloseJobAsync()
        {
            try
            {
                return await WithConnectionAsync(async conn =>
                {
                    using var transaction = await conn.BeginTransactionAsync();
                    try
                    {
                        var sql = "EXECUTE PROCEDURE ARM_JOB_CLOSE";

                        await conn.ExecuteAsync(sql, transaction: transaction);

                        await transaction.CommitAsync();
                        return true;
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch
            {
                throw;
            }
        }

        // ---------------- Получение Sgtin ----------------
        public async Task<ArmJobSgtinResponse?> GetSgtinAsync(long docId)
        {
            try
            {
                return await WithConnectionAsync(async conn =>
                {
                    var sql = @"SELECT * FROM MARK_UN_CODE";

                    var records = await conn.QueryAsync(sql);

                    var sgtinRecords = records.Select(r => new ArmJobSgtinRecord
                    {
                        UNID = r.UNID,
                        STATEID = r.STATEID,
                        UN_CODE = r.UN_CODE?.ToString(),
                        GS1FIELD91 = r.GS1FIELD91?.ToString(),
                        GS1FIELD92 = r.GS1FIELD92?.ToString(),
                        GS1FIELD93 = r.GS1FIELD93?.ToString(),
                        PARENT_SSCCID = r.PARENT_SSCCID,
                        UN_TYPE = r.UN_TYPE,
                        PARENT_UNID = r.PARENT_UNID,
                        QTY = r.QTY
                    }).ToList();

                    return new ArmJobSgtinResponse { RECORDSET = sgtinRecords };
                });
            }
            catch
            {
                throw;
            }
        }

        // ---------------- Получение Sscc ----------------
        public async Task<ArmJobSsccResponse?> GetSsccAsync(long docId)
        {
            try
            {
                return await WithConnectionAsync(async conn =>
                {
                    var sql = @"SELECT * FROM MARK_SSCC_CODE";

                    var records = await conn.QueryAsync(sql);

                    var ssccRecords = records.Select(r => new ArmJobSsccRecord
                    {
                        SSCCID = r.SSCCID,
                        SSCC_CODE = r.SSCC_CODE?.ToString(),
                        STATEID = r.STATEID,
                        TYPEID = r.TYPEID,
                        DISPLAY_BAR_CODE = r.DISPLAY_BAR_CODE?.ToString(),
                        SSCC = r.SSCC?.ToString(),
                        CHECK_BAR_CODE = r.CHECK_BAR_CODE,
                        ORDER_NUM = r.ORDER_NUM,
                        CODE_STATE = r.CODE_STATE?.ToString(),
                        QTY = r.QTY,
                        PARENT_SSCCID = r.PARENT_SSCCID
                    }).ToList();

                    return new ArmJobSsccResponse { RECORDSET = ssccRecords };
                });
            }
            catch
            {
                throw;
            }
        }

        // ---------------- SESSION MANAGEMENT ----------------
        public async Task<bool> StartSessionAsync()
        {
            try
            {
                return await WithConnectionAsync(async conn =>
                {
                    var sql = @"EXECUTE PROCEDURE MARK_ARM_SESSION_START";

                    await conn.ExecuteAsync(sql);

                    return true;
                });
            }
            catch
            {
                
                    throw;
            }
        }

        public async Task<bool> CloseSessionAsync()
        {
            try
            {
                return await WithConnectionAsync(async conn =>
                {
                    var sql = @"EXECUTE PROCEDURE MARK_ARM_SESSION_CLOSE";

                    await conn.ExecuteAsync(sql);

                    return true;
                });
            }
            catch
            {
                throw;
            }
        }

        //  ---------------- Логирование агрегации ----------------
        public async Task<bool> LogAggregationAsync(string UNID, string SSCCID)
        {
            try
            {
                return await WithConnectionAsync(async conn =>
                {
                    using var transaction = await conn.BeginTransactionAsync();
                    try
                    {
                        var sql = "EXECUTE PROCEDURE ARM_SGTIN_SSCC_ADD(@UNID, @SSCCID)";

                        await conn.ExecuteAsync(sql, new
                        {
                            UNID = UNID,
                            SSCCID = SSCCID
                        }, transaction);

                        await transaction.CommitAsync();
                        return true;
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch
            {
                throw;
            }
        }

        // Вспомогательный метод для конвертации в byte[]
        private byte[]? ConvertToByteArray(object? value)
        {
            if (value == null)
                return null;

            if (value is byte[] byteArray)
                return byteArray;

            if (value is string stringValue)
                return System.Text.Encoding.UTF8.GetBytes(stringValue);

            // Если это другой тип, конвертируем через ToString
            return System.Text.Encoding.UTF8.GetBytes(value.ToString() ?? string.Empty);
        }

        // ---------------- Получение счетчиков ARM ----------------
        public async Task<ArmCountersResponse?> GetArmCountersAsync()
        {
            try
            {
                return await WithConnectionAsync(async conn =>
                {
                    var sql = @"SELECT * FROM ARM_COUNTERS_SHOW";

                    var records = await conn.QueryAsync(sql);

                    var armCountersRecords = records.Select(r => new ArmCountersRecord
                    {
                        STATEID = r.STATEID,
                        CODE = r.CODE?.ToString(),
                        NAME = r.NAME?.ToString(),
                        QTY = r.QTY,
                        JOB_QTY = r.JOB_QTY
                    }).ToList();

                    return new ArmCountersResponse { RECORDSET = armCountersRecords };
                });
            }
            catch
            {
                throw;
            }
        }
    }
}