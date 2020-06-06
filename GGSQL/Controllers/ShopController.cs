using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using GGSQL.Models;
using Newtonsoft.Json;

namespace GGSQL.Controllers
{
    public class ShopController : BaseScript
    {
        private static MySqlDatabase m_database;
        public List<Outfit> Outfits { get; private set; } = new List<Outfit>();

        internal ShopController(MySqlDatabase db)
        {
            m_database = db;

            Tick += FirstTick;
        }


        public async Task FirstTick()
        {
            try
            {
                Outfits = await m_database.GetOutfits();

                Outfits.ForEach(outfit => Debug.WriteLine(JsonConvert.SerializeObject(outfit)));

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            } 
            finally
            { 
                Tick -= FirstTick;
            }
        }

        public async Task<bool> ActivateItem(int itemId, int user)
        {
            var query = @"INSERT INTO player_items 
                            (item_id, user_id, active, created_at) 
                            VALUES (@itemId, @userId, 1, @createdAt)";

            int success = await m_database.ExecuteAsync(query, new Dictionary<string, object>
            {
                { "itemId", itemId },
                { "userId", user },
                { "createdAt", DateTime.UtcNow }
            });

            if (success != 0) return true;

            return false;
        }

        public async Task<List<UserOutfit>> GetUserOutfits(int user)
        {
            var outfits = await m_database.GetUserOutfits(user);

            return outfits;
        }
    }
}
