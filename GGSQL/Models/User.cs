using System;
using System.Collections.Generic;

namespace GGSQL.Models
{
    public class User
    {
        public int Id { get; set; }

        public int NetId { get; set; }
        public string LicenseId { get; set; }

        public string SteamId { get; set; }

        public string XblId { get; set; }

        public string LiveId { get; set; }

        public string DiscordId { get; set; }

        public string FivemId { get; set; }

        public string Endpoint { get; set; }

        public int Kills { get; set; } = 0;

        public int Deaths { get; set; } = 0;

        public int Xp { get; set; } = 0;

        public int Money { get; set; } = 500;

        public DateTime LastConnected { get; set; } = DateTime.UtcNow;

        public List<ClothingStyle> ClothingStyles { get; set; } = new List<ClothingStyle> { new ClothingStyle(1) };

        public List<WeaponStyle> WeaponStyles { get; set; } = new List<WeaponStyle>();

        public string PedComponent { get; set; }
    }
}
