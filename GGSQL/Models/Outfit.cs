﻿using GGSQL.Models.Styles;
using System;
using System.Collections.Generic;

namespace GGSQL.Models
{
    public class Outfit
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
        public List<PedComponent> Components { get; set; } = new List<PedComponent>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
