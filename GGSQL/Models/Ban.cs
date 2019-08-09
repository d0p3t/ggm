using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GGSQL.Models
{
    public class Ban
    {
        public int Id { get; set;}

        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime EndDate { get; set; }

        public string Reason { get; set;}

        public string LicenseId { get; set; }

        public string SteamId { get; set; }

        public string XblId { get; set; }

        public string LiveId { get; set; }

        public string DiscordId { get; set; }

        public string FivemId { get; set; }

        public bool IsActive()
        {
            if(EndDate > DateTime.UtcNow)
            {
                return true;
            }

            return false;
        }
    }

    public class BanLog
    {
        public string Name { get; set; }

        public string Reason { get; set; }

        public DateTime Occurrence { get; set; } = DateTime.UtcNow;

        public DateTime Expiration { get; set; } = DateTime.Today.AddDays(30).ToUniversalTime();

        public BanLog(string name, string reason)
        {
            Name = name;
            Reason = reason;
        }
    }
}
