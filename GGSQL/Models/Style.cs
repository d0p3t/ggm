using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GGSQL.Models.Styles;

namespace GGSQL.Models
{
    public abstract class Style
    {
        public int User_id { get; set; }
        public bool IsActiveStyle { get; set; }
    }
}
