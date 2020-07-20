using System;

namespace GGSQL.Models
{
    public class GeneralItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }
        public int Price { get; set; }
        public int RequiredXp { get; set; } = 0;
        public float Discount { get; set; } = 0.0f;
        public bool Enabled { get; set; } = true;
        public int TebexPackageId { get; set; } = 0;
        public TimeSpan Duration { get; set; } = TimeSpan.FromDays(30); 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
