using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using GGSQL.Models;
using GGSQL.Models.Styles;
using Newtonsoft.Json;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;

namespace GGSQL.Controllers
{
    public enum ShopItemType
    {
        OUTFIT,
        XPBOOST,
        CURRENCY,
        DONATION,
        WEAPONTINT
    }

    public class ShopController : BaseScript
    {
        private static MySqlDatabase m_database;
        private static ServerLogger m_logger;

        private ConcurrentDictionary<int, List<UserOutfit>> m_userOutfits = new ConcurrentDictionary<int, List<UserOutfit>>();
        private ConcurrentDictionary<int, List<UserGeneralItem>> m_userGeneralItems = new ConcurrentDictionary<int, List<UserGeneralItem>>();
        private ConcurrentDictionary<int, List<UserWeaponTint>> m_userWeaponTints = new ConcurrentDictionary<int, List<UserWeaponTint>>();

        private static readonly DateTime baseTime = new DateTime(1970, 1, 1);


        internal ShopController(MySqlDatabase db, ServerLogger logger)
        {
            m_database = db;
            m_logger = logger;

            Tick += FirstTick;

            EventHandlers["shop:buyOutfit"] += new Action<Player, int>(OnBuyOutfit);
            EventHandlers["shop-request-data"] += new Action<Player>(OnShopRequestData);
            EventHandlers["shop-request-buy-outfit"] += new Action<Player, int>(OnShopRequestBuyOutfit);
            EventHandlers["shop-request-equip-outfit"] += new Action<Player, int>(OnShopRequestEquipOutfit);
            EventHandlers["shop-request-buy-tint"] += new Action<Player, int>(OnShopRequestBuyTint);
            EventHandlers["shop-request-equip-tint"] += new Action<Player, int>(OnShopRequestEquipTint);



            RegisterCommand("shopcontrolleractivateonetimeitem", new Action<int, List<object>, string>(OnActivateOneTimeItem), true);
            RegisterCommand("shopcontrollerdeactivateonetimeitem", new Action<int, List<object>, string>(OnActivateOneTimeItem), true);

            // RegisterCommand("claim", new Action<int, List<object>, string>(OnClaimFreeOutfits), false);
            RegisterCommand("equip", new Action<int, List<object>, string>(OnEquipOutfit), false);
            RegisterCommand("outfits", new Action<int, List<object>, string>(OnListOutfits), false);

        }

        private async void OnShopRequestEquipOutfit([FromSource]Player player, int outfitId)
        {
            List<UserOutfit> userOutfits;
            m_userOutfits.TryGetValue(Convert.ToInt32(player.Handle), out userOutfits);

            if (userOutfits == null)
            {
                player.TriggerEvent("shop:prompt", "<b>You don't have any outfits!</b>");
                return;
            }

            var userOutfit = userOutfits.FirstOrDefault(uo => uo.OutfitId == outfitId);
            if(userOutfit == null)
            {
                player.TriggerEvent("shop:prompt", "You do not own this outfit!");
                return;
            }

            var outfit = Cache.Outfits.FirstOrDefault(o => o.Id == outfitId);
            if(outfit == null)
            {
                player.TriggerEvent("shop:prompt", "Outfit doesn't exist?!");
                return;
            }

            var clothingStyle = new ClothingStyle(outfitId)
            {
                PedComponents = outfit.Components
            };

            player.TriggerEvent("setActiveStyle", JsonConvert.SerializeObject(clothingStyle));

            var user = Cache.Users.FirstOrDefault(u => u.NetId == Convert.ToInt32(player.Handle));
            if (user != null)
            {
                user.ActiveUserOutfit = userOutfit.Id;
            }

            player.TriggerEvent("shop:prompt", "<b>Equipped Outfit!</b> Press ESC return to game.");

            await Delay(0);
        }

        private async void OnShopRequestEquipTint([FromSource]Player player, int tintId)
        {
            List<UserWeaponTint> userWeaponTints;
            m_userWeaponTints.TryGetValue(Convert.ToInt32(player.Handle), out userWeaponTints);

            if (userWeaponTints == null)
            {
                player.TriggerEvent("shop:prompt", "<b>You don't have any weapon tints!</b>");
                return;
            }

            var userWeaponTint = userWeaponTints.FirstOrDefault(uo => uo.WeaponTintId == tintId);
            if (userWeaponTint == null)
            {
                player.TriggerEvent("shop:prompt", "You do not own this weapon tint!");
                return;
            }

            var weaponTint = Cache.WeaponTints.FirstOrDefault(o => o.Id == tintId);
            if (weaponTint == null)
            {
                player.TriggerEvent("shop:prompt", "Weapon tint doesn't exist?!");
                return;
            }

            player.TriggerEvent("setWeaponTint", weaponTint.TintId, weaponTint.IsMk2);

            var user = Cache.Users.FirstOrDefault(u => u.NetId == Convert.ToInt32(player.Handle));
            if (user != null)
            {
                user.ActiveUserWeaponTint = userWeaponTint.Id;
            }

            player.TriggerEvent("shop:prompt", "<b>Equipped Weapon Tint!</b> Press ESC return to game.");

            await Delay(0);
        }

        private async void OnShopRequestBuyOutfit([FromSource]Player player, int itemId)
        {
            var user = Cache.Users.FirstOrDefault(u => u.NetId == Convert.ToInt32(player.Handle));

            if (user == null)
            {
                player.TriggerEvent("request-buy-outfit-result", false, itemId, "Could not find profile");
                return;
            }

            var outfit = Cache.Outfits.FirstOrDefault(o => o.Id == itemId);
            if(outfit == null)
            {
                return;
            }

            var result = await BuyItemForUser(user.Id, user.NetId, itemId, ShopItemType.OUTFIT, false);
            var success = false;

            if (result == "Success")
                success = true;

            player.TriggerEvent("request-buy-outfit-result", success, itemId, result);
        }

        private async void OnShopRequestBuyTint([FromSource]Player player, int itemId)
        {
            Debug.WriteLine("We are here");
            var user = Cache.Users.FirstOrDefault(u => u.NetId == Convert.ToInt32(player.Handle));

            if (user == null)
            {
                player.TriggerEvent("request-buy-outfit-result", false, itemId, "Could not find profile");
                return;
            }

            var tint = Cache.WeaponTints.FirstOrDefault(o => o.Id == itemId);
            if (tint == null)
            {
                Debug.WriteLine($"{itemId} doesn't exist");
                return;
            }

            Debug.WriteLine("Lets buy it!");
            var result = await BuyItemForUser(user.Id, user.NetId, itemId, ShopItemType.WEAPONTINT, false);
            var success = false;

            if (result == "Success")
                success = true;

            Debug.WriteLine("All good!");
            player.TriggerEvent("request-buy-tint-result", success, itemId, result);
        }

        private void OnShopRequestData([FromSource]Player player)
        {
            try
            {
                List<UserOutfit> userOutfits;
                m_userOutfits.TryGetValue(Convert.ToInt32(player.Handle), out userOutfits);

                if(userOutfits == null)
                {
                    return;
                }

                var outfits = Cache.Outfits.Where(o => o.Enabled);
                var shopOutfits = new List<UserShopOutfit>();

                foreach (var outfit in outfits)
                {
                    var shopOutfit = new UserShopOutfit
                    {
                        id = outfit.Id,
                        slug = outfit.Name,
                        title = outfit.Name,
                        owned = false,
                        price = outfit.Price,
                        xp = outfit.RequiredXp,
                        description = outfit.Description,
                        image = outfit.Image
                    };
                    shopOutfits.Add(shopOutfit);
                }

                foreach (var outfit in userOutfits)
                {
                    var toChange = shopOutfits.FirstOrDefault(so => so.id == outfit.OutfitId);
                    if(toChange != null)
                        toChange.owned = true;
                }

                player.TriggerEvent("update-outfits-data", JsonConvert.SerializeObject(shopOutfits));

                Debug.WriteLine("We are here now");
                List<UserWeaponTint> userWeaponTints;
                m_userWeaponTints.TryGetValue(Convert.ToInt32(player.Handle), out userWeaponTints);

                if(userWeaponTints == null)
                {
                    userWeaponTints = new List<UserWeaponTint>();
                }

                Debug.WriteLine("Alright so lets do the weapon tint stuff");
                var weaponTints = Cache.WeaponTints.Where(o => o.Enabled);
                var shopWeaponTints = new List<UserShopWeaponTint>();

                foreach (var tint in weaponTints)
                {
                    var shopTint = new UserShopWeaponTint
                    {
                        id = tint.Id,
                        slug = tint.Name,
                        title = tint.Name,
                        owned = false,
                        price = tint.Price,
                        xp = tint.RequiredXp,
                        description = tint.Description,
                        image = tint.Image
                    };
                    shopWeaponTints.Add(shopTint);
                }

                foreach (var tint in userWeaponTints)
                {
                    var toChange = shopWeaponTints.FirstOrDefault(so => so.id == tint.WeaponTintId);
                    if (toChange != null)
                        toChange.owned = true;
                }

                Debug.WriteLine($"Sending {shopWeaponTints.Count} tints");
                Debug.WriteLine($"We own {shopWeaponTints.Count(x => x.owned)}");
                player.TriggerEvent("update-weapontints-data", JsonConvert.SerializeObject(shopWeaponTints));

                var user = Cache.Users.FirstOrDefault(u => u.NetId == Convert.ToInt32(player.Handle));

                if (user == null)
                {
                    return;
                }

                var profile = new UserShopProfile
                {
                    money = user.Money,
                    xp = user.Xp
                };

                player.TriggerEvent("update-profile-data", JsonConvert.SerializeObject(profile));
            }
            catch (Exception ex)
            {
                m_logger.Exception("OnShopRequestData", ex);
            }
        }

        public async Task<bool> UpdateOutfits(int netId, bool checkCommerce)
        {
            Player player = Players[netId];

            if (player == null)
            {
                return false;
            }
            try
            {
                var user = Cache.Users.FirstOrDefault(u => u.NetId == netId);

                if (user == null)
                {
                    return false;
                }

                var qualifiedOutfits = Cache.Outfits.Where(o => o.Price == 0 && o.RequiredXp <= user.Xp).ToList();

                List<UserOutfit> userOutfits;
                m_userOutfits.TryGetValue(netId, out userOutfits);

                int newestOutfitId = 0;

                if (userOutfits == null)
                    userOutfits = new List<UserOutfit>();

                foreach (var qualifiedOutfit in qualifiedOutfits)
                {
                    if (userOutfits.Exists(uo => uo.OutfitId == qualifiedOutfit.Id))
                        continue;

                    if (qualifiedOutfit.DonatorExclusive && !user.Donator)
                        continue;

                    var result = await BuyItemForUser(user.Id, user.NetId, qualifiedOutfit.Id, ShopItemType.OUTFIT, checkCommerce);

                    if(result == "Success")
                        newestOutfitId = qualifiedOutfit.Id;

                    player.TriggerEvent("shop:buyItemResult", result);
                }

                if (newestOutfitId != 0)
                    player.TriggerEvent("shop:newestOutfit", newestOutfitId);

                return newestOutfitId != 0 ? true : false;
            }
            catch (Exception ex)
            {
                m_logger.Exception("UpdateOutfits", ex);
                player.TriggerEvent("shop:prompt", "Something went wrong updating outfits");
                return false;
            }
        }

        private async void OnClaimFreeOutfits(int source, List<object> args = null, string raw = "")
        {
            if (source == 0) return;

            Player player = Players[source];

            //if(player != null)
            //{
            //    player.TriggerEvent("shop:prompt", "Claiming free outfits...");
            //}

            bool newOutfits = await UpdateOutfits(source, false);

            if(newOutfits)
            {
                player.TriggerEvent("shop:prompt", "You've claimed free outfit(s). Use <b>/outfits</b> to show outfits");
            }
            else
            {
                player.TriggerEvent("shop:prompt", "No new free outfit(s). Use <b>/outfits</b> to show outfits");
            }
        }

        public async Task<UserOutfit> GetActiveUserOutfit(Player player, int activeUserOutfitId, int userId)
        {
            List<UserOutfit> userOutfits;
            m_userOutfits.TryGetValue(Convert.ToInt32(player.Handle), out userOutfits);

            if (userOutfits == null)
                userOutfits = new List<UserOutfit>();


            var userOutfit = userOutfits.FirstOrDefault(uo => uo.Id == activeUserOutfitId);

            if(userOutfit == null)
            {
                var userOutfitId = await m_database.GetActiveUserOutfit(userId);
                userOutfit = userOutfits.FirstOrDefault(uo => uo.Id == userOutfitId);
            }

            return userOutfit;
        }
        private async void OnEquipOutfit(int source, List<object> args, string raw)
        {
            var _source = source;
            if (_source == 0 || args.Count == 0)
            {
                return;
            }

            Player player = Players[_source];
            if (player == null)
            {
                return;
            }

            try
            {
                List<UserOutfit> userOutfits;
                m_userOutfits.TryGetValue(Convert.ToInt32(player.Handle), out userOutfits);

                if (userOutfits == null)
                {
                    player.TriggerEvent("shop:prompt", "<b>You don't have any outfits!</b>");
                    return;
                }

                int outfitNumber = -1;

                try
                {
                    outfitNumber = Convert.ToInt32(args[0]);
                }
                catch (Exception ex)
                {
                    player.TriggerEvent("shop:prompt", "You did not choose a number");
                    return;
                }

                if (outfitNumber < 0 || outfitNumber >= userOutfits.Count)
                {
                    player.TriggerEvent("shop:prompt", "<b>Invalid outfit!</b> Type <b>/outfits</b> to see a list of your outfits.");
                    return;
                }

                var userOutfit = userOutfits[outfitNumber];

                var outfit = Cache.Outfits.FirstOrDefault(o => o.Id == userOutfit.OutfitId);

                if (outfit == null)
                {
                    player.TriggerEvent("shop:prompt", "<b>Could not find outfit!</b>");
                    return;
                }

                var clothingStyle = new ClothingStyle(outfitNumber)
                {
                    PedComponents = outfit.Components
                };

                player.TriggerEvent("setActiveStyle", JsonConvert.SerializeObject(clothingStyle));

                var user = Cache.Users.FirstOrDefault(u => u.NetId == _source);
                if (user != null)
                {
                    user.ActiveUserOutfit = userOutfit.Id;
                }

                await Delay(0);
            }
            catch (Exception ex)
            {
                m_logger.Exception("OnEquipOutfit", ex);
                player.TriggerEvent("shop:prompt", "Something went wrong equipping an outfit");
            }
        }

        private void OnListOutfits(int source, List<object> args, string raw)
        {
            try
            {
                var _source = source;
                if (_source == 0) return;

                Player player = Players[_source];
                if (player == null)
                {
                    return;
                }

                try
                {
                    List<UserOutfit> userOutfits;
                    m_userOutfits.TryGetValue(Convert.ToInt32(player.Handle), out userOutfits);

                    if (userOutfits == null)
                    {
                        return;
                    }

                    string message = "\n^2[ ID ] Name (Use '/equip ID' to Equip)^7\n";
                    int count = 0;
                    foreach (var userOutfit in userOutfits)
                    {
                        message += $"^1[^7 {count} ^1]^7 {Cache.Outfits.First(o => o.Id == userOutfit.OutfitId).Name}\n";
                        count++;
                    }

                    player.TriggerEvent("chat:addMessage", new
                    {
                        color = new[] { 255, 0, 0 },
                        multiline = true,
                        args = new[] { "Outfits - /equip ID to Equip", message }
                    });
                }
                catch (Exception ex)
                {
                    m_logger.Exception("OnListOutfits", ex);
                    player.TriggerEvent("shop:prompt", "Something went wrong equipping an outfit");
                }
            }
            catch (Exception ex)
            {
                m_logger.Exception("OnListOutfits", ex);
            }
        }

        private void OnActivateOneTimeItem(int source, List<object> args, string raw)
        {
            if(source != 0)
            {
                return;
            }

            if(args.Count < 3)
            {
                return;
            }

            var playerNetId = args[0].ToString();
            var packageId = args[1];
            var packageName = args[2];


            Debug.WriteLine($"{GetPlayerName(playerNetId)} activated [{packageId}] {packageName}");
        }

        private void OnDeactivateOneTimeItem(int source, List<object> args, string raw)
        {
            if (source != 0)
            {
                return;
            }

            if (args.Count < 3)
            {
                return;
            }

            var playerNetId = args[0].ToString();
            var packageId = args[1];
            var packageName = args[2];


            Debug.WriteLine($"{GetPlayerName(playerNetId)} activated [{packageId}] {packageName}");
        }

        public async Task FirstTick()
        {
            try
            {
                var toInsertOutfit = new Outfit
                {
                    CreatedAt = DateTime.UtcNow,
                    Discount = 0.0f,
                    Enabled = false,
                    RequiredXp = 0,
                    Price = 14999,
                    DonatorExclusive = true,
                    Name = "Halo (Black)",
                    Components = new List<PedComponent>
                    {
                         new PedComponent(1 ,29, 0, 0),
                         new PedComponent(3, 33, 0, 0),
                         new PedComponent(4, 31, 0, 0),
                         new PedComponent(6, 24, 0, 0),
                         new PedComponent(7, 0, 0, 0),
                         new PedComponent(8, 55, 0, 0),
                         new PedComponent(9, 0, 0, 0),
                         new PedComponent(10, 0, 0, 0),
                         new PedComponent(11, 53, 0, 0),
                    },
                    Description = "Master Chief Black Edition",
                    TebexPackageId = 0
                };

                //var insertedOutfit = await m_database.InsertOutfit(toInsertOutfit);
                //Debug.WriteLine($"Inserted Outfit [{insertedOutfit.Name}] with ID [{insertedOutfit.Id}]");

                toInsertOutfit = new Outfit
                {
                    CreatedAt = DateTime.UtcNow,
                    Discount = 0.0f,
                    Enabled = false,
                    RequiredXp = 0,
                    Price = 29999,
                    Name = "Fighter (White)",
                    Components = new List<PedComponent>
                    {
                         new PedComponent(1 ,16, 6, 0),
                         new PedComponent(3, 44, 0, 0),
                         new PedComponent(4, 31, 2, 0),
                         new PedComponent(6, 25, 0, 0),
                         new PedComponent(7, 0, 0, 0),
                         new PedComponent(8, 56, 0, 0),
                         new PedComponent(9, 0, 0, 0),
                         new PedComponent(10, 0, 0, 0),
                         new PedComponent(11, 53, 0, 0),
                    },
                    Description = "Don't fight me",
                    TebexPackageId = 0
                };

                //insertedOutfit = await m_database.InsertOutfit(toInsertOutfit);
                //Debug.WriteLine($"Inserted Outfit [{insertedOutfit.Name}] with ID [{insertedOutfit.Id}]");

                toInsertOutfit = new Outfit
                {
                    CreatedAt = DateTime.UtcNow,
                    Discount = 0.0f,
                    Enabled = false,
                    RequiredXp = 0,
                    Price = 6449,
                    Name = "Vagos Masked",
                    Components = new List<PedComponent>
                    {
                         new PedComponent(1 ,50, 1, 0),
                         new PedComponent(3, 5, 0, 0),
                         new PedComponent(4, 7, 1, 0),
                         new PedComponent(6, 12, 0, 0),
                         new PedComponent(7, 17, 2, 0),
                         new PedComponent(8, 15, 0, 0),
                         new PedComponent(9, 0, 0, 0),
                         new PedComponent(10, 0, 0, 0),
                         new PedComponent(11, 5, 0, 0),
                    },
                    Description = "Don't fuck wid me putto",
                    TebexPackageId = 0
                };

                //insertedOutfit = await m_database.InsertOutfit(toInsertOutfit);
                //Debug.WriteLine($"Inserted Outfit [{insertedOutfit.Name}] with ID [{insertedOutfit.Id}]");

                toInsertOutfit = new Outfit
                {
                    CreatedAt = DateTime.UtcNow,
                    Discount = 0.0f,
                    Enabled = true,
                    RequiredXp = 11649,
                    Price = 1495,
                    Name = "The Mechanic (Red)",
                    Components = new List<PedComponent>
                    {
                         new PedComponent(1 ,0, 0, 0),
                         new PedComponent(3, 4, 0, 0),
                         new PedComponent(4, 38, 0, 0),
                         new PedComponent(6, 24, 0, 0),
                         new PedComponent(7, 0, 0, 0),
                         new PedComponent(8, 15, 0, 0),
                         new PedComponent(9, 0, 0, 0),
                         new PedComponent(10, 0, 0, 0),
                         new PedComponent(11, 65, 0, 0),
                    },
                    Description = "Look like you're gonna fix some enemies up",
                    TebexPackageId = 0
                };

                //insertedOutfit = await m_database.InsertOutfit(toInsertOutfit);
                //Debug.WriteLine($"Inserted Outfit [{insertedOutfit.Name}] with ID [{insertedOutfit.Id}]");

                toInsertOutfit = new Outfit
                {
                    CreatedAt = DateTime.UtcNow,
                    Discount = 0.0f,
                    Enabled = true,
                    RequiredXp = 11649,
                    Price = 595,
                    Name = "The Mechanic (Grey)",
                    Components = new List<PedComponent>
                    {
                         new PedComponent(1 ,0, 0, 0),
                         new PedComponent(3, 4, 0, 0),
                         new PedComponent(4, 38, 3, 0),
                         new PedComponent(6, 24, 0, 0),
                         new PedComponent(7, 0, 0, 0),
                         new PedComponent(8, 15, 0, 0),
                         new PedComponent(9, 0, 0, 0),
                         new PedComponent(10, 0, 0, 0),
                         new PedComponent(11, 65, 3, 0),
                    },
                    Description = "You're a simple man and you wanna fix stuff",
                    TebexPackageId = 0
                };

                //insertedOutfit = await m_database.InsertOutfit(toInsertOutfit);
                //Debug.WriteLine($"Inserted Outfit [{insertedOutfit.Name}] with ID [{insertedOutfit.Id}]");

                toInsertOutfit = new Outfit
                {
                    CreatedAt = DateTime.UtcNow,
                    Discount = 0.0f,
                    Enabled = true,
                    RequiredXp = 11649,
                    Price = 750,
                    Name = "The Mechanic (Black)",
                    Components = new List<PedComponent>
                    {
                         new PedComponent(1 ,0, 0, 0),
                         new PedComponent(3, 4, 0, 0),
                         new PedComponent(4, 39, 1, 0),
                         new PedComponent(6, 27, 0, 0),
                         new PedComponent(7, 0, 0, 0),
                         new PedComponent(8, 15, 0, 0),
                         new PedComponent(9, 0, 0, 0),
                         new PedComponent(10, 0, 0, 0),
                         new PedComponent(11, 66, 1, 0),
                    },
                    Description = "A good middleground to make sure you break enemies",
                    TebexPackageId = 0
                };

                var newWeaponTint = new WeaponTint
                {
                    CreatedAt = DateTime.UtcNow,
                    Discount = 0.0f,
                    Enabled = true,
                    RequiredXp = 0,
                    Price = 0,
                    Name = "Black (Default)",
                    TintId = 0,
                    Description = "Black Weapon Tint for Non-MK2 weapons.",
                    TebexPackageId = 0
                };

                //var insertedWeaponTint = await m_database.InsertWeaponTint(newWeaponTint);
                //Debug.WriteLine($"Inserted WeaponTint [{insertedWeaponTint.Name}] with ID [{insertedWeaponTint.Id}]");


                //insertedOutfit = await m_database.InsertOutfit(toInsertOutfit);
                //Debug.WriteLine($"Inserted Outfit [{insertedOutfit.Name}] with ID [{insertedOutfit.Id}]");


                Cache.Outfits = await m_database.GetOutfits();
                Cache.GeneralItems = await m_database.GetGeneralItems();
                Cache.WeaponTints = await m_database.GetWeaponTints();

                //Outfits.ForEach(outfit => Debug.WriteLine(JsonConvert.SerializeObject(outfit)));
                //GeneralItems.ForEach(item => Debug.WriteLine(JsonConvert.SerializeObject(item)));

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

        /// <summary>
        /// Buy an item. Currently only outfits supported (itemType 0)
        /// </summary>
        /// <param name="userId">Id of user in Database</param>
        /// <param name="itemId">Id of item</param>
        /// <param name="itemType">Type of item. Currently only 0 supported</param>
        /// <returns>Descriptive result as string</returns>
        public async Task<string> BuyItemForUser(int userId, int netId, int itemId, ShopItemType itemType, bool forceCommerceCheck)
        {
            int result;
            switch (itemType)
            {
                case ShopItemType.OUTFIT:
                    result = await m_database.BuyUserOutfit(userId, itemId);
                    break;
                case ShopItemType.XPBOOST:
                    result = await m_database.BuyUserBoost(userId, itemId);
                    break;
                case ShopItemType.CURRENCY:
                    // buy user in-game currency
                    result = 1;
                    break;
                case ShopItemType.DONATION:
                    // donation
                    result = 1;
                    break;
                case ShopItemType.WEAPONTINT:
                    result = await m_database.BuyUserWeaponTint(userId, itemId);
                    break;
                default:
                    result = -99;
                    break;

            }

            switch (result)
            {
                case 0:
                    return "You already own this item";
                case -1:
                    return "You don't have enough money";
                case -2:
                    return "You need more XP for this item";
                case -3:
                    return "Item is for donators only";
                case -99:
                    return "Something went wrong";
                default:
                    bool commerce = false;

                    if(forceCommerceCheck && CanPlayerStartCommerceSession(netId.ToString()))
                    {
                        commerce = true;
                    }

                    await UpdateItems(userId, netId, itemType, commerce);
                    return "Success";
            }
        }

        private async Task UpdateItems(int userId, int netId, ShopItemType itemType, bool commerce)
        {
            switch (itemType)
            {
                case ShopItemType.OUTFIT:
                    await GetUserOutfits(userId, netId, commerce);
                    break;
                case ShopItemType.XPBOOST:
                    break;
                case ShopItemType.CURRENCY:
                    break;
                case ShopItemType.DONATION:
                    break;
                case ShopItemType.WEAPONTINT:
                    await GetUserWeaponTints(userId, netId, commerce);
                    break;
                default:
                    break;
            }
        }

        public async Task GetUserGeneralItems(int userId, int netId, bool startedCommerceSession)
        {
            List<UserGeneralItem> generalItems = new List<UserGeneralItem>();
            try
            {
                generalItems = await m_database.GetUserGeneralItems(userId);

                var player = Players[netId];
                var playerSrc = netId.ToString();

                if (startedCommerceSession)
                {
                    List<UserGeneralItem> webshopGeneralItems = new List<UserGeneralItem>();
                    int deactivatedGeneralItems = 0;
                    foreach (var generalItem in Cache.GeneralItems)
                    {
                        if (generalItem.TebexPackageId != 0)
                        {
                            bool existsInData = generalItems.Exists(o => o.ItemId == generalItem.Id);
                            bool owned = DoesPlayerOwnSkuExt(playerSrc, generalItem.TebexPackageId);
                            if (owned)
                            {
                                if (!existsInData)
                                {
                                    var userOutfit = new UserGeneralItem
                                    {
                                        ItemId = generalItem.Id,
                                        UserId = userId,
                                        CreatedAt = DateTime.UtcNow,
                                        ExpirationDate = DateTime.UtcNow + generalItem.Duration,
                                        IsOneTimeActivation = generalItem.Type == (int)ShopItemType.XPBOOST ? false : true
                                    };
                                    userOutfit = await m_database.InsertUserGeneralItem(userOutfit);
                                    webshopGeneralItems.Add(userOutfit);
                                }
                            }
                            else
                            {
                                if (existsInData)
                                {
                                    var deletedItems = await m_database.DeleteUserGeneralItem(generalItem.Id, userId);
                                    deactivatedGeneralItems += deletedItems;
                                }
                            }
                        }
                    }

                    m_logger.Info($"Player {playerSrc} has {webshopGeneralItems.Count} NEW owned General Item Tebex packages");

                    generalItems.AddRange(webshopGeneralItems);

                    if (player != null && (webshopGeneralItems.Count != 0 | deactivatedGeneralItems != 0))
                    {
                        player.TriggerEvent("shop:webshop_outfits_count_update", webshopGeneralItems.Count, deactivatedGeneralItems);
                    }
                }
                else
                {
                    if (player != null)
                    {
                        player.TriggerEvent("shop:webshop_error_loading");
                    }
                }

                m_userGeneralItems.AddOrUpdate(userId, generalItems, (key, oldValue) => oldValue = generalItems);
            }
            catch (Exception ex)
            {
                m_logger.Exception("GetUserOutfits", ex);
            }
            finally
            {
                List<int> ownedGeneralItemIds = new List<int>();
                foreach (var item in generalItems)
                {
                    ownedGeneralItemIds.Add(item.ItemId);
                }

                Players[netId].TriggerEvent("shop:owneditems", JsonConvert.SerializeObject(ownedGeneralItemIds));
            }
        }

        public async Task GetUserOutfits(int userId, int netId, bool startedCommerceSession)
        {
            List<UserOutfit> outfits = new List<UserOutfit>();
            var player = Players[netId];

            if(player == null)
            {
                return;
            }

            try
            {
                outfits = await m_database.GetUserOutfits(userId);

                var playerSrc = netId.ToString();

                // No outfits so we need to add the default one
                if(outfits.Count == 0)
                {
                    var defaultOutfit = Cache.Outfits.FirstOrDefault(o => o.Name == "Default (Grey)");
                    if(defaultOutfit != null)
                    {
                        await BuyItemForUser(userId, netId, defaultOutfit.Id, ShopItemType.OUTFIT, false);
                        outfits = await m_database.GetUserOutfits(userId);
                    }
                }
                
                if (startedCommerceSession)
                {
                    List<UserOutfit> webshopOutfits = new List<UserOutfit>();
                    int deactivatedOutfits = 0;
                    foreach (var outfit in Cache.Outfits)
                    {
                        if(outfit.TebexPackageId != 0)
                        {
                            bool existsInData = outfits.Exists(o => o.OutfitId == outfit.Id);
                            bool owned = DoesPlayerOwnSkuExt(playerSrc, outfit.TebexPackageId);
                            if (owned)
                            {
                                if (!existsInData)
                                {
                                    var userOutfit = new UserOutfit
                                    {
                                        OutfitId = outfit.Id,
                                        UserId = userId,
                                        CreatedAt = DateTime.UtcNow
                                    };
                                    userOutfit = await m_database.InsertUserOutfit(userOutfit);
                                    webshopOutfits.Add(userOutfit);
                                }
                            }
                            else
                            {
                                if(existsInData)
                                {
                                    var deletedItems = await m_database.DeleteUserOutfit(outfit.Id, userId);
                                    deactivatedOutfits += deletedItems;
                                }
                            }
                        }
                    }

                    outfits.AddRange(webshopOutfits);

                    if(player != null && (webshopOutfits.Count != 0 | deactivatedOutfits != 0))
                    {
                        player.TriggerEvent("shop:webshopoutfits_count_update", webshopOutfits.Count, deactivatedOutfits);
                    }
                }

                m_userOutfits.AddOrUpdate(netId, outfits, (key, oldValue) => oldValue = outfits);
            }
            catch (Exception ex)
            {
                m_logger.Exception("GetUserOutfits", ex);
                player.TriggerEvent("shop:webshop_error_loading");
            }
            finally
            {
                m_userOutfits.TryGetValue(netId, out outfits);

                List<int> ownedOutfitIds = new List<int>();
                foreach (var outfit in outfits)
                {
                    ownedOutfitIds.Add(outfit.OutfitId);
                }

                player.TriggerEvent("shop:ownedoutfits", JsonConvert.SerializeObject(ownedOutfitIds));
            }
        }

        public async Task GetUserWeaponTints(int userId, int netId, bool startedCommerceSession)
        {
            List<UserWeaponTint> tints = new List<UserWeaponTint>();
            var player = Players[netId];

            if (player == null)
            {
                return;
            }

            try
            {
                tints = await m_database.GetUserWeaponTints(userId);

                var playerSrc = netId.ToString();

                // No outfits so we need to add the default one
                if (tints.Count == 0)
                {
                    var defaultTint = Cache.Outfits.FirstOrDefault(o => o.Name == "Black (Default)");
                    if (defaultTint != null)
                    {
                        await BuyItemForUser(userId, netId, defaultTint.Id, ShopItemType.WEAPONTINT, false);
                        tints = await m_database.GetUserWeaponTints(userId);
                    }
                }

                if (startedCommerceSession)
                {
                    List<UserWeaponTint> webshopTints = new List<UserWeaponTint>();
                    int deactivatedTints = 0;
                    foreach (var tint in Cache.WeaponTints)
                    {
                        if (tint.TebexPackageId != 0)
                        {
                            bool existsInData = tints.Exists(o => o.WeaponTintId == tint.Id);
                            bool owned = DoesPlayerOwnSkuExt(playerSrc, tint.TebexPackageId);
                            if (owned)
                            {
                                if (!existsInData)
                                {
                                    var userWeaponTint = new UserWeaponTint
                                    {
                                        WeaponTintId = tint.Id,
                                        UserId = userId,
                                        CreatedAt = DateTime.UtcNow
                                    };
                                    userWeaponTint = await m_database.InsertUserWeaponTint(userWeaponTint);
                                    webshopTints.Add(userWeaponTint);
                                }
                            }
                            else
                            {
                                //if (existsInData)
                                //{
                                //    var deletedItems = await m_database.DeleteUserOutfit(outfit.Id, userId);
                                //    deactivatedOutfits += deletedItems;
                                //}
                            }
                        }
                    }

                    tints.AddRange(webshopTints);

                    if (player != null && (webshopTints.Count != 0 | deactivatedTints != 0))
                    {
                        player.TriggerEvent("shop:webshopoutfits_count_update", webshopTints.Count, deactivatedTints);
                    }
                }

                m_userWeaponTints.AddOrUpdate(netId, tints, (key, oldValue) => oldValue = tints);
            }
            catch (Exception ex)
            {
                m_logger.Exception("GetUserOutfits", ex);
                player.TriggerEvent("shop:webshop_error_loading");
            }
            finally
            {
                m_userWeaponTints.TryGetValue(netId, out tints);

                List<int> ownedTintIds = new List<int>();
                foreach (var tint in tints)
                {
                    ownedTintIds.Add(tint.WeaponTintId);
                }

                player.TriggerEvent("shop:ownedTints", JsonConvert.SerializeObject(ownedTintIds));
            }
        }

        public async Task<bool> RemoveUserOutfitsFromCache(int netId)
        {
            try
            {
                List<UserOutfit> outfits = new List<UserOutfit>();
                var removed = m_userOutfits.TryRemove(netId, out outfits);

                await m_database.UpdateUserOutfits(outfits);
                return removed;
            }
            catch (Exception ex)
            {
                m_logger.Exception(nameof(RemoveUserOutfitsFromCache), ex);
                return false;
            }
        }

        public void ActivateGeneralItems(int userId, int netId)
        {
            List<UserGeneralItem> items = new List<UserGeneralItem>();

            m_userGeneralItems.TryGetValue(userId, out items);

            if(items.Count > 0)
            {
                var itemsToActivate = items.Where(item => item.ExpirationDate > DateTime.UtcNow).ToList();

                foreach (var item in itemsToActivate)
                {
                    var generalItem = Cache.GeneralItems.FirstOrDefault(gi => gi.Id == item.ItemId);

                    Action<DateTime, int, int> onActivate = GetActivationAction((ShopItemType)generalItem.Type);

                    if(onActivate != null)
                    {
                        onActivate.Invoke(DateTime.UtcNow.Add(generalItem.Duration), netId, generalItem.Price);
                    }
                }
            }
        }

        private Action<DateTime, int, int> GetActivationAction(ShopItemType itemType)
        {
            Action<DateTime, int, int> onActivate = null;

            switch (itemType)
            {
                case ShopItemType.XPBOOST:
                    onActivate = (DateTime until, int netId, int price) => ActivateUserXpBoost(until, netId, price);
                    break;
                case ShopItemType.DONATION:
                    onActivate = (DateTime until, int netId, int price) => ActivateUserDonation(until, netId, price);
                    break;
                case ShopItemType.CURRENCY:
                    onActivate = (DateTime until, int netId, int price) => ActivateUserCurrency(until, netId, price);
                    break;
                default:
                    break;
            }

            return onActivate;
        }

        private void ActivateUserXpBoost(DateTime until, int netId, int price)
        {
            try
            {
                if (until > DateTime.UtcNow) // Extra check
                {
                    TriggerEvent("shop:activate", netId, ShopItemType.XPBOOST, GetEpochTime(until), price);
                }
            }
            catch (Exception ex)
            {
                m_logger.Exception("UserBoost", ex);
            }
        }

        private void ActivateUserDonation(DateTime until, int netId, int price)
        {
            TriggerEvent("shop:activate", netId, ShopItemType.DONATION, GetEpochTime(until), price);
        }

        private void ActivateUserCurrency(DateTime until, int netId, int price)
        {
            TriggerEvent("shop:activate", netId, ShopItemType.CURRENCY, GetEpochTime(until), price);
        }

        private double GetEpochTime(DateTime start)
        {
            return (start - baseTime).TotalMilliseconds;
        }

        private async void OnBuyOutfit([FromSource]Player player, int outfitId)
        {
            try
            {
                var licenseId = player.Identifiers["license"];
                var user = Cache.Users.FirstOrDefault(x => x.LicenseId == licenseId);

                if (user != null)
                {
                    var result = await BuyItemForUser(user.Id, Convert.ToInt32(player.Handle), outfitId, ShopItemType.OUTFIT, false);
                    player.TriggerEvent("shop:prompt", result);
                }
                else
                {
                    m_logger.Error($"[OnBuyOutfit] Could not find user {licenseId} in cache");
                }
            }
            catch (Exception ex)
            {
                m_logger.Exception("OnBuyOutfit", ex);
            }
        }
    }
}
