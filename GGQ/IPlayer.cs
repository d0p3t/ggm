using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GGQ
{
    public interface IPlayer
    {
        string LicenseId { get; set; }
        string SteamId { get; set; }
        string XblId { get; set; }
        string LiveId { get; set; }
        string DiscordId { get; set; }
        string FivemId { get; set; }
        string Name { get; set; }
    }
}
