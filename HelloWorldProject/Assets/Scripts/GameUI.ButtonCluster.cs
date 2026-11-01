using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 底框图测得的「可见内芯」偏移表。
    ///
    /// Assets/Resources/Icons/frame_*.png 是生图得到的立体皮革 + 黄铜边框：
    /// 下缘的金边加内阴影比上缘厚，所以可见皮革的中心比图片矩形中心**略低、略右**。
    /// 数值是暗色皮革像素 2%~98% 分位 bbox 的中心相对矩形中心的偏移，
    /// 按 宽/高 归一化（实测脚本 HelloWorldProject/_shots/face_offsets.py）。
    ///
    /// 文字直接按矩形居中会系统性偏低约 2%~3% 按钮高度——这就是「白字有些偏下」的根因；
    /// 带图标的长按钮里文字又贴着图标左对齐，于是看起来「偏左」。两件事都在这里修。
    /// </summary>
    public static class UiFace
    {
        public static Vector2 Offset(string frameKey)
        {
            switch (frameKey)
            {
                case "frame_tab": return new Vector2(0.0190f, 0.0290f);
                case "frame_bar_round": return new Vector2(0.0022f, 0.0254f);
                case "frame_bar_notched": return new Vector2(0.0121f, 0.0245f);
                case "frame_bar_socket": return new Vector2(0.0065f, 0.0224f);
                case "frame_pill": return new Vector2(0.0114f, 0.0293f);
                case "frame_oval": return new Vector2(0.0163f, 0.0229f);
                case "frame_plate": return new Vector2(0.0089f, 0.0314f);
                case "frame_oct": return new Vector2(0.0225f, 0.0255f);
                case "frame_hex": return new Vector2(0.0083f, 0.0206f);
                case "frame_hex_wide": return new Vector2(0.0024f, 0.0224f);
                case "frame_square": return new Vector2(0.0347f, 0.0452f);
                case "frame_square_small": return new Vector2(0.0293f, 0.0260f);
                case "frame_shield": return new Vector2(0.0206f, 0.0270f);
                case "frame_banner": return new Vector2(0.0122f, 0.0220f);
                case "frame_ribbon": return new Vector2(0.0080f, 0.0162f);
                case "frame_ribbon_wide": return new Vector2(0.0032f, 0.0121f);
                default: return Vector2.zero;
            }
        }

        /// <summary>
        /// PNG 里量到的内芯偏移，渲染出来大约只有 1/3 大——底框 PNG 自带烘焙投影，
        /// 那部分暗像素把「内芯重心」往右下拽了。全量套用会让文字整体偏右下（实测选中页签
        /// 墨迹比按钮中心偏 (+1.7,+1.0) 设计像素），所以只取 35%。
        /// </summary>
        public const float Gain = 0.35f;

        /// <summary>
        /// 再整体上抬 0.6px：Unity 的 MiddleCenter 居中的是「行框」，而思源字形的墨迹
        /// 比行框中心略低，纯几何居中读起来就是「压在下面」。上抬半个像素刚好抵消。
        /// </summary>
        public const float Lift = 0.6f;

        /// <summary>换算成设计像素的视觉补偿量（正值 = 往右下）。</summary>
        public static Vector2 Pixels(string frameKey, float w, float h)
        {
            Vector2 n = Offset(frameKey);
            return new Vector2(n.x * w * Gain, n.y * h * Gain - Lift);
        }
    }

    /// <summary>
    /// 把「图标 + 文字」当成一整簇，水平居中，并把整簇压到底框图的内芯中心。
    ///
    /// 之前 StyleBar 把文字设成 MiddleLeft 顶在图标右侧，148 宽的「重置」、200 宽的
    /// 「开始挑战」右侧留出大片空皮革，看起来就是文字偏左。这里统一改成
    /// 「图标宽度 + 间距 + 文字实际宽度」整簇居中，文字变长变短（开始/暂停）也会跟着重排。
    /// </summary>
    public class UiButtonCluster : MonoBehaviour
    {
        public Image icon;
        public Text label;
        public float gap = 6f;
        public float insetX = 10f;
        public Vector2 faceOffset;
        public int maxFontSize;

        private Vector2 _size = new Vector2(-1f, -1f);
        private string _text;

        private void OnEnable()
        {
            _size = new Vector2(-1f, -1f);
            _text = null;
        }

        private void LateUpdate()
        {
            if (label == null) return;
            var rt = (RectTransform)transform;
            Vector2 size = rt.rect.size;
            if (size == _size && label.text == _text) return;
            _size = size;
            _text = label.text;
            Apply(size);
        }

        private void Apply(Vector2 size)
        {
            float w = Mathf.Max(8f, size.x);
            float h = Mathf.Max(8f, size.y);

            float iconW = 0f;
            if (icon != null && icon.sprite != null) iconW = icon.rectTransform.sizeDelta.x;
            float gapW = iconW > 0f ? gap : 0f;

            // 字号：按高度推一个默认值，但不超过调用方显式指定的字号；装不下再逐级缩。
            int fs = Mathf.Clamp(Mathf.RoundToInt(h * 0.33f), 11, 17);
            if (maxFontSize > 0) fs = Mathf.Min(fs, maxFontSize);
            label.resizeTextForBestFit = false;
            label.fontSize = fs;
            float avail = Mathf.Max(16f, w - insetX * 2f - iconW - gapW);
            float textW = label.preferredWidth;
            while (textW > avail && label.fontSize > 9)
            {
                label.fontSize--;
                textW = label.preferredWidth;
            }
            textW = Mathf.Min(textW, avail) + 2f;

            float total = iconW + gapW + textW;
            float startX = (w - total) * 0.5f + faceOffset.x;
            float shiftY = faceOffset.y;

            if (iconW > 0f)
            {
                float iy = (h - icon.rectTransform.sizeDelta.y) * 0.5f + shiftY;
                icon.rectTransform.anchoredPosition = new Vector2(startX, -iy);
            }

            RectTransform lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0f, 1f);
            lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 1f);
            lrt.anchoredPosition = new Vector2(startX + iconW + gapW, -shiftY);
            lrt.sizeDelta = new Vector2(textW, h);
            label.alignment = TextAnchor.MiddleCenter;
        }
    }
}
