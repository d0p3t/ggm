using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using GGSQL.Models;
using GGSQL.Models.Styles;
using Newtonsoft.Json;
using System.Linq;

namespace GGSQL.Controllers
{
    public class LobbyController: BaseScript
    {
        private static readonly List<int> staticLobbyRoutingBuckets = new List<int>
        {
            1
        };

        internal LobbyController(ServerLogger logger)
        {
            EventHandlers["lobby:join"] += new Action<Player, int>(OnJoinLobby);
        }

        public void OnJoinLobby([FromSource]Player player, int lobbyId)
        {
            var exists = staticLobbyRoutingBuckets.Contains(lobbyId);

            if(!exists)
            {
                Debug.WriteLine("Try to join non-existent lobby");
                return;
            }

            var userData = Cache.Users.FirstOrDefault(user => user.NetId == Convert.ToInt32(player.Handle));

            if(userData == null)
            {
                Debug.WriteLine("userData doesn't exist");
                return;
            }

            TriggerEvent("allowJoinLobby", JsonConvert.SerializeObject(userData), lobbyId);
        }
    }
}
