using System.Collections.Generic;
using GGSQL.Models;

namespace GGSQL
{
    public static class Cache
    {
        public static List<User> Users = new List<User>();
        public static List<Connection> Connections = new List<Connection>();
        public static List<Outfit> Outfits = new List<Outfit>();
        public static List<GeneralItem> GeneralItems = new List<GeneralItem>();
    }
}
