using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using GGSQL.Models;
using GGSQL.Models.Styles;

namespace GGSQL
{
    public class MySqlDatabase
    {
        // private static ConcurrentQueue<Action> m_callbackQueue;

        private static string m_connectionString;
        private static bool _debug;
        private static bool m_initialized;
        private static ServerLogger m_logger;

        //private readonly CustomTaskScheduler _scheduler;

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

            // m_callbackQueue = new ConcurrentQueue<Action>();

            // Tick += OnTick;

            m_connectionString = connectionString;
            _debug = debug;
            //_scheduler = new CustomTaskScheduler();
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
                            ClothingStyles AS `ClothingStyles`,
                            Donator AS `Donator`,
                            Moderator AS `Moderator`,
                            UserOutfits AS `UserOutfits`
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

        public async Task<bool> UpdateClothingStyles(int userId, List<ClothingStyle> clothingStyles)
        {
            var sql = @"UPDATE
                            users
                        SET 
                            clothingStyles = @cs
                        WHERE 
                            id = @uId;";

            using (var db = new DbConnection(m_connectionString))
            {
                var affectedRows = await db.Connection.ExecuteAsync(sql, new { cs = clothingStyles, uId = userId });

                return affectedRows != 0 ? true : false;
            }
        }

        public async Task<User> CreateNewUser(Player player)
        {
            var sql = @"INSERT INTO users
                            (licenseId, steamId, xblId, liveId, discordId, endpoint, clothingStyles)
                        VALUES
                            (@lid, @sid,  @xblid, @liveid, @did, @endpoint, @cstyles);
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
                var dbResult = await db.Connection.ExecuteScalarAsync<int>(sql, new { lid = user.LicenseId, sid = user.SteamId, xblid = user.XblId, liveid = user.LiveId, did = user.DiscordId, endpoint = user.Endpoint, cstyles = user.ClothingStyles });
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
                            lastconnected=@now,
                            clothingStyles=@cstyles
                        WHERE 
                            id=@userId;";

            using (var db = new DbConnection(m_connectionString))
            {
                var clothingStyles = JsonConvert.SerializeObject(user.ClothingStyles);
                var affectedRows = await db.Connection.ExecuteAsync(sql, new { userId = user.Id, kills = user.Kills, deaths = user.Deaths, xp = user.Xp, money = user.Money, now = DateTime.UtcNow, cstyles = clothingStyles });
                if(affectedRows != 0)
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
                var clothingStyles = JsonConvert.SerializeObject(user.ClothingStyles);

                sql += $@"UPDATE
                            users
                        SET
                            kills={user.Kills},
                            deaths={user.Deaths},
                            xp={user.Xp},
                            money={user.Money},
                            clothingStyles='{clothingStyles}'
                        WHERE 
                            id={user.Id};";
            }

            using(var db = new DbConnection(m_connectionString))
            {
                var affectedRows = await db.Connection.ExecuteAsync(sql);
                if(affectedRows != 0)
                {
                    return true;
                }

                return false;
            }
        }

        public async Task<Connection> InsertConnection(Connection connection)
        {
            var sql = @"INSERT INTO connections
                            (established, userId, endPoint)
                        VALUES
                            (@e, @userId, @ip);
                        SELECT LAST_INSERT_ID();";

            using(var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.ExecuteScalarAsync<int>(sql, new { e = connection.Established, userId = connection.UserId, ip = connection.EndPoint});
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
                var affectedRows = await db.Connection.ExecuteAsync(sql, new { id = connection.Id, dropped = DateTime.UtcNow, totalSeconds = (DateTime.UtcNow - connection.Established).TotalSeconds});

                if(affectedRows != 0)
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

                if(dbResult.Count() > 0)
                {
                    return dbResult.ToList();
                }

                return new List<Ban>();
            }
        }

        public async Task<GameRound> InsertGameRound(GameRound gameRound)
        {
            var sql = @"INSERT INTO gamerounds
                            (startTime, endTime, mapName, 
                                winnerUserId, winnerRoundKills, winnerRoundDeaths)
                        VALUES
                            (@st, @et,@mn, @wuid, @wrk,@wrd);
                        SELECT LAST_INSERT_ID();";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.ExecuteScalarAsync<int>(sql, new 
                {   st = gameRound.StartTime, et = gameRound.EndTime, 
                    mn = gameRound.MapName,  wuid = gameRound.WinnerUserId,
                    wrk = gameRound.WinnerRoundKills, wrd = gameRound.WinnerRoundDeaths  
                });

                gameRound.Id = dbResult;
                return gameRound;
            }
        }

        public async Task<List<Outfit>> GetOutfits()
        {
            var sql = @"SELECT id, name, price, requiredXp, discount, enabled, components, createdAt, updatedAt
                            FROM outfits WHERE enabled = true";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.QueryAsync<Outfit>(sql);

                return dbResult.ToList();
            }
        }

        public async Task<Outfit> InsertOutfit(Outfit outfit)
        {
            var sql = @"INSERT INTO outfits (name, price, requiredXp, discount, enabled, components, createdAt, updatedAt)
                            VALUES (@name, @price, @xp, @d, @e, @comp, @ca, @ua);
                            SELECT LAST_INSERT_ID();";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.ExecuteScalarAsync<int>(sql, new
                {
                    name = outfit.Name,
                    price = outfit.Price,
                    xp = outfit.RequiredXp,
                    d = outfit.Discount,
                    e = outfit.Enabled,
                    comp = outfit.Components,
                    ca = outfit.CreatedAt,
                    ua = outfit.UpdatedAt
                });

                outfit.Id = dbResult;
                return outfit;
            }
        }

        public async Task<List<UserOutfit>> GetUserOutfits(int userId)
        {
            var sql = @"SELECT uo.id, uo.user_id, uo.outfit_id, uo.created_at FROM UserOutfits uo
                            INNER JOIN Outfits o ON uo.outfit_id = o.id WHERE ui.user_id = @userId";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.QueryAsync<UserOutfit>(sql, new { userId = userId });

                if(dbResult.Count() > 0)
                {
                    return dbResult.ToList();
                }

                return new List<UserOutfit>();
            }
        }

        public async Task<UserOutfit> InsertUserOutfit(UserOutfit userOutfit)
        {
            var sql = @"INSERT INTO useroutfits (userId, outfitId, createdAt)
                            VALUES (@uid, @oid, @ca); SELECT LAST_INSERT_ID();";

            using (var db = new DbConnection(m_connectionString))
            {
                var dbResult = await db.Connection.ExecuteScalarAsync<int>(sql, new
                {
                    uid = userOutfit.UserId,
                    oid = userOutfit.OutfitId,
                    ca = userOutfit.CreatedAt
                });

                userOutfit.Id = dbResult;
                return userOutfit;
            }
        }

        public async Task<int> ExecuteAsync(string query, IDictionary<string, object> parameters)
        {
            int numberOfUpdatedRows = 0;

            try
            {
                using (var db = new DbConnection(m_connectionString))
                {
                    await db.Connection.OpenAsync();

                    using (var command = db.Connection.CreateCommand())
                    {
                        BuildCommand(command, query, parameters);
                        numberOfUpdatedRows = await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            { m_logger.Exception("ExecuteAsync", ex); }

            return numberOfUpdatedRows;
        }

        public async Task<bool> TransactionAsync(IList<string> queries, IDictionary<string, object> parameters)
        {
            bool isSucceed = false;

            try
            {
                using (var db = new DbConnection(m_connectionString))
                {
                    await db.Connection.OpenAsync();

                    using (var command = db.Connection.CreateCommand())
                    {
                        foreach (var parameter in parameters ?? Enumerable.Empty<KeyValuePair<string, object>>())
                            command.Parameters.AddWithValue(parameter.Key, parameter.Value);

                        using (var transaction = await db.Connection.BeginTransactionAsync())
                        {
                            command.Transaction = transaction;

                            try
                            {
                                foreach (var query in queries)
                                {
                                    command.CommandText = query;
                                    await command.ExecuteNonQueryAsync();
                                }

                                await transaction.CommitAsync();
                                isSucceed = true;
                            }
                            catch (Exception ex)
                            {
                                m_logger.Exception("TransactionAsync", ex);

                                try
                                { await transaction.RollbackAsync(); }
                                catch (Exception rollbackEx)
                                { m_logger.Exception("TransactionAsync:Rollback", rollbackEx); }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            { m_logger.Exception("TransactionAsync", ex); }

            return isSucceed;
        }

        public async Task<object> FetchScalarAsync(string query, IDictionary<string, object> parameters)
        {
            object result = null;

            try
            {
                using (var db = new DbConnection(m_connectionString))
                {
                    await db.Connection.OpenAsync();

                    using (var command = db.Connection.CreateCommand())
                    {
                        BuildCommand(command, query, parameters);
                        result = await command.ExecuteScalarAsync();
                    }
                }
            }
            catch (Exception ex)
            { m_logger.Exception("FetchScalarAsync", ex); }

            return result;
        }

        public async Task<List<Dictionary<string, object>>> FetchAllAsync(string query, IDictionary<string, object> parameters)
        {
            var result = new List<Dictionary<string, Object>>();

            try
            {
                using (var db = new DbConnection(m_connectionString))
                {
                    await db.Connection.OpenAsync();

                    using (var command = db.Connection.CreateCommand())
                    {
                        BuildCommand(command, query, parameters);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(Enumerable.Range(0, reader.FieldCount).ToDictionary(
                                    i => reader.GetName(i),
                                    i => reader.IsDBNull(i) ? null : reader.GetValue(i)
                                ));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            { m_logger.Exception("FetchAllAsync", ex); }

            return result;
        }

        private static void BuildCommand(MySqlCommand command, string query, IDictionary<string, object> parameters)
        {
            command.CommandText = query;

            foreach (var parameter in parameters ?? Enumerable.Empty<KeyValuePair<string, object>>())
                command.Parameters.AddWithValue(parameter.Key, parameter.Value);
        }

        //private async static Task OnTick()
        //{
        //    while(m_callbackQueue.TryDequeue(out Action action))
        //    {
        //        action.Invoke();
        //    }

        //    await Task.FromResult(0);
        //}
    }
}
