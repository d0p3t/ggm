using System;
using System.Collections.Generic;
using System.Linq;
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
        private int _flushHours;

        private List<User> _cachedUsers;
        private List<Connection> _cachedConnections;

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
            _saveMinutes = 5;
            _flushHours = 3;

            _cachedUsers = new List<User>();
            _cachedConnections = new List<Connection>();

            _logger = new ServerLogger("GGSQL", LogLevel.Info);

            EventHandlers["onServerResourceStart"] += new Action<string>(BaseOnServerResourceStart);
            EventHandlers["playerReady"] += new Action<Player>(OnPlayerReady);
            EventHandlers["gg_internal:updateXpMoney"] += new Action<int, int, int>(OnUpdateXpAndMoney);
            EventHandlers["gg_internal:syncUsers"] += new Action<string>(OnUsersSync);
            EventHandlers["gg_internal:syncUser"] += new Action<string>(OnUserSync);

            Tick += SaveTick;
            Tick += FlushTick;
        }

        public async void BaseOnServerResourceStart(string resourceName)
        {
            if (!API.GetCurrentResourceName().Equals(resourceName, StringComparison.CurrentCultureIgnoreCase)) { return; }

            var stopWatch = new System.Diagnostics.Stopwatch();

            stopWatch.Start();

            await _mysqlDb.DummyQuery();

            stopWatch.Stop();

            _logger.Info($"[DB] Connection established in {stopWatch.ElapsedMilliseconds}ms");
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

                // 1. Check for cached user
                User user = null;

                if (_cachedUsers.Count > 0)
                {
                    user = _cachedUsers.FirstOrDefault(x => x.LicenseId == licenseId);

                    if (user != null)
                        user.NetId = Convert.ToInt32(player.Handle);
                }
                // 2. If no cached user
                if (user == null)
                {
                    user = await _mysqlDb.GetUser(player);

                    // 3. Still null so we create new user
                    if (user == null)
                    {
                        user = await _mysqlDb.CreateNewUser(player);

                        if (user == null)
                        {
                            player.Drop("Failed to load your profile, please try again. Contact an Administrator after 5 failed tries.");
                            _logger.Info($"Failed to load profile for {player.Name} (IP: {player.EndPoint})");
                            API.CancelEvent();
                            return;
                        }
                    }

                    user.NetId = Convert.ToInt32(player.Handle);

                    _cachedUsers.Add(user);
                }

                _logger.Info($"[JOIN] {player.Name} joined. (IP: {player.EndPoint})");

                TriggerEvent("gg_internal:playerReady", JsonConvert.SerializeObject(user));

                var connection = _cachedConnections.FirstOrDefault(c => c.UserId == user.Id);
                if(connection == null)
                    connection = new Connection(user.Id, user.Endpoint);

                var conn = await _mysqlDb.InsertConnection(connection);
                if(conn != null)
                    _cachedConnections.Add(conn);
            }
            catch (Exception ex)
            {
                _logger.Exception("OnPlayerReady", ex);
            }
        }

        public void OnUpdateXpAndMoney(int netId, int addedXp, int addedMoney)
        {
            var user = _cachedUsers.FirstOrDefault(x => x.NetId == netId);

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
                    Exports["ggcommon"].Log("XP/Money Violatoin", $"**Player:** {user.LicenseId}\n**Added XP:** {addedXp}\n**Added Money:** {addedMoney}");
                    player.Drop("Kicked.");
                }

                return;
            }

            user.Xp += addedXp;
            user.Money += addedMoney;

            _logger.Info($"Updated money and xp for {user.LicenseId}. Total XP: {user.Xp} Total Money: ${user.Money}");
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

        private async Task FlushTick()
        {
            try
            {
                await Delay(60000 * 60 * _flushHours);

                if (_cachedUsers.Count == 0) return;

                var success = await _mysqlDb.SaveUsers(_cachedUsers); // Lets first save users

                if (!success) return;

                List<User> usersToKeep = new List<User>();

                foreach(var player in Players)
                {
                    var user = _cachedUsers.FirstOrDefault(x => x.NetId == Convert.ToInt32(player.Handle));
                    if(user != null)
                    {
                        usersToKeep.Add(user);
                    }
                }

                if(usersToKeep.Count > 0)
                    _cachedUsers = usersToKeep;

                _logger.Info($"Flush complete. Kept {usersToKeep.Count} Users in Cache.");
            }
            catch (Exception e)
            {
                _logger.Exception("FlushTick", e);
            }
        }

        private void OnUsersSync(string data)
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<List<SyncUser>>(data);

                var count = 0;
                foreach (var syncUser in parsed)
                {
                    var user = _cachedUsers.FirstOrDefault(x => x.NetId == syncUser.NetId && x.Id == syncUser.Id);

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

        private async void OnUserSync(string data)
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<SyncUser>(data);

                var user = _cachedUsers.FirstOrDefault(x => x.NetId == parsed.NetId && x.Id == parsed.Id);

                if(user != null)
                {
                    user.Kills = parsed.Kills;
                    user.Deaths = parsed.Deaths;
                    user.Xp = parsed.Xp;
                    user.Money = parsed.Money;

                    var success = await _mysqlDb.SaveUser(user);

                    var droppedConnection = _cachedConnections.FirstOrDefault(c => c.UserId == user.Id);
                    if (droppedConnection != null)
                    {
                        success = await _mysqlDb.UpdateConnection(droppedConnection);
                        _cachedConnections.Remove(droppedConnection);
                    }

                    _logger.Info($"Saving profile was {(success ? "Successful" : "Unsuccessful")}");
                }
            }
            catch (Exception e)
            {
                _logger.Exception("OnUserSync", e);
            }
        }
    }
}
