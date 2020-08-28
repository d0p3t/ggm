using System;

namespace GGSQL.Models
{
    public class WeaponTint
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }
        public int RequiredXp { get; set; } = 0;
        public float Discount { get; set; } = 0.0f;
        public bool Enabled { get; set; } = true;
        public string Image { get; set; } = "no_image.png";
        public string Description { get; set; }
        public int TebexPackageId { get; set; } = 0;
        public bool DonatorExclusive { get; set; } = false;
        public bool IsMk2 { get; set; } = false;
        public int TintId { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
