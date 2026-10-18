using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 视觉主题层：程序化生成圆角/描边贴图，提供卡片、分区标题、进度条等统一样式。
    /// 换皮肤的入口只有两处——这里和 <see cref="GameConfig"/> 的配色块（GameConfig.Colors.cs）。
    /// </summary>
    public partial class GameUI
    {
        // ---- 统一字号 ----
        public const int FontTitle = 22;
        public const int FontSection = 18;
        public const int FontBody = 15;
        public const int FontSmall = 13;
        public const int FontTiny = 11;

        // ---- 统一圆角 ----
        public const int RadiusPanel = 12;
        public const int RadiusCard = 10;
        public const int RadiusButton = 8;
        public const int RadiusBar = 5;

        private static readonly Dictionary<int, Sprite> _roundCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> _ringCache = new Dictionary<int, Sprite>();

        /// <summary>圆角矩形（白色形状，靠 Image.color 染色）。按半径缓存，4 倍超采样抗锯齿。</summary>
        public static Sprite RoundSprite(int radius)
        {
            radius = Mathf.Clamp(radius, 2, 32);
            if (_roundCache.TryGetValue(radius, out Sprite cached) && cached != null) return cached;

            const int ss = 4;
            int size = radius * 2 + 4;          // 两端各留 2px 直边，九宫格拉伸才不会变形
            int tex = size * ss;
            float r = (radius + 2) * ss;        // 圆角半径（含留边）
            var pixels = new Color32[tex * tex];

            for (int y = 0; y < tex; y++)
            for (int x = 0; x < tex; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float dx = Mathf.Max(r - px, px - (tex - r), 0f);
                float dy = Mathf.Max(r - py, py - (tex - r), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - r;    // <0 在内部
                float inside = Mathf.Clamp01(-d + 0.5f);
                pixels[y * tex + x] = new Color32(255, 255, 255, (byte)(inside * 255f));
            }

            var texture = new Texture2D(tex, tex, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Round" + radius
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            int border = (radius + 2) * ss;
            var sprite = Sprite.Create(texture, new Rect(0, 0, tex, tex), new Vector2(0.5f, 0.5f),
                100f * ss, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = "Round" + radius;
            _roundCache[radius] = sprite;
            return sprite;
        }

        /// <summary>空心的圆角描边环（1.5px 视觉厚度），铺在卡片/按钮上做细边框。</summary>
        public static Sprite RingSprite(int radius)
        {
            radius = Mathf.Clamp(radius, 2, 32);
            if (_ringCache.TryGetValue(radius, out Sprite cached) && cached != null) return cached;

            const int ss = 4;
            int size = radius * 2 + 4;
            int tex = size * ss;
            float r = (radius + 2) * ss;
            float half = tex * 0.5f;
            var pixels = new Color32[tex * tex];

            for (int y = 0; y < tex; y++)
            for (int x = 0; x < tex; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float dx = Mathf.Max(r - px, px - (tex - r), 0f);
                float dy = Mathf.Max(r - py, py - (tex - r), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - r;    // <0 在内部
                // 只在「内部且贴着边」的一圈上不透明，其余透明。
                float ring = Mathf.Clamp01(-d) * Mathf.Clamp01(d + 1.6f * ss);
                pixels[y * tex + x] = new Color32(255, 255, 255, (byte)(ring * 255f));
            }

            var texture = new Texture2D(tex, tex, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Ring" + radius
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            int border = (radius + 2) * ss;
            var sprite = Sprite.Create(texture, new Rect(0, 0, tex, tex), new Vector2(0.5f, 0.5f),
                100f * ss, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = "Ring" + radius;
            _ringCache[radius] = sprite;
            return sprite;
        }

        private static void Round(Image img, int radius)
        {
            if (img == null) return;
            img.sprite = RoundSprite(radius);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
        }

        /// <summary>在已有控件上补一圈描边（作为子物体，不挡点击、不参与布局）。</summary>
        private static Image AddBorder(Image parent, int radius, Color color)
        {
            if (parent == null) return null;
            RectTransform rt = CreateRect(parent.transform, "Border", 0, 0, 0, 0);
            Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RingSprite(radius);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>卡片：圆角底 + 细描边。返回底图，内容用它的 transform 作父级。</summary>
        private Image CreateCard(Transform parent, string name, float x, float y, float w, float h,
            Color fill, int radius = RadiusCard, Color? border = null, bool raycast = false)
        {
            Image img = CreateImage(parent, name, x, y, w, h, fill, raycast);
            Round(img, radius);
            AddBorder(img, radius, border ?? GameConfig.Border);
            return img;
        }

        /// <summary>分区标题：左侧一道竖向强调条 + 标题文字。</summary>
        private Text CreateSectionTitle(Transform parent, string title, float x, float y, float w,
            int fontSize = FontSection)
        {
            CreateImage(parent, "AccentBar_" + title, x, y + 4, 3, fontSize - 3, GameConfig.AccentSoft);
            return CreateText(parent, "Title_" + title, title, x + 10, y, w - 10, fontSize + 8,
                fontSize, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private static Sprite _glow;

        /// <summary>径向光晕：中心不透明、边缘化到全透明，靠 Image.color 染色。给立绘垫一层「打光」，
        /// 否则深色立绘贴在同样深的卡底上会整块陷进去。</summary>
        public static Sprite GlowSprite()
        {
            if (_glow != null) return _glow;

            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Glow"
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a *= a;                       // 平方一下，边缘化得更开
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            _glow = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _glow.name = "Glow";
            return _glow;
        }

        /// <summary>条上的亮面：让进度条有一点厚度，而不是纯色块。</summary>
        private static void AddBarGloss(Image fill)
        {
            if (fill == null) return;
            RectTransform rt = CreateRect(fill.transform, "Gloss", 0, 0, 0, 0);
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(1f, -1f);
            rt.offsetMax = new Vector2(-1f, -1f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundSprite(RadiusBar);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = new Color(1f, 1f, 1f, 0.12f);
            img.raycastTarget = false;
        }
    }
}
