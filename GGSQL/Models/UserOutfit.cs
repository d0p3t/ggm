using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GGSQL.Models
{
    public class UserOutfit
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int OutfitId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
