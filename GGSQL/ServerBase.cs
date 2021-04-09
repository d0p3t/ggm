using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using GGSQL.Models;
using GGSQL.Models.Styles;
using GGSQL.Controllers;

namespace GGSQL
{
    public class ServerBase : BaseScript
    {
        protected ServerLogger _logger;

        private MySqlDatabase _db = null;
        private bool _dbDebug = false;
        private bool _firstTick = true;

        private int _saveMinutes;
        private int _flushHours;

        private ShopController m_shopController;

        protected MySqlDatabase _mysqlDb
        {
            get
            {
                if(_db == null)
                {
                    _dbDebug = API.GetConvarInt("mysql_debug", 0) == 1 ? true : false;
                    _db = new MySqlDatabase(API.GetConvar("mysql_connection_string", "notset"), _dbDebug, _logger);
                }
                return _db;
            }
        }

        public ServerBase()
        {
            Exports.Add("QueryResult", new Func<string, dynamic, Task<List<dynamic>>>(
                (query, parameters) => QueryResult(query, parameters))
            );
            Exports.Add("QueryAsync", new Action<string, dynamic, CallbackDelegate>(
                (query, parameters, cb) => QueryAsync(query, parameters, cb))
            );

            _saveMinutes = 15;
            _flushHours = 3;

            _logger = new ServerLogger("GGSQL", LogLevel.Info);

            EventHandlers["onServerResourceStart"] += new Action<string>(BaseOnServerResourceStart);
            EventHandlers["playerReady"] += new Action<Player>(OnPlayerReady);
            EventHandlers["gg_internal:updateXpMoney"] += new Action<int, int, int>(OnUpdateXpAndMoney);
            EventHandlers["gg_internal:syncUsers"] += new Action<string>(OnUsersSync);
            EventHandlers["gg_internal:syncUser"] += new Action<string, string>(OnUserSync);
            EventHandlers["gg_internal:syncWinner"] += new Action<string>(OnWinnerSync);

            // Tick += SaveTick;
            // Tick += FlushTick;
            Tick += InitializeController;
        }

        private async Task<List<dynamic>> QueryResult(string query, dynamic parameters)
        {
            var result = await _mysqlDb.QueryResult(query, _mysqlDb.TryParseParameters(parameters));
            return result;
        }

        private void QueryAsync(string query, dynamic parameters, CallbackDelegate callback = null)
        {
            Task<int> resultTask = _mysqlDb.Query(query, _mysqlDb.TryParseParameters(parameters));
#pragma warning disable CS4014
            resultTask.ContinueWith((task) => callback?.Invoke(task.Result));
#pragma warning restore CS4014
        }

        public async Task InitializeController()
        {
            await Delay(0);
            if (_firstTick)
            {
                _firstTick = false;
                m_shopController = new ShopController(_mysqlDb, _logger);
                RegisterScript(m_shopController);

                Tick -= InitializeController;
            }
        }

        public async void BaseOnServerResourceStart(string resourceName)
        {
            if (!API.GetCurrentResourceName().Equals(resourceName, StringComparison.CurrentCultureIgnoreCase)) { return; }

            var stopWatch = new System.Diagnostics.Stopwatch();

            stopWatch.Start();

            await _mysqlDb.DummyQuery();

            stopWatch.Stop();

            _logger.Info($"[DB] Connection established in {stopWatch.ElapsedMilliseconds}ms");
            stopWatch = null;
        }

        public async void OnPlayerReady([FromSource]Player player)
        {
            try
            {
                var licenseId = player.Identifiers["license"];

                // 1. Check for cached user
                User user = null;

                if (Cache.Users.Count > 0)
                {
                    user = Cache.Users.FirstOrDefault(x => x.LicenseId == licenseId);

                    if (user != null)
                    {
                        user.NetId = Convert.ToInt32(player.Handle); ;
                    }
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
                            return;
                        }
                    }

                    user.NetId = Convert.ToInt32(player.Handle);

                    Cache.Users.Add(user);

                }

                try
                {
                    bool loadCommerce = false;
                    if(API.CanPlayerStartCommerceSession(player.Handle))
                    {
                        if (!API.IsPlayerCommerceInfoLoadedExt(player.Handle))
                        {
                            API.LoadPlayerCommerceDataExt(player.Handle);

                            var start = API.GetGameTimer();
                            var broke = false;
                            while (!API.IsPlayerCommerceInfoLoadedExt(player.Handle))
                            {
                                if(API.GetGameTimer() - start > 5000)
                                {
                                    broke = true;
                                    break;
                                }
                                await Delay(100);
                            }

                            if(!broke)
                                loadCommerce = true;
                        }
                    }

                    await m_shopController.GetUserOutfits(user.Id, user.NetId, loadCommerce);
                    await m_shopController.GetUserWeaponTints(user.Id, user.NetId, loadCommerce);
                    // await m_shopController.GetUserGeneralItems(user.Id, user.NetId, loadCommerce);
                    // m_shopController.ActivateGeneralItems(user.Id, user.NetId);
                }
                catch (Exception ex)
                {
                    _logger.Exception("OnPlayerReady - ShopController stuff", ex);
                }

                try
                {
                        var style = await m_shopController.GetActiveUserOutfit(player, user.ActiveUserOutfit, user.Id);
                        if(user.ActiveUserOutfit == 0 && style != null)
                        {
                            user.ActiveUserOutfit = style.Id;
                        }

                        var tint = await m_shopController.GetActiveUserWeaponTint(player, user.ActiveUserWeaponTint, user.Id);

                        if(user.ActiveUserWeaponTint == 0 && tint != null)
                        {
                            user.ActiveUserWeaponTint = tint.Id;
                        }

                        List<PedComponent> components = new List<PedComponent>
                        {
                            new PedComponent(1, 57, 0, 2), // Head
                            new PedComponent(3, 41, 0, 2), // Torso
                            new PedComponent(4, 98, 13, 2), // Legs
                            new PedComponent(6, 71, 13, 2), // Feet
                            new PedComponent(8, 15, 0, 2), // Accessoires
                            new PedComponent(11, 251, 13, 2) // Torso2
                        };

                        int weaponTintIndex = 0;

                        try
                        {
                            if (style != null)
                            {
                                var outfit = Cache.Outfits.FirstOrDefault(o => o.Id == style.OutfitId);

                                if (outfit != null)
                                {
                                    components = outfit.Components;
                                }
                            }

                            if(tint != null)
                            {
                                var weaponTint = Cache.WeaponTints.FirstOrDefault(t => t.Id == tint.WeaponTintId);

                                if (weaponTint != null)
                                {
                                    weaponTintIndex = weaponTint.TintId;
                                }
                            }

                            try
                            {
                                var clothingStyle = new ClothingStyle(1)
                                {
                                    PedComponents = components,
                                    IsActiveStyle = true,
                                    User_id = user.Id
                                };

                                TriggerEvent("gg_internal:playerReady", JsonConvert.SerializeObject(user), JsonConvert.SerializeObject(clothingStyle), weaponTintIndex);
                                TriggerEvent("gg_internal:updateTags", user.NetId, user.Moderator, user.Donator, user.Xp);
                            }
                            catch (Exception)
                            {
                                Debug.WriteLine($"User ActiveUserOutfit? {user.ActiveUserOutfit} | Components? {components[0].ComponentId}");
                            }
                        }
                        catch (Exception)
                        {
                            Debug.WriteLine("ACTUALLY HERE");
                        }
                }
                catch (Exception ex)
                {
                    _logger.Exception("OnPlayerReady - Sending playerReady -", ex);
                }

                try
                {
                    var connection = Cache.Connections.FirstOrDefault(c => c.UserId == user.Id);
                    if (connection == null)
                        connection = new Connection(user.Id, user.Endpoint);

                    var conn = await _mysqlDb.InsertConnection(connection);
                    if (conn != null)
                        Cache.Connections.Add(conn);
                }
                catch (Exception ex)
                {
                    _logger.Exception("OnPlayerReady - Adding new connection -", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.Exception("OnPlayerReady - Other -", ex);
            }

            _logger.Info($"[JOIN] [{player.Handle}] {player.Name} joined. (IP: {player.EndPoint})^7");
        }

        public void OnUpdateXpAndMoney(int netId, int addedXp, int addedMoney)
        {
            var user = Cache.Users.FirstOrDefault(x => x.NetId == netId);

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
                    Exports["ggcommon"].Log("XP/Money Violation", $"**Player:** {user.LicenseId}\n**Added XP:** {addedXp}\n**Added Money:** {addedMoney}");
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

                if (Cache.Users.Count > 0)
                {
                    var success = await _mysqlDb.SaveUsers(Cache.Users);

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

                if (Cache.Users.Count == 0) return;

                var success = await _mysqlDb.SaveUsers(Cache.Users); // Lets first save Cache.Users

                if (!success) return;

                List<User> UsersToKeep = new List<User>();

                foreach(var player in Players)
                {
                    var user = Cache.Users.FirstOrDefault(x => x.NetId == Convert.ToInt32(player.Handle));
                    if(user != null)
                    {
                        UsersToKeep.Add(user);
                    }
                }

                if(UsersToKeep.Count > 0)
                    Cache.Users = UsersToKeep;

                _logger.Info($"Flush complete. Kept {UsersToKeep.Count} Users in Cache.");
            }
            catch (Exception e)
            {
                _logger.Exception("FlushTick", e);
            }
        }

        private async void OnUsersSync(string data)
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<List<SyncUser>>(data);

                var count = 0;
                foreach (var syncUser in parsed)
                {
                    try
                    {
                        var user = Cache.Users.FirstOrDefault(x => x.NetId == syncUser.NetId && x.Id == syncUser.Id);

                        if (user == null)
                        {
                            continue;
                        }

                        user.Kills = syncUser.Kills;
                        user.Deaths = syncUser.Deaths;
                        user.Xp = syncUser.Xp;
                        user.Money = syncUser.Money;

                        await m_shopController.UpdateOutfits(user.NetId, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.Exception("OnUsersSync:foreachStatement", ex);
                    }

                    count++;
                }

                _logger.Info($"Synced {count} Users");
            }
            catch (Exception e)
            {
                _logger.Exception("OnUsersSync", e);
            }
        }

        private async void OnUserSync(string data, string name)
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<SyncUser>(data);

                var user = Cache.Users.FirstOrDefault(x => x.NetId == parsed.NetId && x.Id == parsed.Id);

                if(user != null)
                {
                    user.Kills = parsed.Kills;
                    user.Deaths = parsed.Deaths;
                    user.Xp = parsed.Xp;
                    user.Money = parsed.Money;

                    var success = await _mysqlDb.SaveUser(user);

                    var droppedConnection = Cache.Connections.FirstOrDefault(c => c.UserId == user.Id);
                    if (droppedConnection != null)
                    {
                        success = await _mysqlDb.UpdateConnection(droppedConnection);
                        Cache.Connections.Remove(droppedConnection);
                    }

                    if (success)
                    {
                        success = await m_shopController.RemoveUserOutfitsFromCache(user.NetId);
                        Cache.Users.Remove(user);
                    }

                    _logger.Info($"Saving profile of {name}^7 was {(success ? "Successful" : "Unsuccessful")}");
                }
            }
            catch (Exception e)
            {
                _logger.Exception("OnCache.Usersync", e);
            }
        }

        private async void OnWinnerSync(string data)
        {
            try
            {
                _logger.Info(data);
                var parsed = JsonConvert.DeserializeObject<GameRound>(data);
                var gameRound = await _mysqlDb.InsertGameRound(parsed);

                _logger.Info($"[GAME] Round saved. Winner had [{gameRound.WinnerRoundKills}] kills and [{gameRound.WinnerRoundDeaths}] deaths.");
            }
            catch (Exception e)
            {
                _logger.Exception("OnCache.Usersync", e);
            }
        }
    }
}
