using System;

namespace GGSQL
{
    public class UserGeneralItem
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ItemId { get; set; }
        public bool IsOneTimeActivation { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpirationDate { get; set; } = DateTime.UtcNow.AddDays(1825);
    }
}
