using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace AutoBattle
{
    public partial class GameUI : MonoBehaviour
    {
        // 控件工厂：文本、按钮、图标槽、条与面板的 Create 系列
        // ==================== 控件工厂 ====================

        private static RectTransform CreateRect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); // y 向下为正，写起来更直观
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(Transform parent, string name, float x, float y, float w, float h, Color color, bool raycast = false)
        {
            RectTransform rt = CreateRect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>
        /// 浮层面板底板。blockClicks=true 时底板自己吃点击：UGUI 的射线只会落在最上层的
        /// raycastTarget 上，底板如果不吃，点面板空白处会穿透到下层按钮（选关面板曾因此
        /// 一点空白就误买药剂、误扣金币）。
        /// </summary>
        private RectTransform CreatePanel(Transform parent, string name, float x, float y, float w, float h,
            Color color, bool blockClicks = false)
        {
            Image img = CreateImage(parent, name, x, y, w, h, color, blockClicks);
            Round(img, RadiusPanel);
            AddBorder(img, RadiusPanel, GameConfig.Border);
            return img.rectTransform;
        }

        private Text CreateText(Transform parent, string name, string content, float x, float y, float w, float h,
            int fontSize, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            RectTransform rt = CreateRect(parent, name, x, y, w, h);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>背景条 + 填充条；返回填充条供刷新时改比例。</summary>
        private Image CreateBar(Transform parent, string name, float x, float y, float w, float h, Color bgColor, Color fillColor)
        {
            Image bg = CreateImage(parent, name, x, y, w, h, bgColor);
            Round(bg, RadiusBar);
            Image fill = CreateImage(bg.rectTransform, name + "Fill", 0, 0, w, h, fillColor);
            Round(fill, RadiusBar);
            Stretch(fill.rectTransform);
            AddBarGloss(fill);
            return fill;
        }

        private Button CreateButton(Transform parent, string name, string label, float x, float y, float w, float h,
            Color bgColor, out Text labelText)
        {
            Image img = CreateImage(parent, name, x, y, w, h, bgColor, true);
            Round(img, RadiusButton);
            AddBorder(img, RadiusButton, new Color(1f, 1f, 1f, 0.10f));
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            // 点完按钮就把悬停名称收掉：否则点开设置时，指针还停在齿轮上，
            // tooltip 会一直浮在新面板上面。
            btn.onClick.AddListener(HideTooltip);
            // 所有 CreateButton 共用点击音；面板入口的开关音另外挂（避免关闭时重复）。
            btn.onClick.AddListener(GameAudio.PlayUiClick);

            // 注意：这几个颜色是**乘数**，乘在 Image.color（也就是 bgColor）上。
            // 原来写成 1.18f * bgColor，对深色按钮等于又乘一遍暗色，
            // 悬停/按下时整块掉到接近纯黑——x4 停在鼠标下面时就是这么变黑的。
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.30f, 1.30f, 1.30f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            labelText = CreateText(img.rectTransform, name + "Label", label, 0, 0, w, h, 17, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(labelText.rectTransform);

            return btn;
        }

    }
}
