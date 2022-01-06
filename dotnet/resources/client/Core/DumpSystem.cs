using GTANetworkAPI;
using System.Collections.Generic;
using System;
using Golemo.GUI;
using Golemo.Core;
using GolemoSDK;
using Golemo.Jobs;
using System.Linq;
using System.Threading;

namespace Golemo
{
    class DumpSystem : Script
    {
        private static nLog Log = new nLog("DumpSystem");
        //Starting price on an auction (it is then *10)
        private static int black_metalPrice = 10;
        private static int nonferrous_metalPrice = 15;
        private static int old_computerPrice = 20;
        private static int silver_nuggetPrice = 30;
        private static int gold_coinPrice = 50;

        //The multiplier in pawn shop
        private static int itemMultiplier = 10;
        private static int maxMultiplier = 20;
        private static int minMultiplier = 5;
        /*private static int black_metalPricePawn = 170;
        private static int nonferrous_metalPricePawn = 275;
        private static int old_computerPricePawn = 430;
        private static int silver_nuggetPricePawn = 630;
        private static int gold_coinPricePawn = 1023;*/

        private static int checkpoints_area = 41; //amount of items per area

        private static int size = 40; //area size
        private static Vector3 zone = new Vector3(1984.712, 3397.3235, 42.14608);
        //Rates
        private static int black_metal = 30;
        private static int nonferrous_metal = 30;
        private static int old_computer = 20; 
        private static int silver_nugget = 13;
        private static int gold_coin = 7;

        public static List<ColShape> Items = new List<ColShape>();
        private static TextLabel Pawn_label;

        [ServerEvent(Event.ResourceStart)]
        public static void Event_ResourceStart()
        {
            try
            {
                NAPI.Blip.CreateBlip(632, new Vector3(1984.712, 3397.3235, 42.14608), 1, 21, Main.StringToU16("Свалка"), 255, 0, true, 0, 0); // Blip

                NAPI.Blip.CreateBlip(357, new Vector3(2342.3704, 3146.532, 47.088707), 1, 44, Main.StringToU16("Ломбард"), 255, 0, true, 0, 0);
                Pawn_label = NAPI.TextLabel.CreateTextLabel($"~w~Откройте инвентарь и нажмите 'использовать' \nна предмет, который хотите продать \nКоэффициент: {itemMultiplier}", new Vector3(2342.3704, 3146.532, 48.888707), 30f, 0.3f, 0, new Color(255, 255, 255), true, NAPI.GlobalDimension); // On npc head
                NAPI.Marker.CreateMarker(1, new Vector3(2342.8872, 3145.6184, 47.288707) - new Vector3(0, 0, 0.7), new Vector3(), new Vector3(), 1, new Color(255, 255, 255, 220));
                var pawn = NAPI.ColShape.CreateCylinderColShape(new Vector3(2342.8872, 3145.6184, 47.088707), 4, 2, 0); // pawnshop
                pawn.OnEntityEnterColShape += (shape, player) => {
                    try
                    {
                        player.SetData("InPawnShop_dump", true);
                    }
                    catch (Exception ex) { Log.Write("col.OnEntityEnterColShape: " + ex.Message, nLog.Type.Error); }
                };
                pawn.OnEntityExitColShape += (shape, player) => {
                    try
                    {
                        player.SetData("InPawnShop_dump", false);
                    }
                    catch (Exception ex) { Log.Write("col.OnEntityExitColShape: " + ex.Message, nLog.Type.Error); }
                };
                UpdateMultiplier();
            }
            catch (Exception e) { Log.Write("ResourceStart: " + e.Message, nLog.Type.Error); }
        }
        public static void createItems()
        {
            try
            {
                NAPI.Chat.SendChatMessageToAll("!{#fc4626} [Свалка]: !{#ffffff}" + "Была доставлена новая партия мусора.");
                foreach (var col in Items.ToList()) //Удаляем предыдущие колшейпы
                {
                    TextLabel label = col.GetData<TextLabel>("lable_auction");
                    Marker marker = col.GetData<Marker>("marker_auction");
                    if (label != null)
                    {
                        NAPI.Entity.DeleteEntity(label);
                    }
                    if (marker != null)
                    {
                        NAPI.Entity.DeleteEntity(marker);
                    }
                    Player player = col.GetData<Player>("player_auction");
                    if (player != null)
                    {
                        Timers.Stop(NAPI.Data.GetEntityData(player, "DUMP_EXIT_TIMER"));
                        NAPI.Data.ResetEntityData(player, "DUMP_EXIT_TIMER");
                        player.SetData("InAuction", false);
                        player.SetData("OnItem", false);
                        player.SetData("OnCheckpoint", false);
                        int Price = col.GetData<int>("CurrentPrice");
                        MoneySystem.Wallet.Change(player, Price);
                    }
                    Items.Remove(col);
                    col.Delete();
                }
                for (int a = 0; a < checkpoints_area; a++)
                {
                    int x = Jobs.WorkManager.rnd.Next(-size, size);
                    int y = Jobs.WorkManager.rnd.Next(-size, size);
                    Vector3 base_location = new Vector3(zone.X + x, zone.Y + y, 39.1);
                    var item = NAPI.ColShape.CreateCylinderColShape(base_location, 5, 10, 0);
                    item.OnEntityEnterColShape += PlayerEnterCheckpoint_item;
                    item.OnEntityExitColShape += PlayerExitCheckpoint_item;
                    item.SetData("Vector3_Item", base_location);
                    Items.Add(item);
                }
            }
            catch (Exception e) { Log.Write("DumpCreate: " + e.Message, nLog.Type.Error); }
        }
        #region Player enters checkpoints
        private static void PlayerEnterCheckpoint_item(ColShape shape, Player player) //If item is discovered
        {
            if (player.IsInVehicle) return;
            player.SetData("ItemShape", shape);
            player.SetData("OnCheckpoint", true);
        }
        [RemoteEvent("Eclick_dump")]
        public static void Eclick(Player player)
        {
            if (player.GetData<bool>("OnCheckpoint"))
            {
                ColShape shape = player.GetData<ColShape>("ItemShape");
                if (!shape.Exists) return;
                if (shape.GetData<bool>("State_auction"))
                {
                    string Name = shape.GetData<string>("NameItem");
                    Trigger.ClientEvent(player, "openInput", "Аукцион", $"Лот: {Name}", 5, "DumpAuction");
                }
                else
                {
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Похоже, вы что-то нашли. Используйте лопату чтобы откапать.", 3000);
                    player.SetData("OnItem", true);
                }
            }
        }
        #endregion
        #region player exit checkpoints
        private static void PlayerExitCheckpoint_item(ColShape shape, Player player) //If player leaves item checkpoint
        {
            try
            {
                if (player.IsInVehicle) return;
                player.SetData("OnItem", false);
                player.SetData("OnCheckpoint", false);
                if (shape.GetData<Player>("player_auction") == player)
                {
                    Timers.Stop(NAPI.Data.GetEntityData(player, "DUMP_EXIT_TIMER"));
                    NAPI.Data.ResetEntityData(player, "DUMP_EXIT_TIMER");
                    player.SetData("InAuction", false);
                    TextLabel label = shape.GetData<TextLabel>("lable_auction");
                    string Name = shape.GetData<string>("NameItem");
                    int startPrice = shape.GetData<int>("StartPrice");
                    label.Text = $"Лот: ~y~{Name} \n~w~Текущая цена: ~r~НЕТ \n~w~Начальная цена: ~g~{startPrice}$";
                    int Price = shape.GetData<int>("CurrentPrice");
                    MoneySystem.Wallet.Change(player, Price);
                    shape.SetData("CurrentPrice", startPrice);
                    shape.SetData("auction_timer", false);
                    NAPI.Data.ResetEntityData(shape, "player_auction");
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "Вы покинули аукцион, он был аннулирован.", 3000);
                }
            }
            catch (Exception e)
            {
                Log.Write("PlayerExitCheckpoint_dump: \n" + e.ToString(), nLog.Type.Error);
            }
        }
        #endregion
        #region Item used
        public static void Shovel(Player player)
        {
            if (player.IsInVehicle) return;
            if (player.Position.X < zone.X + size && player.Position.X > zone.X - size && player.Position.Y < zone.Y + size && player.Position.Y > zone.X - size) //Check if player is in area
            {
                if (player.GetData<bool>("OnItem") != false)
                {
                    Main.OnAntiAnim(player);
                    BasicSync.AttachObjectToPlayer(player, NAPI.Util.GetHashKey("prop_cs_trowel"), 4138, new Vector3(0, 0, 0), new Vector3(0, 0, 0) );
                    player.PlayAnimation("anim@mp_snowball", "pickup_snowball", 47);
                    int limit = black_metal + nonferrous_metal + old_computer + silver_nugget + gold_coin;
                    int number = Jobs.WorkManager.rnd.Next(0, limit);
                    NAPI.Task.Run(() =>
                    {
                        try
                        {
                            player.StopAnimation();
                            BasicSync.DetachObject(player);
                            ColShape shape = player.GetData<ColShape>("ItemShape");
                            int startPrice = 0;
                            string Name = "";
                            if (number > black_metal && number < nonferrous_metal + black_metal + 1) //nonferrous_metal
                            {
                                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы нашли цветмет.", 3000);
                                startPrice = nonferrous_metalPrice*10;
                                Name = "Цветмет";
                            }
                            else if (number > nonferrous_metal + black_metal && number < old_computer + black_metal + nonferrous_metal + 1) //old_computer
                            {
                                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы нашли старый компьютер.", 3000);
                                startPrice = old_computerPrice*10;
                                Name = "Старый компьютер";
                            }
                            else if (number > old_computer + black_metal + nonferrous_metal && number < silver_nugget + old_computer + black_metal + nonferrous_metal + 1) //silver_nugget
                            {
                                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы нашли серебрянный самородок.", 3000);
                                startPrice = silver_nuggetPrice*10;
                                Name = "Серебрянный самородок";
                            }
                            else if (number > silver_nugget + old_computer + black_metal + nonferrous_metal) //gold_coin
                            {
                                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы нашли золотую монету.", 3000);
                                startPrice = gold_coinPrice*10;
                                Name = "Золотая монета";
                            }
                            else //black_metal
                            {
                                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы нашли черный металл.", 3000);
                                startPrice = black_metalPrice*10;
                                Name = "Черный металл";
                            }
                            shape.SetData("CurrentPrice", startPrice);
                            shape.SetData("StartPrice", startPrice);
                            shape.SetData("NameItem", Name);
                            shape.SetData("State_auction", true);
                            var pos = player.Position;
                            var marker = NAPI.Marker.CreateMarker(1, pos - new Vector3(0, 0, 1.2), new Vector3(), new Vector3(), 1, new Color(255, 179, 0, 220));
                            TextLabel label = NAPI.TextLabel.CreateTextLabel($"Лот: ~y~{Name} \n~w~Текущая цена: ~r~НЕТ \n~w~Начальная цена: ~g~{startPrice}$", pos + new Vector3(0, 0, 1), 10f, 0.2f, 0, new Color(255, 255, 255), true, 0);
                            shape.SetData("auction_timer", false);
                            shape.SetData("lable_auction", label);
                            shape.SetData("marker_auction", marker);
                        }
                        catch { }
                    }, 1300);
                }
                else
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Вы не нашли предмет, чтобы начать копать.", 3000);
                }
            }
            else
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Вы не можете здесь копать.", 3000);
            }
        }
        #endregion
        #region Auction
        public static void create_bill(Player player, int Price)
        {
            ColShape shape = player.GetData<ColShape>("ItemShape");
            if (shape == null) return;
            /*if (player.GetData<bool>("OnItem") == false)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Вы должны находиться на аукционе.", 3000);
                return;
            }*/
            if (player.GetData<bool>("InAuction"))
            {
                if (shape.GetData<Player>("player_auction") == player)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Вы уже участвуете в этом аукционе.", 3000);
                    return;
                }
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Вы уже участвуете в другом аукционе.", 3000);
                return;
            }
            else if (Main.Players[player].Money < Price)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"У вас недостаточно средств для участия в этом аукционе.", 3000);
                return;
            }
            else if (Price <= shape.GetData<int>("CurrentPrice"))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Ваша ставка должна быть больше, чем та, что сейчас.", 3000);
                return;
            }
            if (shape.GetData<Player>("player_auction") != null) //предыдущая ставка
            {
                Player old = shape.GetData<Player>("player_auction");
                int money = shape.GetData<int>("CurrentPrice");
                MoneySystem.Wallet.Change(old, money);
                player.SetData("InAuction", false);
                if ((NAPI.Data.GetEntityData(old, "DUMP_EXIT_TIMER") != null))
                {
                    Timers.Stop(NAPI.Data.GetEntityData(old, "DUMP_EXIT_TIMER"));
                    NAPI.Data.ResetEntityData(old, "DUMP_EXIT_TIMER");
                }
                NAPI.Data.ResetEntityData(shape, "player_auction");
                Notify.Send(old, NotifyType.Info, NotifyPosition.BottomCenter, $"Вашу ставку перебили. Вам были возвращены средства в размере {money}$", 3000);
            }
            MoneySystem.Wallet.Change(player, -Price);
            shape.SetData("CurrentPrice", Price);
            string Name = shape.GetData<string>("NameItem");
            int startPrice = shape.GetData<int>("StartPrice");
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы сделали ставку в размере {Price}$ на лот '{Name}'.", 3000);
            shape.SetData("auction_timer", false);
            player.SetData("InAuction", true);
            shape.SetData("player_auction", player);
            player.SetData("DUMP_TIMER_COUNT", 0);
            player.SetData("DUMP_EXIT_TIMER", Timers.Start(1000, () => auction_timer(player, shape, Name, Price, startPrice)));
        }
        public static void auction_timer(Player player, ColShape shape, string Name, int Price, int startPrice)
        {
            TextLabel label = shape.GetData<TextLabel>("lable_auction");
            Marker marker = shape.GetData<Marker>("marker_auction");
            NAPI.Task.Run(() =>
            {
                try
                {
                    if (!player.HasData("DUMP_EXIT_TIMER")) return;
                    if (shape.GetData<Player>("player_auction") != player) return;
                    if (shape.GetData<int>("CurrentPrice") != Price) return;
                    label.Text = $"Лот: ~y~{Name} \n~w~Текущая цена: ~g~{Price}$ \n~w~Начальная цена: ~g~{startPrice}$ \n~w~Осталось: ~g~{60 - NAPI.Data.GetEntityData(player, "DUMP_TIMER_COUNT")} секунд. \n~w~Лидирующая ставка: ~y~{player.Name}";
                    shape.SetData("lable_auction", label);
                    if (NAPI.Data.GetEntityData(player, "DUMP_TIMER_COUNT") > 59)
                    {
                        //when auction finished
                        NAPI.Entity.DeleteEntity(label);
                        NAPI.Entity.DeleteEntity(marker);

                        Timers.Stop(NAPI.Data.GetEntityData(player, "DUMP_EXIT_TIMER"));
                        NAPI.Data.ResetEntityData(player, "DUMP_EXIT_TIMER");
                        player.SetData("InAuction", false);
                        player.SetData("OnItem", false);
                        player.SetData("OnCheckpoint", false);
                        Items.Remove(shape);
                        shape.Delete();
                        switch (Name) //Give player item
                        {
                            case "Цветмет":
                                nInventory.Add(player, new nItem(ItemType.NonferrousMetal, 1));
                                break;
                            case "Старый компьютер":
                                nInventory.Add(player, new nItem(ItemType.OldComputer, 1));
                                break;
                            case "Серебрянный самородок":
                                nInventory.Add(player, new nItem(ItemType.SilverNugget, 1));
                                break;
                            case "Золотая монета":
                                nInventory.Add(player, new nItem(ItemType.GoldCoin, 1));
                                break;
                            case "Черный металл":
                                nInventory.Add(player, new nItem(ItemType.BlackMetal, 1));
                                break;
                        }
                        Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы забрали {Name} за {Price}$", 3000);
                        return;
                    }
                    NAPI.Data.SetEntityData(player, "DUMP_TIMER_COUNT", NAPI.Data.GetEntityData(player, "DUMP_TIMER_COUNT") + 1);

                }
                catch (Exception e)
                {
                    Log.Write("Timer_DumpAuction: \n" + e.ToString(), nLog.Type.Error);
                }
            });
        }
        #endregion
        #region Pawn shop
        public static void UpdateMultiplier()
        {
            itemMultiplier = WorkManager.rnd.Next(minMultiplier, maxMultiplier);
            Log.Write($"[Ломбард] Обновлен коэффициент на: {itemMultiplier} ({minMultiplier}-{maxMultiplier})");
            Pawn_label.Text = $"~w~Откройте инвентарь и нажмите 'использовать' \nна предмет, который хотите продать \nКоэффициент: {itemMultiplier} ({minMultiplier}-{maxMultiplier})";

        }
        public static void ItemUse(Player player, ItemType item)
        {
            if (player.IsInVehicle) return;
            if (!player.GetData<bool>("InPawnShop_dump"))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Вы не находитесь в месте для продажи этой вещи.", 3000);
                return;
            }
            nInventory.Remove(player, item, 1);
            switch (item)
            {
                case ItemType.BlackMetal:
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы продали 1 кг черного металла за {black_metalPrice * itemMultiplier}$", 3000);
                    MoneySystem.Wallet.Change(player, black_metalPrice*itemMultiplier);
                    return;
                case ItemType.NonferrousMetal:
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы продали 1 кг цветмета за {nonferrous_metalPrice * itemMultiplier}$.", 3000);
                    MoneySystem.Wallet.Change(player, nonferrous_metalPrice * itemMultiplier);
                    return;
                case ItemType.OldComputer:
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы продали 1 старый компьютер за {old_computerPrice * itemMultiplier}$.", 3000);
                    MoneySystem.Wallet.Change(player, old_computerPrice * itemMultiplier);
                    return;
                case ItemType.SilverNugget:
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы продали серебрянный самородок за {silver_nuggetPrice * itemMultiplier}$.", 3000);
                    MoneySystem.Wallet.Change(player, silver_nuggetPrice * itemMultiplier);
                    return;
                case ItemType.GoldCoin:
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы продали золотую монету за {gold_coinPrice * itemMultiplier}$.", 3000);
                    MoneySystem.Wallet.Change(player, gold_coinPrice * itemMultiplier);
                    return;
            }
        }
        #endregion
        #region If dead
        public static void Event_PlayerDeath(Player player, Player entityKiller, uint weapon)
        {
            try
            {
                if (!Main.Players.ContainsKey(player)) return;
                player.SetData("InAuction", false);
                player.SetData("OnItem", false);
                player.SetData("OnCheckpoint", false);
                ColShape shape = player.GetData<ColShape>("ItemShape");
                if (shape.GetData<Player>("player_auction") == player)
                {
                    if(NAPI.Data.GetEntityData(player, "DUMP_EXIT_TIMER"))
                    {
                        Timers.Stop(NAPI.Data.GetEntityData(player, "DUMP_EXIT_TIMER"));
                        NAPI.Data.ResetEntityData(player, "DUMP_EXIT_TIMER");
                    }
                    player.SetData("InAuction", false);
                    TextLabel label = shape.GetData<TextLabel>("lable_auction");
                    string Name = shape.GetData<string>("NameItem");
                    int startPrice = shape.GetData<int>("StartPrice");
                    label.Text = $"Лот: ~y~{Name} \n~w~Текущая цена: ~r~НЕТ \n~w~Начальная цена: ~g~{startPrice}$";
                    int Price = shape.GetData<int>("CurrentPrice");
                    MoneySystem.Wallet.Change(player, Price);
                    shape.SetData("CurrentPrice", startPrice);
                    shape.SetData("auction_timer", false);
                    NAPI.Data.ResetEntityData(shape, "player_auction");
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "Вы покинули аукцион, он был аннулирован.", 3000);
                }
            }
            catch (Exception e) { Log.Write("PlayerDeath: " + e.Message, nLog.Type.Error); }
        }
        #endregion
        #region If disconnected
        public static void Event_PlayerDisconnected(Player player, DisconnectionType type, string reason)
        {
            try
            {
                if (!Main.Players.ContainsKey(player)) return;
                player.SetData("InAuction", false);
                player.SetData("OnItem", false);
                player.SetData("OnCheckpoint", false);
                ColShape shape = player.GetData<ColShape>("ItemShape");
                if (shape.GetData<Player>("player_auction") == player)
                {
                    Timers.Stop(NAPI.Data.GetEntityData(player, "DUMP_EXIT_TIMER"));
                    NAPI.Data.ResetEntityData(player, "DUMP_EXIT_TIMER");
                    player.SetData("InAuction", false);
                    TextLabel label = shape.GetData<TextLabel>("lable_auction");
                    string Name = shape.GetData<string>("NameItem");
                    int startPrice = shape.GetData<int>("StartPrice");
                    label.Text = $"Лот: ~y~{Name} \n~w~Текущая цена: ~r~НЕТ \n~w~Начальная цена: ~g~{startPrice}$";
                    int Price = shape.GetData<int>("CurrentPrice");
                    MoneySystem.Wallet.Change(player, Price);
                    shape.SetData("CurrentPrice", startPrice);
                    shape.SetData("auction_timer", false);
                    NAPI.Data.ResetEntityData(shape, "player_auction");
                }
            }
            catch (Exception e) { Log.Write("PlayerDisconnected: " + e.Message, nLog.Type.Error); }
        }
        #endregion
        internal class Checkpoint
        {
            public Vector3 Position { get; }
            public double Heading { get; }

            public Checkpoint(Vector3 pos, double rot)
            {
                Position = pos;
                Heading = rot;
            }
        }
    }
}
