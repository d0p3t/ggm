using System;

namespace GGSQL.Models
{
    public class GameRound
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; } = DateTime.UtcNow;
        public string MapName { get; set; }
        public int WinnerUserId { get; set; }
        public int WinnerRoundKills { get; set; }
        public int WinnerRoundDeaths { get; set; }
    }
}
