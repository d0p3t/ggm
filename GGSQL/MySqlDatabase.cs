using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly CustomTaskScheduler _scheduler;

        public MySqlDatabase(string connectionString)
        {
            _connectionString = connectionString;
            _scheduler = new CustomTaskScheduler();
        }

        public async Task<User> GetUser(string licenseId, string steamId, string xblId, string liveId, string discordId, string fivemId)
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
                            LastConnected AS `LastConnected`
                        FROM
	                        users
                        WHERE
                            licenseId = @lid
                        OR
	                        steamId = @sid
                        OR 
                            xblId = @xid 
                        OR 
                            liveId = @liveid 
                        OR 
                            discordId = @did 
                        OR 
                            fivemId = @fid;
                        SELECT
                            ClothingStyles AS `ClothingStyles`
                        FROM
                            users
                        WHERE
                            licenseId = @lid
                        OR
	                        steamId = @sid
                        OR 
                            xblId = @xid 
                        OR 
                            liveId = @liveid 
                        OR 
                            discordId = @did 
                        OR 
                            fivemId = @fid;";

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
                        dbUser.ClothingStyles = new List<ClothingStyle> { new ClothingStyle(1) };
                    }
                    else
                    {
                        dbUser.ClothingStyles = JsonConvert.DeserializeObject<List<ClothingStyle>>(clothingStyles);
                    }
                    return dbUser;
                }
            }
        }

        public async Task<Ban> IsUserBanned(string licenseId, string steamId, string xblId, string liveId, string discordId, string fivemId)
        {
            var sql = @"SELECT
                            End_date AS `EndDate`,
                            Reason AS `Reason`
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
                            kills=@kills
                            deaths=@deaths
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
    }
}
