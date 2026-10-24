using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 带名称提示的按钮。图标按钮看不出用途时，鼠标悬停显示名称。
    /// </summary>
    public class UITooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameUI owner;
        public string text;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (owner != null) owner.ShowTooltip(text, (RectTransform)transform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (owner != null) owner.HideTooltip();
        }

        private void OnDisable()
        {
            if (owner != null) owner.HideTooltip();
        }
    }

    /// <summary>
    /// 给任意 Graphic（属性文字、进度条、规则区）挂悬停提示。
    /// 按钮走 UITooltipTrigger（Button 负责点击），非按钮控件用这个，两者接口一致。
    /// </summary>
    public class UITooltipArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameUI owner;
        public string text;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (owner != null) owner.ShowTooltip(text, (RectTransform)transform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (owner != null) owner.HideTooltip();
        }

        private void OnDisable()
        {
            if (owner != null) owner.HideTooltip();
        }
    }

    public partial class GameUI
    {
        private RectTransform _tooltipRoot;
        private RectTransform _tooltipParent;
        private Image _tooltipBg;
        private Text _tooltipText;

        // ==================== 按钮底板 ====================

        /// <summary>
        /// 把按钮换成长条/方形/盾形等底框。底框由生图插件生成，不能再用旧按钮的
        /// 圆角底色和 Border 子物体，否则会盖在花纹上。
        /// </summary>
        private bool ApplyButtonFrame(Button button, string frameKey)
        {
            if (button == null) return false;
            Sprite sprite = IconSprite(frameKey);
            if (sprite == null) return false;

            Image bg = button.image;
            if (bg == null) return false;
            bg.sprite = sprite;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
            bg.color = Color.white;

            Transform border = button.transform.Find("Border");
            if (border != null)
            {
                Image borderImage = border.GetComponent<Image>();
                if (borderImage != null) borderImage.enabled = false;
            }
            return true;
        }

        /// <summary>横向按钮：图标在左、名称在右。用于开始/暂停、关闭、换装、购买等。</summary>
        private Image StyleBar(Button button, Text label, string frameKey, string glyphKey,
            float glyphSize, string tooltip = null, int fontSize = 0)
        {
            if (button == null) return null;
            if (!ApplyButtonFrame(button, frameKey))
            {
                // 资源缺失时保留旧按钮，至少还能点。
                if (label != null && !string.IsNullOrEmpty(tooltip)) AttachTooltip(button, tooltip);
                return null;
            }

            var rt = (RectTransform)button.transform;
            float h = rt.sizeDelta.y;
            float iconY = (h - glyphSize) * 0.5f;
            // 生图底框有一圈较厚的金边，图标贴太左会被金边吃掉（播放键的左尖、暂停键的左条最明显）。
            // 按按钮高度给内边距，短按钮不会浪费空间，56 高的开始/暂停也能完整露出。
            float iconX = Mathf.Clamp(h * 0.44f, 9f, 27f);
            Image icon = CreateIcon(button.transform, glyphKey, iconX, iconY, glyphSize);

            if (label != null)
            {
                label.gameObject.SetActive(true);
                int size = fontSize > 0 ? fontSize : Mathf.Clamp(Mathf.RoundToInt(h * 0.33f), 11, 17);
                label.fontSize = size;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleLeft;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 9;
                label.resizeTextMaxSize = size;
                label.rectTransform.offsetMin = new Vector2(iconX + glyphSize + 6f, 2f);
                label.rectTransform.offsetMax = new Vector2(-10f, -2f);
            }

            AttachTooltip(button, string.IsNullOrEmpty(tooltip) ? (label != null ? label.text : null) : tooltip);
            return icon;
        }

        /// <summary>紧凑图标按钮：没有文字，悬停显示名称；badge=true 时保留右下角数字。</summary>
        private Image StyleIcon(Button button, Text label, string frameKey, string glyphKey,
            float glyphSize, string tooltip = null, bool badge = false)
        {
            if (button == null) return null;
            if (!ApplyButtonFrame(button, frameKey)) return null;

            var rt = (RectTransform)button.transform;
            float w = rt.sizeDelta.x;
            float h = rt.sizeDelta.y;
            Image icon = CreateIcon(button.transform, glyphKey,
                (w - glyphSize) * 0.5f, (h - glyphSize) * 0.5f, glyphSize);

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

                    label.gameObject.SetActive(true);
                    label.fontSize = 10;
                    label.fontStyle = FontStyle.Bold;
                    label.color = GameConfig.TextGold;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.resizeTextForBestFit = false;
                    label.rectTransform.offsetMin = new Vector2(badgeX, badgeBottom);
                    label.rectTransform.offsetMax = new Vector2(-4f, -(h - badgeH - badgeBottom));

                    Outline outline = label.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                    outline.effectDistance = new Vector2(1f, -1f);
                    label.transform.SetAsLastSibling();
                }
                else
                {
                    label.gameObject.SetActive(false);
                }
            }

            AttachTooltip(button, tooltip);
            return icon;
        }

        /// <summary>上方图标、下方短名称的按钮。速度 ×1/×2/×4 用这种结构。</summary>
        private Image StyleStack(Button button, Text label, string frameKey, string glyphKey,
            float glyphSize, string tooltip = null)
        {
            if (button == null) return null;
            if (!ApplyButtonFrame(button, frameKey)) return null;

            var rt = (RectTransform)button.transform;
            float w = rt.sizeDelta.x;
            float h = rt.sizeDelta.y;
            Image icon = CreateIcon(button.transform, glyphKey,
                (w - glyphSize) * 0.5f, 6f, glyphSize);

            if (label != null)
            {
                label.gameObject.SetActive(true);
                label.fontSize = 11;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleCenter;
                label.resizeTextForBestFit = false;
                label.rectTransform.offsetMin = new Vector2(2f, 7f);
                label.rectTransform.offsetMax = new Vector2(-2f, -(h - 21f));
            }

            AttachTooltip(button, tooltip);
            return icon;
        }

        /// <summary>页签：只换底板，文字居中，不用图标（章节/分类太多，图标会变成猜谜）。</summary>
        private void StyleTab(Button button, Text label, string frameKey, int fontSize = 12)
        {
            if (button == null || !ApplyButtonFrame(button, frameKey)) return;
            if (label == null) return;

            label.gameObject.SetActive(true);
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 9;
            label.resizeTextMaxSize = fontSize;
            label.rectTransform.offsetMin = new Vector2(6f, 1f);
            label.rectTransform.offsetMax = new Vector2(-6f, -1f);
        }

        /// <summary>没有图标、只有底框的装饰/选项按钮（装备槽页签、成就分类）。</summary>
        private void StylePlain(Button button, Text label, string frameKey)
        {
            StyleTab(button, label, frameKey, 11);
        }

        // ==================== 悬停名称 ====================

        private void BuildTooltip(Transform root)
        {
            _tooltipBg = CreateImage(root, "UITooltip", 0, 0, 300, 30,
                new Color(0.02f, 0.03f, 0.05f, 0.97f), false);
            Round(_tooltipBg, 8);
            AddBorder(_tooltipBg, 8, new Color(0.82f, 0.66f, 0.34f, 0.95f));
            _tooltipRoot = _tooltipBg.rectTransform;
            _tooltipRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltipRoot.pivot = new Vector2(0.5f, 0.5f);

            _tooltipParent = root as RectTransform;

            _tooltipText = CreateText(_tooltipRoot, "Text", "", 0, 0, 300, 30, 13,
                GameConfig.TextTitle, TextAnchor.MiddleCenter);
            // 文本必须用固定尺寸（锚点居中 + sizeDelta），不能 Stretch 之后再写 sizeDelta：
            // 拉伸锚点下 sizeDelta 是「相对父框的增量」，写进去等于把文本框撑成父框两倍宽，
            // 文字整体被推出提示框（实测偏右下约 70px）。这里锚点居中，sizeDelta 就是真实尺寸。
            RectTransform textRt = _tooltipText.rectTransform;
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = Vector2.zero;
            textRt.sizeDelta = new Vector2(280f, 22f);
            _tooltipRoot.gameObject.SetActive(false);
        }

        private const float TooltipPadX = 10f;
        private const float TooltipPadY = 5f;
        private const float TooltipGap = 8f;

        /// <summary>
        /// 悬停名称。坐标系全部在 1280×720 的设计容器里算，不碰 Screen.width/World：
        /// 画布是 Expand 的 ScreenSpaceOverlay，视口比例一变，世界坐标和屏幕像素就不再一一对应，
        /// 而按钮的 pivot 又是左上角（CreateRect 的约定），拿 target.position 当中心会让提示框
        /// 整体左移半个按钮宽。这里取包围盒四角中心，并把边界钳在容器矩形内。
        /// </summary>
        public void ShowTooltip(string text, RectTransform target)
        {
            if (_tooltipRoot == null || _tooltipParent == null || target == null || string.IsNullOrEmpty(text)) return;

            Rect area = _tooltipParent.rect;
            float maxWidth = Mathf.Clamp(area.width - 16f, 90f, 360f);
            float width = Mathf.Clamp(EstimateTextWidth(text) + TooltipPadX * 2f, 90f, maxWidth);

            RectTransform textRt = _tooltipText.rectTransform;
            _tooltipText.text = text;
            // 单行提示居中；多行（装备卡、转生购买键）必须左对齐——居中会让长短行像排版错位。
            _tooltipText.alignment = text.IndexOf('\n') >= 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            // 先把宽度定下来，preferredHeight 才是「这个宽度下换行后」的真实高度。
            textRt.sizeDelta = new Vector2(width - TooltipPadX * 2f, 22f);
            float textHeight = Mathf.Max(18f, _tooltipText.preferredHeight);
            float height = textHeight + TooltipPadY * 2f;
            textRt.sizeDelta = new Vector2(width - TooltipPadX * 2f, textHeight);
            _tooltipRoot.sizeDelta = new Vector2(width, height);

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 bl = _tooltipParent.InverseTransformPoint(corners[0]);
            Vector3 tr = _tooltipParent.InverseTransformPoint(corners[2]);
            float centerX = (bl.x + tr.x) * 0.5f;
            float bottom = Mathf.Min(bl.y, tr.y);
            float top = Mathf.Max(bl.y, tr.y);

            float half = width * 0.5f;
            float x = Mathf.Clamp(centerX, area.xMin + half + 4f, Mathf.Max(area.xMin + half + 4f, area.xMax - half - 4f));

            // 默认挂在按钮下方；下方顶到容器底边时翻到上方（底部那排按钮都是这种情况）。
            float y = bottom - TooltipGap - height * 0.5f;
            if (y - height * 0.5f < area.yMin + 4f) y = top + TooltipGap + height * 0.5f;
            y = Mathf.Clamp(y, area.yMin + height * 0.5f + 4f, area.yMax - height * 0.5f - 4f);

            _tooltipRoot.anchoredPosition = new Vector2(x, y);
            _tooltipRoot.SetAsLastSibling();
            _tooltipRoot.gameObject.SetActive(true);
        }

        /// <summary>
        /// 粗估宽度（13 号字）：全角约 13.6px、半角约 7.2px。
        /// 跳过富文本标签（&lt;color&gt; &lt;size&gt; &lt;b&gt; 不占宽度），并按「最长的一行」算——
        /// 否则多行提示会被当成一整行超长文本，宽度一上来就顶到上限。
        /// </summary>
        private static float EstimateTextWidth(string text)
        {
            float best = 0f, w = 0f;
            bool inTag = false;
            foreach (char c in text)
            {
                if (c == '<') { inTag = true; continue; }
                if (inTag) { if (c == '>') inTag = false; continue; }
                if (c == '\n') { if (w > best) best = w; w = 0f; continue; }
                w += c >= 0x2E80 && c <= 0xFFEF ? 13.6f : 7.2f;
            }
            return w > best ? w : best;
        }

        public void HideTooltip()
        {
            if (_tooltipRoot != null) _tooltipRoot.gameObject.SetActive(false);
        }

        private void AttachTooltip(Button button, string text)
        {
            if (button == null || string.IsNullOrEmpty(text)) return;
            UITooltipTrigger trigger = button.GetComponent<UITooltipTrigger>();
            if (trigger == null) trigger = button.gameObject.AddComponent<UITooltipTrigger>();
            trigger.owner = this;
            trigger.text = text;
        }

        /// <summary>
        /// 动态 tooltip：装备卡、转生购买键的内容会随装备/印记余额变，
        /// 建面板时写死一份就会一直显示建面板那一刻的文本。传空串等于「这一帧没有提示」。
        /// </summary>
        private void SetTooltip(Button button, string text)
        {
            if (button == null) return;

            UITooltipTrigger trigger = button.GetComponent<UITooltipTrigger>();
            if (trigger == null)
            {
                if (!string.IsNullOrEmpty(text)) AttachTooltip(button, text);
                return;
            }

            trigger.text = text ?? string.Empty;
        }

        /// <summary>非按钮控件的悬停提示（属性行、规则文字、质量说明）。</summary>
        private void AttachTooltip(RectTransform target, string text)
        {
            if (target == null || string.IsNullOrEmpty(text)) return;
            UITooltipArea area = target.GetComponent<UITooltipArea>();
            if (area == null) area = target.gameObject.AddComponent<UITooltipArea>();
            area.owner = this;
            area.text = text;
        }

        /// <summary>非按钮控件的动态悬停提示；传空串等于「这一帧没有提示」。</summary>
        private void SetTooltip(RectTransform target, string text)
        {
            if (target == null) return;
            UITooltipArea area = target.GetComponent<UITooltipArea>();
            if (area == null)
            {
                if (!string.IsNullOrEmpty(text)) AttachTooltip(target, text);
                return;
            }

            area.text = text ?? string.Empty;
        }
    }
}