using System;

namespace GGSQL.Models
{
    public class Connection
    {
        public int Id { get; set; }

        public DateTime Established { get; set; } = DateTime.UtcNow;

        public DateTime Dropped { get; set; }

        public int TotalSeconds { get; set; } = 0;

        public int UserId { get; set; }

        public string EndPoint { get; set; }

        public Connection(int userId, string endPoint)
        {
            UserId = userId;
            EndPoint = endPoint;
        }
    }
}
