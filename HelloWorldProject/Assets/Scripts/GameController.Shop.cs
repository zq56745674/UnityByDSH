using UnityEngine;

namespace AutoBattle
{
    /// <summary>购买结果：UI 需要区分「已扣款」「等待二次确认」「钱不够」「已拥有」。</summary>
    public enum ShopPurchaseResult
    {
        Success = 0,
        NeedConfirm = 1,
        InsufficientGold = 2,
        MaxRank = 3,
        AlreadyOwned = 4,
        Invalid = 5,
    }

    /// <summary>
    /// 营地行商（B1 / docs/shop-2-design.md）。
    /// 只做三件事：扣金币、写存档、把「操作类服务」接到既有面板上。
    /// 战斗内 3 秒补给站不在这里，仍然由 GameController 的 Shop 状态机负责。
    /// </summary>
    public partial class GameController
    {
        /// <summary>行商面板是否打开（打开时冻结战斗，和成就/设置同一套浮层规则）。</summary>
        public bool ShopPanelOpen { get; private set; }

        /// <summary>每次购买/装备外观/改自动出售规则都 +1，UI 用它判断要不要重画并清背景缓存。</summary>
        public int ShopRevision { get; private set; }

        private string _shopConfirmKey = string.Empty;
        private float _shopConfirmUntil;

        /// <summary>已上膛的购买项 key（空 = 没有待确认）。</summary>
        public string ShopConfirmKey => _shopConfirmKey;

        /// <summary>二次确认剩余秒数。</summary>
        public float ShopConfirmLeft =>
            string.IsNullOrEmpty(_shopConfirmKey) ? 0f : Mathf.Max(0f, _shopConfirmUntil - Time.unscaledTime);

        public void ToggleShopPanel()
        {
            if (ChoicePending) return;
            bool open = !ShopPanelOpen;
            CloseAllPanels();
            ShopPanelOpen = open;
        }

        public void CloseShopPanel()
        {
            ShopPanelOpen = false;
            CancelShopConfirm();
        }

        private void CancelShopConfirm()
        {
            _shopConfirmKey = string.Empty;
            _shopConfirmUntil = 0f;
        }

        /// <summary>
        /// 购买入口。key 约定："rank"（收藏家升 1 级）、"svc:N"（第 N 项服务）、"cos:N"（第 N 件珍藏）。
        /// 单价 ≥ 1,000,000 时需要连点两次：第一次只上膛，3 秒内再点同一条才扣款。
        /// </summary>
        public ShopPurchaseResult RequestPurchase(string key)
        {
            if (Player == null || string.IsNullOrEmpty(key)) return ShopPurchaseResult.Invalid;

            int price;
            ShopPurchaseResult check = CheckPurchase(key, out price, out string error);
            if (check != ShopPurchaseResult.Success)
            {
                if (!string.IsNullOrEmpty(error)) AddLog(error, GameConfig.LogWarn);
                CancelShopConfirm();
                return check;
            }

            if (price >= ShopCatalog.ConfirmThreshold &&
                (_shopConfirmKey != key || Time.unscaledTime > _shopConfirmUntil))
            {
                _shopConfirmKey = key;
                _shopConfirmUntil = Time.unscaledTime + ShopCatalog.ConfirmWindow;
                GameAudio.PlayUiClick();
                AddLog($"◇ 待确认：{DescribeKey(key)} 需 {price:N0} 金币，3 秒内再点一次才扣款。", GameConfig.LogSystem);
                return ShopPurchaseResult.NeedConfirm;
            }

            CancelShopConfirm();
            if (Player.gold < price)
            {
                AddLog($"✕ 金币不足：{DescribeKey(key)} 需要 {price:N0}，当前 {Player.gold:N0}。", GameConfig.LogWarn);
                return ShopPurchaseResult.InsufficientGold;
            }

            Player.gold -= price;
            ApplyPurchase(key, price);
            ShopRevision++;
            GameAudio.PlayBuy();
            bool saved = TrySave();
            if (!saved)
                AddLog("⚠ 购买已生效，但存档写入失败；本次改动只在本局有效。", GameConfig.LogWarn);
            return ShopPurchaseResult.Success;
        }

        private ShopPurchaseResult CheckPurchase(string key, out int price, out string error)
        {
            price = 0;
            error = null;

            if (key == "rank")
            {
                if (ShopCatalog.IsMaxRank(Player))
                {
                    error = "收藏家已满级（Lv.20）。";
                    return ShopPurchaseResult.MaxRank;
                }
                price = ShopCatalog.NextRankCost(Player);
                return ShopPurchaseResult.Success;
            }

            if (key.StartsWith("svc:"))
            {
                int index;
                if (!int.TryParse(key.Substring(4), out index) ||
                    index < 0 || index >= ShopCatalog.ServiceCount) return ShopPurchaseResult.Invalid;
                if (ShopCatalog.HasService(Player, index))
                {
                    error = $"服务「{ShopCatalog.ServiceNames[index]}」已购买。";
                    return ShopPurchaseResult.AlreadyOwned;
                }
                price = ShopCatalog.ServicePrices[index];
                return ShopPurchaseResult.Success;
            }

            if (key.StartsWith("cos:"))
            {
                int index;
                if (!int.TryParse(key.Substring(4), out index) ||
                    index < 0 || index >= ShopCatalog.CosmeticCount) return ShopPurchaseResult.Invalid;
                if (ShopCatalog.HasCosmetic(Player, index))
                {
                    error = $"珍藏「{ShopCatalog.CosmeticNames[index]}」已拥有。";
                    return ShopPurchaseResult.AlreadyOwned;
                }
                price = ShopCatalog.CosmeticPrices[index];
                return ShopPurchaseResult.Success;
            }

            return ShopPurchaseResult.Invalid;
        }

        private string DescribeKey(string key)
        {
            if (key == "rank") return "收藏家等级 +1";
            if (key.StartsWith("svc:"))
            {
                int i;
                if (int.TryParse(key.Substring(4), out i) && i >= 0 && i < ShopCatalog.ServiceCount)
                    return "服务「" + ShopCatalog.ServiceNames[i] + "」";
            }
            if (key.StartsWith("cos:"))
            {
                int i;
                if (int.TryParse(key.Substring(4), out i) && i >= 0 && i < ShopCatalog.CosmeticCount)
                    return "珍藏「" + ShopCatalog.CosmeticNames[i] + "」";
            }
            return key;
        }

        private void ApplyPurchase(string key, int price)
        {
            if (key == "rank")
            {
                Player.collectorRank = Mathf.Clamp(Player.collectorRank + 1, 0, ShopCatalog.MaxCollectorRank);
                Player.shopCosmeticMask |= ShopCatalog.CollectorRewardMask(Player.collectorRank);
                AddLog($"◆ 收藏家等级提升至 Lv.{Player.collectorRank}（-{price:N0} 金币，余 {Player.gold:N0}）", GameConfig.LogGold);
                string reward = CollectorRankReward(Player.collectorRank);
                if (!string.IsNullOrEmpty(reward))
                    AddLog("  ★ " + reward, GameConfig.LogUnlock);
                return;
            }

            if (key.StartsWith("svc:"))
            {
                int index = int.Parse(key.Substring(4));
                Player.shopServiceMask |= 1 << index;
                AddLog($"◆ 已解锁服务「{ShopCatalog.ServiceNames[index]}」（-{price:N0} 金币，余 {Player.gold:N0}）",
                    GameConfig.LogSuccess);
                return;
            }

            int cos = int.Parse(key.Substring(4));
            Player.shopCosmeticMask |= 1 << cos;
            AddLog($"◆ 已获得珍藏「{ShopCatalog.CosmeticNames[cos]}」（-{price:N0} 金币，余 {Player.gold:N0}）",
                GameConfig.LogSuccess);
        }

        private static string CollectorRankReward(int rank)
        {
            switch (rank)
            {
                case 1: return "称号前缀「行商」已生效，头顶称号后缀会显示行商。";
                case 5: return "获得收藏家奖励：头像框·青铜（可在珍藏页装备）。";
                case 10: return "获得收藏家奖励：头像框·白银、背景色调·暖阳。";
                case 15: return "获得收藏家奖励：头像框·赤金、伤害数字·赤金。";
                case 20: return "获得收藏家最高称号「黄金旅人」、背景色调·极光。";
                default: return null;
            }
        }

        /// <summary>装备/卸下珍藏。index 是 ShopCatalog 的 0..14 全局下标。</summary>
        public bool EquipCosmetic(int index)
        {
            if (Player == null || !ShopCatalog.HasCosmetic(Player, index)) return false;
            ShopCatalog.CosmeticKind kind = ShopCatalog.KindOf(index);
            int current = ShopCatalog.EquippedCosmetic(Player, kind);
            int next = current == index ? -1 : index;
            ShopCatalog.SetEquippedCosmetic(Player, kind, next);
            ShopRevision++;
            GameAudio.PlayUiClick();
            AddLog(next < 0
                ? $"◇ 已卸下 {ShopCatalog.CosmeticNames[index]}"
                : $"◇ 已装备 {ShopCatalog.CosmeticNames[index]}", GameConfig.LogInfo);
            TrySave();
            return true;
        }

        /// <summary>自动出售阈值：-1 关，0 普通，1 优秀，2 稀有。需要先买服务 3。</summary>
        public bool SetAutoSellQuality(int quality)
        {
            if (Player == null) return false;
            if (quality >= 0 && !ShopCatalog.HasService(Player, 3))
            {
                AddLog("✕ 自动出售规则尚未解锁（服务页购买后可用）。", GameConfig.LogWarn);
                return false;
            }

            Player.autoSellQuality = Mathf.Clamp(quality, -1, 2);
            ShopRevision++;
            GameAudio.PlayUiClick();
            AddLog("◇ " + ShopCatalog.AutoSellLabel(Player.autoSellQuality), GameConfig.LogInfo);
            TrySave();
            return true;
        }

        /// <summary>一键锁定/解锁当前五件，返回实际改动件数。锁只挡自动替换，不影响对比面板手动换装。</summary>
        public int LockAllEquipment(bool locked)
        {
            if (Player == null) return 0;
            int changed = 0;
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                Equipment e = Player.GetEquipment((EquipmentSlot)i);
                if (e == null || e.locked == locked) continue;
                e.locked = locked;
                changed++;
            }

            if (changed > 0)
            {
                GameAudio.PlayUiClick();
                AddLog(locked ? $"◇ 已锁定 {changed} 件装备（自动换装不会替换它们）" : $"◇ 已解锁 {changed} 件装备",
                    GameConfig.LogInfo);
                TrySave();
            }
            return changed;
        }

        /// <summary>服务 1：一次给五个槽位各强化最多 levels 级，逐级扣费，钱不够就停。</summary>
        public int EnhanceAllSlots(int levels)
        {
            if (Player == null) return 0;
            int total = 0;
            EnhanceResult stop = EnhanceResult.Success;
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                EnhanceResult reason;
                total += EnhanceEquipmentBulk((EquipmentSlot)i, levels, out reason);
                if (reason != EnhanceResult.Success) stop = reason;
            }

            if (total > 0)
            {
                AddLog($"◆ 全槽强化完成：共 {total} 级，余 {Player.gold:N0} 金币。", GameConfig.LogSuccess);
                TrySave();
            }
            else
            {
                AddLog(stop == EnhanceResult.NotEnoughGold
                    ? "✕ 金币不足，未能强化任何一级。"
                    : "◇ 没有可强化的装备（空槽或已满级）。", GameConfig.LogWarn);
            }
            return total;
        }

        /// <summary>服务 2：连洗最多 times 次，锁定词条原样传入；中途钱不够/全锁就停。</summary>
        public int ReforgeMultiple(EquipmentSlot slot, int lockedMask, int times)
        {
            if (Player == null) return 0;
            int done = 0;
            ReforgeResult last = ReforgeResult.Success;
            for (int i = 0; i < Mathf.Max(0, times); i++)
            {
                last = ReforgeEquipment(slot, lockedMask);
                if (last != ReforgeResult.Success) break;
                done++;
            }

            if (done > 0)
            {
                AddLog($"◆ 连续洗练完成：{done} 次，余 {Player.gold:N0} 金币。", GameConfig.LogSuccess);
                TrySave();
            }
            else
            {
                AddLog(last == ReforgeResult.NotEnoughGold ? "✕ 金币不足，未洗练。" : "◇ 没有可洗练的词条（空槽或全部锁定）。",
                    GameConfig.LogWarn);
            }
            return done;
        }
    }
}
