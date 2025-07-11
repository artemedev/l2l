using Dapper;
using l2l_aggregator.Models;
using l2l_aggregator.Services.Database.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.Database.Repositories
{
    // ========================== Аутентификация пользователей ==========================
    public class UserAuthRepository : BaseRepository, IUserAuthRepository
    {
        public UserAuthRepository(DatabaseInitializer dbService) : base(dbService) { }
        private object MapUserAuthParams(UserAuthResponse response) => new
        {
            response.USERID,
            response.USER_NAME,
            response.PERSONID,
            response.PERSON_NAME,
            response.PERSON_DELETE_FLAG,
            response.AUTH_OK,
            response.ERROR_TEXT,
            response.NEED_CHANGE_FLAG,
            EXPIRATION_DATE = string.IsNullOrWhiteSpace(response.EXPIRATION_DATE)
                            ? (DateTime?)null
                            : DateTime.ParseExact(response.EXPIRATION_DATE, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture),
            response.REC_TYPE
        };

        public async Task SaveUserAuthAsync(UserAuthResponse response)
        {
            await WithConnectionAsync(async conn =>
            {
                var exists = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM USER_AUTH_INFO WHERE USERID = @USERID",
                    new { response.USERID });

                var parameters = MapUserAuthParams(response);

                if (exists > 0)
                {
                    await conn.ExecuteAsync(
                        @"UPDATE USER_AUTH_INFO SET 
                          USER_NAME = @USER_NAME, 
                          PERSONID = @PERSONID, 
                          PERSON_NAME = @PERSON_NAME, 
                          PERSON_DELETE_FLAG = @PERSON_DELETE_FLAG, 
                          AUTH_OK = @AUTH_OK, 
                          ERROR_TEXT = @ERROR_TEXT, 
                          NEED_CHANGE_FLAG = @NEED_CHANGE_FLAG, 
                          EXPIRATION_DATE = @EXPIRATION_DATE, 
                          REC_TYPE = @REC_TYPE
                          WHERE USERID = @USERID",
                        parameters);
                }
                else
                {
                    await conn.ExecuteAsync(
                        @"INSERT INTO USER_AUTH_INFO 
                          (USERID, USER_NAME, PERSONID, PERSON_NAME, PERSON_DELETE_FLAG, 
                           AUTH_OK, ERROR_TEXT, NEED_CHANGE_FLAG, EXPIRATION_DATE, REC_TYPE)
                          VALUES (@USERID, @USER_NAME, @PERSONID, @PERSON_NAME, @PERSON_DELETE_FLAG, 
                                  @AUTH_OK, @ERROR_TEXT, @NEED_CHANGE_FLAG, @EXPIRATION_DATE, @REC_TYPE)",
                        parameters);
                }
            });
        }

        public Task<List<UserAuthResponse>> GetUserAuthAsync() =>
            WithConnectionAsync(async conn =>
            {
                var result = await conn.QueryAsync<UserAuthResponse>(
                    @"SELECT USERID, USER_NAME, PERSONID, PERSON_NAME, PERSON_DELETE_FLAG,
                             AUTH_OK, ERROR_TEXT, NEED_CHANGE_FLAG, EXPIRATION_DATE, REC_TYPE 
                      FROM USER_AUTH_INFO");
                return result.ToList();
            });

        public Task<string?> GetLastUserIdAsync() =>
            WithConnectionAsync(conn =>
                conn.QueryFirstOrDefaultAsync<string>(
                    @"SELECT USERID FROM USER_AUTH_INFO ORDER BY EXPIRATION_DATE DESC"));
        // ========================== Админ-проверка ==========================
        public async Task<bool> ValidateAdminUserAsync(string username, string password)
        {
            return await WithConnectionAsync(async conn =>
            {
                var storedPassword = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT PASSWORD FROM ADMIN_USERS WHERE USERNAME = @username",
                    new { username });

                return storedPassword == password;
            });
        }
    }
}
