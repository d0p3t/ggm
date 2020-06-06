namespace GGQ.Models
{
    public class PriorityPlayer : IPlayer
    {
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
