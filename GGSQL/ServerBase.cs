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

        private List<User> _cachedUsers;

        protected MySqlDatabase _mysqlDb
        {
            get
            {
                if(_db == null)
                {
                    _db = new MySqlDatabase(API.GetConvar("mysql_connection_string", "notset"));
                }
                return _db;
            }
        }

        public ServerBase()
        {
            _cachedUsers = new List<User>();
            _logger = new ServerLogger("GGSQL", LogLevel.Info);

            EventHandlers["onServerResourceStart"] += new Action<string>(BaseOnServerResourceStart);
            EventHandlers["playerDropped"] += new Action<Player, string>(OnPlayerDropped);
            EventHandlers["playerReady"] += new Action<Player>(OnPlayerReady);
        }

        public async void BaseOnServerResourceStart(string resourceName)
        {
            if (!API.GetCurrentResourceName().Equals(resourceName, StringComparison.CurrentCultureIgnoreCase)) { return; }
            var user = await _mysqlDb.GetUser("license:b91d74d438e493d8b5a1cfb3b86638d6c95e46c0", "INVALID", "INVALID", "INVALID", "INVALID", "INVALID");
            Debug.WriteLine($"User is {user.Id}");
            Debug.WriteLine($"Slot 1 model is {user.ClothingStyles.First().ModelName}");
        }

        public async void OnPlayerDropped([FromSource]Player player, string reason)
        {
            var droppedUser = _cachedUsers.Find(x => x.NetId == Convert.ToInt32(player.Handle));

            var success = false;

            if(droppedUser != null)
            {
                success = await _mysqlDb.SaveUser(droppedUser);

                _cachedUsers.Remove(droppedUser);
            }

            _logger.Info($"{player.Name} left (Reason: {reason}) - Saving profile was {(success ? "Successful" : "Unsuccessful")}");
        }

        public async void OnPlayerReady([FromSource]Player player)
        {
            _logger.Info($"Called Player Ready by {player.Name}");
            try
            {
                _logger.Info($"[JOIN] {player.Name} joined. (IP: {player.EndPoint})");
                var licenseId = player.Identifiers["license"];
                var steamId = player.Identifiers["steam"];
                var xblId = player.Identifiers["xbl"];
                var liveId = player.Identifiers["live"];
                var discordId = player.Identifiers["discord"];
                var fivemId = player.Identifiers["fivem"];

                _logger.Info($"License {licenseId} | Steam {steamId} | XBL {xblId} | Live {liveId} | Discord {discordId} | FiveM {fivemId}");

                // 1. Check for cached user
                User user = null;

                if (_cachedUsers.Count > 0)
                {
                    user = _cachedUsers.FirstOrDefault(x => x.LicenseId == licenseId || x.SteamId == steamId || x.XblId == xblId || x.LiveId == liveId || x.DiscordId == discordId || x.FivemId == fivemId);
                }
                // 2. If no cached user
                if(user == null)
                {
                    user = await _mysqlDb.GetUser(licenseId, steamId, xblId, liveId, discordId, fivemId);

                    // 3. Still null so we create new user
                    if(user == null)
                    {
                        user = await _mysqlDb.CreateNewUser(player);
                    }

                    _cachedUsers.Add(user);
                }

                TriggerEvent("gg_internal:playerReady", JsonConvert.SerializeObject(user));
            }
            catch (Exception ex)
            {
                _logger.Exception("OnPlayerReady", ex);
            }
        }
    }
}
