using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using CitizenFX.Core;
using GGSQL.Models;

namespace GGSQL
{
    public class MySqlDatabase
    {
        private string _connectionString;
        private bool _debug;
        private readonly CustomTaskScheduler _scheduler;

        public MySqlDatabase(string connectionString, bool debug = false)
        {
            _connectionString = connectionString;
            _debug = debug;
            _scheduler = new CustomTaskScheduler();
        }

        public async Task DummyQuery()
        {
            var sql = "SELECT VERSION();";

            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.QuerySingleAsync(sql);
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
                            (fivemId IS NOT NULL AND fivemId = @fid);
                        SELECT
                            ClothingStyles AS `ClothingStyles`
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

            using (var conn = new MySqlConnection(_connectionString))
            {
                using (var multi = await conn.QueryMultipleAsync(sql, new { lid = licenseId, sid = steamId, xid = xblId, liveid = liveId, did = discordId, fid = fivemId }))
                {
                    var dbUser = multi.Read<User>().FirstOrDefault();

                    if(dbUser == null)
                    {
                        return null;
                    }

                    var clothingStyles = multi.Read<string>().FirstOrDefault();
                    if (clothingStyles == null)
                    {
                        dbUser.ClothingStyles = new List<ClothingStyle> { new ClothingStyle(1) { IsActiveStyle = true}, new ClothingStyle(2), new ClothingStyle(3) };
                    }
                    else
                    {
                        dbUser.ClothingStyles = JsonConvert.DeserializeObject<List<ClothingStyle>>(clothingStyles);
                    }

                    dbUser.NetId = Convert.ToInt32(player.Handle);
                    dbUser.Endpoint = player.EndPoint;

                    return dbUser;
                }
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

            using (var conn = new MySqlConnection(_connectionString))
            {
                var dbResult = await conn.QueryFirstOrDefaultAsync<Ban>(sql, new { t = DateTime.UtcNow, lid = licenseId, sid = steamId, xid = xblId, liveid = liveId, did = discordId, fid = fivemId });
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

            using (var conn = new MySqlConnection(_connectionString))
            {
                var affectedRows = await conn.ExecuteAsync(sql, JsonConvert.SerializeObject(clothingStyles));

                if (affectedRows != 0)
                {
                    return true;
                }

                return false;
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

            var clothingStyles = JsonConvert.SerializeObject(user.ClothingStyles);

            using (var conn = new MySqlConnection(_connectionString))
            {
                var dbResult = await conn.ExecuteScalarAsync<int>(sql, new { lid = user.LicenseId, sid = user.SteamId, xblid = user.XblId, liveid = user.LiveId, did = user.DiscordId, endpoint = user.Endpoint, cstyles = clothingStyles });
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

            using (var conn = new MySqlConnection(_connectionString))
            {
                var clothingStyles = JsonConvert.SerializeObject(user.ClothingStyles);
                var affectedRows = await conn.ExecuteAsync(sql, new { userId = user.Id, kills = user.Kills, deaths = user.Deaths, xp = user.Xp, money = user.Money, now = DateTime.UtcNow, cstyles = clothingStyles });
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

            using(var conn = new MySqlConnection(_connectionString))
            {
                var affectedRows = await conn.ExecuteAsync(sql);
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

            using(var conn = new MySqlConnection(_connectionString))
            {
                var dbResult = await conn.ExecuteScalarAsync<int>(sql, new { e = connection.Established, userId = connection.UserId, ip = connection.EndPoint});
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

            using (var conn = new MySqlConnection(_connectionString))
            {
                var affectedRows = await conn.ExecuteAsync(sql, new { id = connection.Id, dropped = DateTime.UtcNow, totalSeconds = (DateTime.UtcNow - connection.Established).TotalSeconds});

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

            using (var conn = new MySqlConnection(_connectionString))
            {
                var dbResult = await conn.QueryAsync<Ban>(sql, new { now = DateTime.UtcNow });

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

            using (var conn = new MySqlConnection(_connectionString))
            {
                var dbResult = await conn.ExecuteScalarAsync<int>(sql, new 
                {   st = gameRound.StartTime, et = gameRound.EndTime, 
                    mn = gameRound.MapName,  wuid = gameRound.WinnerUserId,
                    wrk = gameRound.WinnerRoundKills, wrd = gameRound.WinnerRoundDeaths  
                });

                gameRound.Id = dbResult;
                return gameRound;
            }
        }
    }
}
