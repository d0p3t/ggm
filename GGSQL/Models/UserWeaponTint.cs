using System;

namespace GGSQL.Models
{
    public class UserWeaponTint
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int WeaponTintId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpirationDate { get; set; } = DateTime.UtcNow.AddDays(1825);
    }
}
