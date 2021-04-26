using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib;
using MySql.Data.MySqlClient;
using CitizenFX.Core;
using GGSQL.Models;
using GGSQL.Models.Styles;
using Dapper.Contrib.Extensions;
using System.Data;
using System.Threading;

namespace GGSQL
{
    public class MySqlDatabase
    {
        private static string m_connectionString;
        private static ServerLogger m_logger;

        private readonly CustomTaskScheduler _scheduler;

        private class DbConnection : IDisposable
        {
            public readonly MySqlConnection Connection;

            public DbConnection(string connectionString)
            {
                Connection = new MySqlConnection(connectionString);
            }

            public void Dispose()
            {
                Connection.Close();
            }
        }

        internal MySqlDatabase(string connectionString, bool debug = false, ServerLogger logger = null)
        {

            m_connectionString = connectionString;
            _scheduler = new CustomTaskScheduler();
            m_logger = logger;

            SqlMapper.AddTypeHandler(typeof(List<PedComponent>), new JsonTypeHandler());
            SqlMapper.AddTypeHandler(typeof(List<ClothingStyle>), new JsonTypeHandler());
        }

        public async Task DummyQuery()
        {
            var sql = "SELECT VERSION();";

            using (var db = new DbConnection(m_connectionString))
            {
                await db.Connection.QuerySingleAsync(sql);

            }
        }

        public async Task<User> GetUser(Player player)
        {
            var sql = @"SELECT
	                        Id AS `Id`,
                            LicenseId AS `LicenseId`,
                            SteamId AS `SteamId`,
                            XblId AS `XblId`,
                            LiveId AS `LiveId`,
                            DiscordId AS `DiscordId`,
                            FivemId AS `FivemId`,
                            Endpoint AS `Endpoint`,
                            Kills AS `Kills`,
                            Deaths AS `Deaths`,
                            Xp AS `Xp`,
                            Money AS `Money`,
                            LastConnected AS `LastConnected`,
                            ActiveUserOutfit AS `ActiveUserOutfit`,
                            ActiveUserWeaponTint AS `ActiveUserWeaponTint`,
                            Donator AS `Donator`,
                            Moderator AS `Moderator`
                        FROM
	                        users
                        WHERE
                            licenseId = @lid
                        OR
	                        (steamId IS NOT NULL AND steamId = @sid)
                        OR 
                            (xblId IS NOT NULL AND xblId = @xid)
                        OR 
                            (liveId IS NOT NULL AND liveId = @liveid)
                        OR 
                            (discordId IS NOT NULL AND discordId = @did)
                        OR 
                            (fivemId IS NOT NULL AND fivemId = @fid);";

            var licenseId = player.Identifiers["license"];
            var steamId = player.Identifiers["steam"];
            var xblId = player.Identifiers["xbl"];
            var liveId = player.Identifiers["live"];
            var discordId = player.Identifiers["discord"];
            var fivemId = player.Identifiers["fivem"];

            using (var db = new DbConnection(m_connectionString))
            {
                var dbUser = await db.Connection.QueryFirstOrDefaultAsync<User>(sql, new { lid = licenseId, sid = steamId, xid = xblId, liveid = liveId, did = discordId, fid = fivemId });

                if (dbUser == null) return null;

                dbUser.NetId = Convert.ToInt32(player.Handle);
                dbUser.Endpoint = player.EndPoint;

                return dbUser;
            }
        }

        public async Task<Ban> IsUserBanned(string licenseId, string steamId, string xblId, string liveId, string discordId, string fivemId)
        {
            var sql = @"SELECT
                            endDate AS `EndDate`,
                            reason AS `Reason`
                        FROM 
                            bans 
                        WHERE 
                            end_date>=@t 
                        AND 
                            (licenseId=@lid OR steamId=@sid OR xblId=@xid OR liveId=@liveid OR discordId=@did OR fivemId=@fid);";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.QueryFirstOrDefaultAsync<Ban>(sql, new { t = DateTime.UtcNow, lid = licenseId, sid = steamId, xid = xblId, liveid = liveId, did = discordId, fid = fivemId });

                return dbResult;
            }
        }

        public async Task<User> CreateNewUser(Player player)
        {
            var sql = @"INSERT INTO users
                            (licenseId, steamId, xblId, liveId, discordId, endpoint)
                        VALUES
                            (@lid, @sid,  @xblid, @liveid, @did, @endpoint);
                        SELECT LAST_INSERT_ID();";

            var user = new User();
            user.NetId = Convert.ToInt32(player.Handle);
            user.LicenseId = player.Identifiers["license"];
            user.SteamId = player.Identifiers["steam"];
            user.XblId = player.Identifiers["xbl"];
            user.LiveId = player.Identifiers["live"];
            user.DiscordId = player.Identifiers["discord"];
            user.FivemId = player.Identifiers["fivem"];
            user.Endpoint = player.EndPoint;

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.ExecuteScalarAsync<int>(sql, new { lid = user.LicenseId, sid = user.SteamId, xblid = user.XblId, liveid = user.LiveId, did = user.DiscordId, endpoint = user.Endpoint });
                user.Id = dbResult;

                return user;
            }
        }

        public async Task<bool> SaveUser(User user)
        {
            var sql = @"UPDATE
                            users
                        SET
                            kills=@kills,
                            deaths=@deaths,
                            xp=@xp,
                            money=@money,
                            activeUserOutfit=@outfitId,
                            activeUserWeaponTint=@weaponTintId,
                            lastconnected=@now
                        WHERE 
                            id=@userId;";

            using (var db = new DbConnection(m_connectionString))
            {
                var affectedRows = await db.Connection.ExecuteAsync(sql, new { userId = user.Id, kills = user.Kills, deaths = user.Deaths, xp = user.Xp, money = user.Money, outfitId = user.ActiveUserOutfit, weaponTintId = user.ActiveUserWeaponTint, now = DateTime.UtcNow });
                if (affectedRows != 0)
                {
                    return true;
                }

                return false;
            }
        }

        public async Task<bool> SaveUsers(List<User> users)
        {
            string sql = @"";

            foreach (var user in users)
            {

                sql += $@"UPDATE
                            users
                        SET
                            kills={user.Kills},
                            deaths={user.Deaths},
                            xp={user.Xp},
                            money={user.Money},
                            activeUserOutfit={user.ActiveUserOutfit},
                            activeUserWeaponTint={user.ActiveUserWeaponTint}
                        WHERE 
                            id={user.Id};";
            }

            using (var db = new DbConnection(m_connectionString))
            {
                var affectedRows = await db.Connection.ExecuteAsync(sql);
                if (affectedRows != 0)
                {
                    return true;
                }

                return false;
            }
        }

        public async Task<int> GetActiveUserOutfit(int userId)
        {
            try
            {
                var sql = @"SELECT activeUserOutfit FROM users WHERE id=@userId";

                using (var db = new DbConnection(m_connectionString))
                {
                    var oid = await db.Connection.ExecuteScalarAsync<int>(sql, new { userId = userId });
                    return oid;
                }
            }
            catch (Exception ex)
            {
                m_logger.Exception("GetActiveUserWeaponTint", ex);
                return 0;
            }
        }

        public async Task<int> GetActiveUserWeaponTint(int userId)
        {
            try
            {
                var sql = @"SELECT activeUserWeaponTint FROM users WHERE id=@userId";

                using (var db = new DbConnection(m_connectionString))
                {
                    var oid = await db.Connection.ExecuteScalarAsync<int>(sql, new { userId = userId });
                    return oid;
                }
            }
            catch (Exception ex)
            {
                m_logger.Exception("GetActiveUserWeaponTint", ex);
                return 0;
            }
        }

        public async Task<Connection> InsertConnection(Connection connection)
        {
            var sql = @"INSERT INTO connections
                            (established, userId, endPoint)
                        VALUES
                            (@e, @userId, @ip);
                        SELECT LAST_INSERT_ID();";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.ExecuteScalarAsync<int>(sql, new { e = connection.Established, userId = connection.UserId, ip = connection.EndPoint });
                connection.Id = dbResult;

                return connection;
            }
        }

        public async Task<bool> UpdateConnection(Connection connection)
        {
            var sql = @"UPDATE
                            connections
                        SET
                            dropped = @dropped,
                            totalSeconds = @totalSeconds
                        WHERE
                            id = @id;";

            using (var db = new DbConnection(m_connectionString))
            {
                var affectedRows = await db.Connection.ExecuteAsync(sql, new { id = connection.Id, dropped = DateTime.UtcNow, totalSeconds = (DateTime.UtcNow - connection.Established).TotalSeconds });

                if (affectedRows != 0)
                {
                    return true;
                }

                return false;
            }
        }

        public async Task<List<Ban>> GetAllActiveBans()
        {
            var sql = @"SELECT
                            Id AS `Id`,
                            LicenseId AS `LicenseId`,
                            SteamId AS `SteamId`,
                            XblId AS `XblId`,
                            LiveId AS `LiveId`,
                            DiscordId AS `DiscordId`,
                            FivemId AS `FivemId`,
                            EndDate AS `EndDate`,
                            Reason AS `Reason`
                        FROM
                            bans
                        WHERE
                            endDate >= @now;";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.QueryAsync<Ban>(sql, new { now = DateTime.UtcNow });

                if (dbResult.Count() > 0)
                {
                    return dbResult.ToList();
                }

                return new List<Ban>();
            }
        }

        public async Task<GameRound> InsertGameRound(GameRound gameRound)
        {
            using (var db = new DbConnection(m_connectionString))
            {
                gameRound.Id = await db.Connection.InsertAsync(gameRound);
                return gameRound;
            }
        }

        public async Task<List<Outfit>> GetOutfits()
        {
            var sql = @"SELECT id, name, tebexPackageId, donatorExclusive, price, requiredXp, discount, enabled, description, image, components, createdAt, updatedAt
                            FROM outfits WHERE enabled = true";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.QueryAsync<Outfit>(sql);

                return dbResult.ToList();
            }
        }

        public async Task<List<GeneralItem>> GetGeneralItems()
        {
            var sql = @"SELECT id, name, type, tebexPackageId, price, requiredXp, discount, enabled, extraData, createdAt, updatedAt
                            FROM generalitems WHERE enabled = true";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.QueryAsync<GeneralItem>(sql);

                return dbResult.ToList();
            }
        }

        public async Task<List<WeaponTint>> GetWeaponTints()
        {
            var sql = @"SELECT id, name, tebexPackageId, donatorExclusive, price, requiredXp, discount, enabled, isMk2, tintId, image, description, createdAt, updatedAt
                            FROM weapontints WHERE enabled = true";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.QueryAsync<WeaponTint>(sql);

                return dbResult.ToList();
            }
        }

        public async Task<Outfit> InsertOutfit(Outfit outfit)
        {
            using (var db = new DbConnection(m_connectionString))
            {
                outfit.Id = await db.Connection.InsertAsync(outfit);
                return outfit;
            }
        }

        public async Task<WeaponTint> InsertWeaponTint(WeaponTint weaponTint)
        {
            using (var db = new DbConnection(m_connectionString))
            {
                weaponTint.Id = await db.Connection.InsertAsync(weaponTint);
                return weaponTint;
            }
        }

        public async Task<List<UserOutfit>> GetUserOutfits(int userId)
        {
            var sql = @"SELECT uo.id, uo.userId, uo.outfitId, uo.createdAt FROM useroutfits uo
                            INNER JOIN outfits o ON uo.outfitId = o.id WHERE uo.userId = @userId";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.QueryAsync<UserOutfit>(sql, new { userId = userId });

                if (dbResult.Count() > 0)
                {
                    return dbResult.ToList();
                }

                return new List<UserOutfit>();
            }
        }

        public async Task<List<UserGeneralItem>> GetUserGeneralItems(int userId)
        {
            var sql = @"SELECT uo.id, uo.userId, uo.itemId, uo.createdAt FROM usergeneralitems uo
                            INNER JOIN GeneralItems o ON uo.itemId = o.id WHERE uo.userId = @userId";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.QueryAsync<UserGeneralItem>(sql, new { userId = userId });

                if (dbResult.Count() > 0)
                {
                    return dbResult.ToList();
                }

                return new List<UserGeneralItem>();
            }
        }

        public async Task<List<UserWeaponTint>> GetUserWeaponTints(int userId)
        {
            var sql = @"SELECT uo.id, uo.userId, uo.weaponTintId, uo.createdAt FROM userweapontints uo
                            INNER JOIN weapontints o ON uo.weaponTintId = o.id WHERE uo.userId = @userId";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.QueryAsync<UserWeaponTint>(sql, new { userId = userId });

                if (dbResult.Count() > 0)
                {
                    return dbResult.ToList();
                }
                return new List<UserWeaponTint>();
            }
        }

        public async Task<UserOutfit> InsertUserOutfit(UserOutfit userOutfit)
        {
            using (var db = new DbConnection(m_connectionString))
            {
                userOutfit.Id = await db.Connection.InsertAsync(userOutfit);
                return userOutfit;
            }
        }

        public async Task<UserGeneralItem> InsertUserGeneralItem(UserGeneralItem userGeneralItem)
        {
            using (var db = new DbConnection(m_connectionString))
            {
                userGeneralItem.Id = await db.Connection.InsertAsync(userGeneralItem);
                return userGeneralItem;
            }
        }

        public async Task<UserWeaponTint> InsertUserWeaponTint(UserWeaponTint userWeaponTint)
        {
            using (var db = new DbConnection(m_connectionString))
            {
                userWeaponTint.Id = await db.Connection.InsertAsync(userWeaponTint);
                return userWeaponTint;
            }
        }

        public async Task<int> DeleteUserOutfit(int outfitId, int userId)
        {
            var sql = @"DELETE FROM useroutfits WHERE userId=@uid AND outfitId=@oid";

            using (var db = new DbConnection(m_connectionString))
            {
                var affectedRows = await db.Connection.ExecuteAsync(sql, new
                {
                    uid = userId,
                    oid = outfitId
                });

                return affectedRows;
            }
        }

        public async Task<int> DeleteUserGeneralItem(int itemId, int userId)
        {
            var sql = @"DELETE FROM usergeneralitems WHERE userId=@uid AND itemId=@oid";

            using (var db = new DbConnection(m_connectionString))
            {
                var affectedRows = await db.Connection.ExecuteAsync(sql, new
                {
                    uid = userId,
                    oid = itemId
                });

                return affectedRows;
            }
        }

        public async Task UpdateUserOutfits(List<UserOutfit> userOutfits)
        {
            using (var db = new DbConnection(m_connectionString))
            {
                await db.Connection.UpdateAsync(userOutfits);
            }
        }

        public async Task<int> BuyUserOutfit(int userId, int outfitId)
        {
            using (var db = new DbConnection(m_connectionString))
            {
                DynamicParameters _params = new DynamicParameters();
                _params.Add("@pSuccess", DbType.Int32, direction: ParameterDirection.Output);
                _params.Add("@pUserId", userId);
                _params.Add("@pOutfitId", outfitId);
                await db.Connection.ExecuteAsync("UserBuyOutfit", _params, commandType: CommandType.StoredProcedure);
                return _params.Get<int>("pSuccess");
            }
        }

        public async Task<int> BuyUserBoost(int userId, int boostId)
        {
            using (var db = new DbConnection(m_connectionString))
            {
                DynamicParameters _params = new DynamicParameters();
                _params.Add("@pSuccess", DbType.Int32, direction: ParameterDirection.Output);
                _params.Add("@pUserId", userId);
                _params.Add("@pItemId", boostId);
                _params.Add("@pItemTypeId", 1);

                await db.Connection.ExecuteAsync("UserBuyBoost", new { pUserId = userId, pItemId = boostId, pItemTypeId = 1 }, commandType: CommandType.StoredProcedure);
                return _params.Get<int>("pSuccess");
            }
        }

        public async Task<int> BuyUserWeaponTint(int userId, int weaponTintId)
        {
            using (var db = new DbConnection(m_connectionString))
            {
                DynamicParameters _params = new DynamicParameters();
                _params.Add("@pSuccess", DbType.Int32, direction: ParameterDirection.Output);
                _params.Add("@pUserId", userId);
                _params.Add("@pWeaponTintId", weaponTintId);
                await db.Connection.ExecuteAsync("UserBuyWeaponTint", _params, commandType: CommandType.StoredProcedure);
                return _params.Get<int>("pSuccess");
            }
        }

        public Task<List<dynamic>> QueryResult(string query, IDictionary<string, dynamic> parameters = null)
        {
            return Task.Factory.StartNew(() =>
            {
                using(var db = new DbConnection(m_connectionString))
                {
                    DynamicParameters _params = new DynamicParameters();
                    _params.AddDynamicParams(parameters);
                    var results = db.Connection.Query(query, _params);
                    return results.ToList();
                }
            }, CancellationToken.None, TaskCreationOptions.None, _scheduler);
        }

        public Task<int> Query(string query, IDictionary<string, dynamic> parameters = null, bool isInsert = false)
        {
            return Task.Factory.StartNew(() =>
            {
                using(var db = new DbConnection(m_connectionString))
                {
                    DynamicParameters _params = new DynamicParameters();
                    _params.AddDynamicParams(parameters);
                    return db.Connection.Execute(query, parameters);
                }
            }, CancellationToken.None, TaskCreationOptions.None, _scheduler);
        }

        public IDictionary<string, dynamic> TryParseParameters(dynamic parameters, bool debug = true)
        {
            IDictionary<string, dynamic> parsedParameters = null;
            try
            {
                parsedParameters = parameters;
            }
            catch
            {
                // Only Warn that the user supplied bad parameters when debug is set to true
                if (debug)
                    m_logger.Warning("Parameters are not in Dictionary-shape");
                parsedParameters = null;
            }

            return parsedParameters;
        }
    }
}
