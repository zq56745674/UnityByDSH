using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 左上角立绘的待机动画。
    ///
    /// 立绘是单张 803×1024 的透明底插画，没有骨骼也没有序列帧；为了不改变画风，
    /// 这里只做“呼吸 + 轻微浮动/摇摆 + 偶发眨眼”这类整体变换，绝不重绘角色。
    /// 眨眼帧 HeroPortraitBlink.png 是用同一张图做图生图得到的（只闭眼，其余保持不变）。
    ///
    /// 时间基准用 Time.unscaledDeltaTime：立绘是界面装饰，不应该跟着 x2/x4 战斗速度抽动。
    /// </summary>
    public partial class GameUI
    {
        private Image _portraitImage;
        private RectTransform _portraitRect;
        private Image _portraitGlow;
        private Image _portraitFloor;
        private Color _portraitGlowBase;
        private Color _portraitFloorBase;
        private Vector2 _portraitHome;
        private Sprite _portraitOpenSprite;
        private Sprite _portraitBlinkSprite;
        private float _portraitTime;
        private float _portraitBlinkTimer = 3f;
        private float _portraitBlinkHold;
        private float _portraitBlinkGap;
        private int _portraitBlinksQueued;

        /// <summary>
        /// 由 BuildHeroPortrait 调用：把立绘改成中心 pivot（缩放/旋转才不会绕左上角甩出去），
        /// 记住基准位置，并加载眨眼帧。眨眼帧缺失时只保留呼吸/摆动，不影响启动。
        /// </summary>
        private void SetupHeroPortrait(Image img, Image glow, Image floorLight)
        {
            if (img == null) return;

            _portraitImage = img;
            _portraitRect = img.rectTransform;
            _portraitOpenSprite = img.sprite;

            // CreateRect 默认 pivot 在左上角；这里改成中心，再做 scale/rotation 才是“原地呼吸”。
            // anchoredPosition 也要补上半个尺寸，保证视觉位置不变。
            Vector2 half = new Vector2(_portraitRect.sizeDelta.x * 0.5f, _portraitRect.sizeDelta.y * 0.5f);
            _portraitRect.pivot = new Vector2(0.5f, 0.5f);
            _portraitRect.anchoredPosition += new Vector2(half.x, -half.y);
            _portraitHome = _portraitRect.anchoredPosition;
            _portraitRect.localScale = Vector3.one;
            _portraitRect.localRotation = Quaternion.identity;

            _portraitGlow = glow;
            _portraitFloor = floorLight;
            if (_portraitGlow != null) _portraitGlowBase = _portraitGlow.color;
            if (_portraitFloor != null) _portraitFloorBase = _portraitFloor.color;

            _portraitBlinkSprite = Resources.Load<Sprite>("HeroPortraitBlink");
            if (_portraitBlinkSprite == null)
            {
                Debug.LogWarning("[GameUI] 找不到 Resources/HeroPortraitBlink.png，立绘只做呼吸摆动，不眨眼。");
            }

            // 从随机相位开始，避免每次启动都像机械表一样同时吸气。
            _portraitTime = GameRandom.Range(0f, 8f);
            _portraitBlinkTimer = GameRandom.Range(2.6f, 4.4f);
        }

        /// <summary>每帧推进立绘动画。由 GameUI.Update 调用。</summary>
        private void TickHeroPortrait()
        {
            if (_portraitRect == null || _portraitImage == null) return;

            // 掉帧时别让角色瞬移一大截；0.05s 约等于 20fps，已经足够覆盖正常卡顿。
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _portraitTime += dt;

            float breath = Mathf.Sin(_portraitTime * 1.30f);
            float sway = Mathf.Sin(_portraitTime * 0.58f + 0.35f);
            float bob = Mathf.Sin(_portraitTime * 0.87f + 1.15f);

            // 幅度刻意压得很小：140×179 的框里，1px 的位移已经能看出“活着”，
            // 再大就会像在框里游泳，和旁边的静态 UI 打架。
            _portraitRect.anchoredPosition = _portraitHome + new Vector2(sway * 0.85f, bob * 1.15f);
            _portraitRect.localScale = new Vector3(1f - breath * 0.010f, 1f + breath * 0.014f, 1f);
            _portraitRect.localRotation = Quaternion.Euler(0f, 0f, sway * 0.75f);

            // 光晕/地灯跟着呼吸，人物和背景才像同一个光源下的整体。
            float pulse = 0.5f + 0.5f * Mathf.Sin(_portraitTime * 1.10f - 0.6f);
            if (_portraitGlow != null)
            {
                Color c = _portraitGlowBase;
                c.a *= 0.82f + 0.18f * pulse;
                _portraitGlow.color = c;
            }
            if (_portraitFloor != null)
            {
                Color c = _portraitFloorBase;
                c.a *= 0.78f + 0.22f * (1f - pulse);
                _portraitFloor.color = c;
            }

            TickHeroPortraitBlink(dt);
        }

        /// <summary>
        /// 两帧眨眼状态机：睁眼 → 闭眼约 0.11s → 睁眼；偶尔连眨两下。
        /// 重复间隔带随机，避免固定节拍看起来像 UI 呼吸灯。
        /// </summary>
        private void TickHeroPortraitBlink(float dt)
        {
            if (_portraitBlinkSprite == null) return;

            if (_portraitBlinkHold > 0f)
            {
                _portraitBlinkHold -= dt;
                if (_portraitBlinkHold > 0f) return;

                if (_portraitBlinksQueued > 0)
                {
                    _portraitBlinksQueued--;
                    _portraitImage.sprite = _portraitOpenSprite; // 两下之间要睁眼，否则就成了一次长眯眼
                    _portraitBlinkGap = 0.075f;
                    return;
                }

                _portraitImage.sprite = _portraitOpenSprite;
                _portraitBlinkTimer = GameRandom.Range(3.2f, 6.5f);
                return;
            }

            if (_portraitBlinkGap > 0f)
            {
                _portraitBlinkGap -= dt;
                if (_portraitBlinkGap <= 0f) CloseHeroPortraitEyes(0.095f);
                return;
            }

            _portraitBlinkTimer -= dt;
            if (_portraitBlinkTimer <= 0f)
            {
                CloseHeroPortraitEyes(0.11f);
                _portraitBlinksQueued = GameRandom.Value < 0.20f ? 1 : 0;
            }
        }

        private void CloseHeroPortraitEyes(float holdSeconds)
        {
            _portraitBlinkHold = holdSeconds;
            _portraitImage.sprite = _portraitBlinkSprite;
        }
    }
}
