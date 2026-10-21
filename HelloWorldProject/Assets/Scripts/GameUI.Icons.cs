using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 图标层：Resources/Icons 下的 256×256 透明圆形徽章。
    /// 生图插件统一生成（深海军蓝底 + 古金边），这里只负责「键 → Sprite」与常用的摆放方式，
    /// 不在各面板里散落 Resources.Load 字符串。
    /// </summary>
    public partial class GameUI
    {
        /// <summary>Resources.Load 的结果缓存：技能池面板每帧刷新 15 张，不能每帧走一遍资源系统。</summary>
        private readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();

        private Sprite IconSprite(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_iconCache.TryGetValue(key, out Sprite cached)) return cached;

            Sprite loaded = Resources.Load<Sprite>("Icons/" + key);
            _iconCache[key] = loaded;
            return loaded;
        }

        /// <summary>建一张固定图标。贴图缺失时隐藏（不留下白方块），布局仍照常占位。</summary>
        private Image CreateIcon(Transform parent, string key, float x, float y, float size)
        {
            Image img = CreateImage(parent, "Icon_" + key, x, y, size, size, Color.white);
            img.sprite = IconSprite(key);
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (img.sprite == null) img.enabled = false;
            return img;
        }

        /// <summary>建一个空图标槽：技能栏/技能卡在刷新时才知道装的是哪个技能。</summary>
        private Image CreateIconSlot(Transform parent, string name, float x, float y, float size)
        {
            Image img = CreateImage(parent, name, x, y, size, size, Color.white);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        /// <summary>
        /// 给按钮左侧挂图标，并把文字起始位置右移，避免文字压在图标上。
        /// size 建议 12~24：标题行按钮高 24，图标超过 16 会顶到圆角。
        /// </summary>
        private Image AddButtonIcon(Button button, Text label, string key, float size, float left = 7f)
        {
            if (button == null) return null;

            var rt = (RectTransform)button.transform;
            float h = rt.sizeDelta.y;
            Image icon = CreateIcon(button.transform, key, left, (h - size) * 0.5f, size);
            if (icon == null || icon.sprite == null || label == null) return icon;

            Vector2 min = label.rectTransform.offsetMin;
            min.x = Mathf.Max(min.x, left + size + 4f);
            label.rectTransform.offsetMin = min;
            return icon;
        }

        /// <summary>
        /// 把按钮改成「图标即按钮」：图标铺满按钮、底衬透明、文字隐藏；
        /// badge=true 时保留原文字作为右下角数字徽章（存档槽 / 成就数）。
        /// 按钮的点击区仍是原矩形，只是视觉上由徽章承担。
        /// </summary>
        private Image StyleIconButton(Button button, Text label, string key, float iconSize, bool badge = false)
        {
            if (button == null) return null;

            var rt = (RectTransform)button.transform;
            float w = rt.sizeDelta.x;
            float h = rt.sizeDelta.y;
            Image icon = CreateIcon(button.transform, key, (w - iconSize) * 0.5f, (h - iconSize) * 0.5f, iconSize);

            if (button.image != null)
            {
                Color bg = button.image.color;
                bg.a = 0f;
                button.image.color = bg;
            }

            // 按钮自带的圆角描边是独立子物体，只清底图会留下一个方块框，盖在圆形徽章上很脏。
            Transform border = button.transform.Find("Border");
            if (border != null)
            {
                Image borderImage = border.GetComponent<Image>();
                if (borderImage != null) borderImage.enabled = false;
            }

            if (label != null)
            {
                if (badge)
                {
                    const float badgeW = 18f;
                    const float badgeH = 14f;
                    const float badgeBottom = 8f;
                    float badgeX = w - badgeW - 4f;
                    float badgeY = h - badgeH - badgeBottom;

                    Image badgeBg = CreateImage(button.transform, "BadgeBg", badgeX, badgeY, badgeW, badgeH,
                        new Color(0.02f, 0.03f, 0.05f, 0.90f));
                    Round(badgeBg, 7);

                    label.fontSize = 10;
                    label.fontStyle = FontStyle.Bold;
                    label.color = GameConfig.TextGold;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.rectTransform.offsetMin = new Vector2(badgeX, badgeBottom);
                    label.rectTransform.offsetMax = new Vector2(-4f, -(h - badgeH - badgeBottom));

                    Outline outline = label.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                    outline.effectDistance = new Vector2(1f, -1f);

                    // 图标/底衬是后建的同级节点，默认会盖住先建的文字；数字徽章必须排在最后才看得见。
                    label.transform.SetAsLastSibling();
                }
                else
                {
                    label.gameObject.SetActive(false);
                }
            }

            return icon;
        }

        /// <summary>给图标槽换图；key 为空或资源缺失时隐藏。</summary>
        private void SetIcon(Image image, string key)
        {
            if (image == null) return;
            Sprite sprite = IconSprite(key);
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private static string SkillIconKey(int skillId)
        {
            switch ((SkillId)skillId)
            {
                case SkillId.HeavyStrike: return "skill_heavy_strike";
                case SkillId.Whirlwind: return "skill_whirlwind";
                case SkillId.Berserk: return "skill_berserk";
                case SkillId.Execute: return "skill_execute";
                case SkillId.IronBody: return "skill_iron_body";
                case SkillId.BerserkHeart: return "skill_berserk_heart";
                case SkillId.LethalStrike: return "skill_lethal_strike";
                case SkillId.Vampire: return "skill_vampire";
                case SkillId.Swiftness: return "skill_swiftness";
                case SkillId.GiantSlayer: return "skill_giant_slayer";
                case SkillId.Undying: return "skill_undying";
                case SkillId.Counter: return "skill_counter";
                case SkillId.CritChain: return "skill_crit_chain";
                case SkillId.NearDeathBurst: return "skill_near_death_burst";
                case SkillId.ShieldGen: return "skill_shield_gen";
                default: return null;
            }
        }

        private static string SlotIconKey(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon: return "ui_attack";
                case EquipmentSlot.Armor: return "ui_defense";
                case EquipmentSlot.Helmet: return "ui_health";
                case EquipmentSlot.Boots: return "ui_speed";
                case EquipmentSlot.Accessory: return "ui_armor_pen";
                default: return "ui_compare";
            }
        }
    }
}
