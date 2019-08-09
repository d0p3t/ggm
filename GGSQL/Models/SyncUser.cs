using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GGSQL.Models
{
    public class SyncUser
    {
        public int Id { get; set; }
        public int NetId { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Xp { get; set; }
        public int Money { get; set; }
    }
}
