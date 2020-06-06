using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CitizenFX.Core.Native.API;

namespace GGQ.Models
{
    public class Config
    {
        public List<PriorityPlayer> PriorityPlayers { get; set; } = new List<PriorityPlayer>();
        public int DisconnectGrace { get; set; } = 60;
        public int ConnectionTimeout { get; set; } = 120;
        public int DeferralDelay { get; set; } = 250;
        public bool QueueWhenNotFull { get; set; } = false;

        public int MaxClients { get; private set; }
        public string HostName { get; set; }

        public Config()
        {
            MaxClients = GetConvarInt("sv_maxclients", 32);
            HostName = GetConvar("sv_hostname", "");
        }
    }
}
