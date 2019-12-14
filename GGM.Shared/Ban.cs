using System;

namespace GGM.Shared
{
    public class Ban
    {
        public int Id { get; set; }

        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime EndDate { get; set; }

        public string Reason { get; set; }

        public string LicenseId { get; set; }

        public string SteamId { get; set; }

        public string XblId { get; set; }

        public string LiveId { get; set; }

        public string DiscordId { get; set; }

        public string FivemId { get; set; }

        public bool IsActive()
        {
            if (EndDate > DateTime.UtcNow)
            {
                return true;
            }

            return false;
        }
    }
}
