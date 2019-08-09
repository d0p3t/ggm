using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using GGSQL.Models;

namespace GGSQL
{
    public class ServerBase : BaseScript
    {
        protected ServerLogger _logger;

        private MySqlDatabase _db = null;
        private bool _dbDebug = false;

        private int _saveMinutes;

        private List<User> _cachedUsers;
        private List<Connection> _cachedConnections;
        private List<Ban> _cachedBans;

        private List<BanLog> _cachedTempBans;

        protected MySqlDatabase _mysqlDb
        {
            get
            {
                if(_db == null)
                {
                    _dbDebug = API.GetConvarInt("mysql_debug", 0) == 1 ? true : false;
                    _db = new MySqlDatabase(API.GetConvar("mysql_connection_string", "notset"), _dbDebug);
                }
                return _db;
            }
        }

        public ServerBase()
        {
            _saveMinutes = 15;

            _cachedUsers = new List<User>();
            _cachedConnections = new List<Connection>();
            _cachedTempBans = new List<BanLog>();

            _logger = new ServerLogger("GGSQL", LogLevel.Info);

            EventHandlers["onServerResourceStart"] += new Action<string>(BaseOnServerResourceStart);
            EventHandlers["playerDropped"] += new Action<Player, string>(OnPlayerDropped);
            EventHandlers["playerReady"] += new Action<Player>(OnPlayerReady);
            EventHandlers["gg_internal:updateXpMoney"] += new Action<int, int, int>(OnUpdateXpAndMoney);
            EventHandlers["gg_internal:userSync"] += new Action<string>(OnUserSync);
            EventHandlers["gg_internal:TempLogBan"] += new Action<string, string>(OnTempLogBan);

            Exports.Add("gg_internal:checkIsBanned", new Func<string, string, string, string, string, string, dynamic>((license, steam, xbl, live, discord, fivem) => OnCheckBan(license, steam, xbl, live, discord, fivem)));

            Tick += SaveTick;

            API.RegisterCommand("banlog", new Action<int, List<object>, string>((source, args, raw) => {
                if(source != 0)
                {
                    return;
                }

                List<BanLog> toRemove = new List<BanLog>();
                foreach (var banLog in _cachedTempBans)
                {
                    if(banLog.Expiration > DateTime.UtcNow)
                    {
                        toRemove.Add(banLog);
                        continue;
                    }

                    Debug.WriteLine($"[{banLog.Occurrence}] Name: {banLog.Name} (Reason {banLog.Reason})");
                }

                if(toRemove.Count != 0)
                {
                    foreach (var item in toRemove)
                    {
                        _cachedTempBans.Remove(item);
                    }
                    Debug.WriteLine($"Cleaned up cached temp bans (Count: {toRemove.Count})");
                }
            }), true);
        }

        public void OnTempLogBan(string name, string reason)
        {
            var banLog = new BanLog(name, reason);
            _cachedTempBans.Add(banLog);
        }

        public dynamic OnCheckBan(string license, string steam, string xbl, string live, string discord, string fivem)
        {
            List<Ban> toRemove = new List<Ban>();

            foreach (var ban in _cachedBans)
            {
                if(DateTime.UtcNow > ban.EndDate)
                {
                    toRemove.Add(ban);
                    continue;
                }

                if(ban.LicenseId == license || ban.SteamId == steam || ban.XblId == xbl || ban.LiveId == live || ban.DiscordId == discord || ban.FivemId == fivem)
                {
                    return new { IsBanned = true, Reason = ban.Reason };
                }
            }

            return new { IsBanned = false, Reason = "" };
        }

        public async void BaseOnServerResourceStart(string resourceName)
        {
            if (!API.GetCurrentResourceName().Equals(resourceName, StringComparison.CurrentCultureIgnoreCase)) { return; }

            await _mysqlDb.DummyQuery();

            _cachedBans = await _mysqlDb.GetAllActiveBans();

            _logger.Info("Performed Dummy Query");
        }

        public async void OnPlayerDropped([FromSource]Player player, string reason)
        {
            var droppedUser = _cachedUsers.Find(x => x.NetId == Convert.ToInt32(player.Handle));

            var success = false;

            if(droppedUser != null)
            {
                success = await _mysqlDb.SaveUser(droppedUser);

                var connection = _cachedConnections.Find(x => x.UserId == droppedUser.Id);
                if (connection != null)
                {
                    await _mysqlDb.UpdateConnection(connection);
                    _cachedConnections.Remove(connection);
                }

                _cachedUsers.Remove(droppedUser);
            }

            _logger.Info($"{player.Name} left (Reason: {reason}) - Saving profile was {(success ? "Successful" : "Unsuccessful")}");
        }

        public async void OnPlayerReady([FromSource]Player player)
        {
            try
            {
                var licenseId = player.Identifiers["license"];
                var steamId = player.Identifiers["steam"];
                var xblId = player.Identifiers["xbl"];
                var liveId = player.Identifiers["live"];
                var discordId = player.Identifiers["discord"];
                var fivemId = player.Identifiers["fivem"];

                // 1. Check for cached user (only really useful on resource restart)
                User user = null;

                if (_cachedUsers.Count > 0)
                {
                    user = _cachedUsers.FirstOrDefault(x => x.LicenseId == licenseId || x.SteamId == steamId || x.XblId == xblId || x.LiveId == liveId || x.DiscordId == discordId || x.FivemId == fivemId);
                }
                // 2. If no cached user
                if(user == null)
                {
                    user = await _mysqlDb.GetUser(player);

                    // 3. Still null so we create new user
                    if(user == null)
                    {
                        user = await _mysqlDb.CreateNewUser(player);

                        if(user == null)
                        {
                            player.Drop("Failed to load your profile, please try again. Contact an Administrator after 5 failed tries.");
                            _logger.Info($"Failed to load profile for {player.Name} (IP: {player.EndPoint})");
                            API.CancelEvent();
                            return;
                        }
                    }

                    _cachedUsers.Add(user);
                }

                var connection = new Connection(user.Id, user.Endpoint);
                connection = await _mysqlDb.InsertConnection(connection);
                _cachedConnections.Add(connection);

                _logger.Info($"[JOIN] {player.Name} joined. (IP: {player.EndPoint})");

                TriggerEvent("gg_internal:playerReady", JsonConvert.SerializeObject(user));
            }
            catch (Exception ex)
            {
                _logger.Exception("OnPlayerReady", ex);
            }
        }

        public void OnUpdateXpAndMoney(int netId, int addedXp, int addedMoney)
        {
            var user = _cachedUsers.Find(x => x.NetId == netId);

            if (user == null)
            {
                return;
            }

            var player = Players.FirstOrDefault(x => x.Handle == user.NetId.ToString());

            if (addedXp > 99999 || addedMoney > 99999)
            {
                _logger.Warning($"LicenseId [{user.LicenseId}] XP [{addedXp}] or Money [{addedMoney}] update violation!");

                if(player != null)
                {
                    player.Drop("Kicked.");
                }

                return;
            }

            user.Xp += addedXp;
            user.Money += addedMoney;
        }

        private async Task SaveTick()
        {
            try
            {
                await Delay(60000 * _saveMinutes);

                if (_cachedUsers.Count > 0)
                {
                    var success = await _mysqlDb.SaveUsers(_cachedUsers);

                    _logger.Info($"Saving profiles was {(success ? "Successful" : "Unsuccessful")}");
                }
            }
            catch (Exception e)
            {
                _logger.Exception("SaveTick", e);
            }
        }

        private void OnUserSync(string data)
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<List<SyncUser>>(data);

                var count = 0;
                foreach (var syncUser in parsed)
                {
                    var user = _cachedUsers.Find(x => x.NetId == syncUser.NetId && x.Id == syncUser.Id);

                    if (user == null)
                    {
                        continue;
                    }

                    user.Kills = syncUser.Kills;
                    user.Deaths = syncUser.Deaths;
                    user.Xp = syncUser.Xp;
                    user.Money = syncUser.Money;

                    count++;
                }

                _logger.Info($"Synced {count} users");
            }
            catch (Exception e)
            {
                _logger.Exception("OnUserSync", e);
            }

        }
    }
}
