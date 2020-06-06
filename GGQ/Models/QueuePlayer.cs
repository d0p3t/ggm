using System;
using System.Dynamic;

namespace GGQ.Models
{
    public class QueuePlayer: IPlayer
    {
        public int Handle { get; set; }
        public ExpandoObject Deferrals { get; set; }
        public string Dots { get; set; } = "";
        public QueueStatus Status { get; set; } = QueueStatus.Queued;
        public DateTime JoinTime { get; set; }
        public int JoinCount { get; set; }
        public DateTime ConnectTime { get; set; }
        public DateTime DisconnectTime { get; set; }
        public int Priority { get; set; }
        public string LicenseId { get; set; }
        public string SteamId { get; set; }
        public string XblId { get; set; }
        public string LiveId { get; set; }
        public string DiscordId { get; set; }
        public string FivemId { get; set; }
        public string Name { get; set; }
    }
}
