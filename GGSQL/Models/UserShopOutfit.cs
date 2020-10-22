namespace GGSQL.Models
{
    public class UserShopOutfit
    {
        public int id { get; set; }
        public string slug { get; set; }
        public string title { get; set; }
        public string image { get; set; }
        public bool owned { get; set; }
        public bool donator { get; set; }
        public int price { get; set; }
        public string description { get; set; }
        public int xp { get; set; }
    }
}
