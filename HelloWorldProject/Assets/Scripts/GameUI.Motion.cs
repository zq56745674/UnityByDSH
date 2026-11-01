using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 交互动效。主题定成「桌面实体卡牌」，反馈就不能只靠颜色：
    /// 按钮悬停时抬起 1px、按下时沉 2px，面板打开时淡入 + 从下方 8px 处落位。
    /// 全部用 anchoredPosition / CanvasGroup 实现，不改布局锚点，不碰 Scale（避免
    /// 改 pivot 后把大量写死坐标的调用点搞乱）。
    /// </summary>
    public static class UiMotion
    {
        /// <summary>给按钮挂上悬停/按下的位移反馈。重复调用不会叠加组件。</summary>
        public static void Attach(Button btn)
        {
            if (btn == null) return;
            if (btn.GetComponent<UiButtonFeel>() == null) btn.gameObject.AddComponent<UiButtonFeel>();
        }

        /// <summary>给面板挂上打开时的淡入。重复调用不会叠加组件。</summary>
        public static void PanelIn(GameObject panel)
        {
            if (panel == null) return;
            if (panel.GetComponent<UiPanelIn>() == null) panel.AddComponent<UiPanelIn>();
        }

        /// <summary>
        /// 自己接管淡入淡出的面板（横幅、成就吐司）用这个取 CanvasGroup：
        /// CreatePanel 已经挂了 UiPanelIn，它 Awake 时就加过 CanvasGroup，
        /// 再 AddComponent 一次会返回 null（Unity 报 already added），
        /// 后续设置 alpha 直接 NRE。这里顺手把 UiPanelIn 摘掉，免得两套动画互相打架。
        /// </summary>
        public static CanvasGroup GrabCanvasGroup(GameObject panel)
        {
            if (panel == null) return null;
            UiPanelIn auto = panel.GetComponent<UiPanelIn>();
            if (auto != null) Object.Destroy(auto);
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.AddComponent<CanvasGroup>();
            return group;
        }
    }

    /// <summary>
    /// 卡片与它的硬投影是两个兄弟节点（投影必须在下层，只能是兄弟不能是子节点），
    /// 所以卡片 SetActive(false) 时投影不会跟着消失——本组件负责同步开关与销毁，
    /// 否则所有隐藏浮层的投影都会留在屏幕上，叠成一块块黑影。
    /// </summary>
    public class UiCardShadow : MonoBehaviour
    {
        public GameObject shadow;

        private Image _image;
        private RectTransform _rt;
        private RectTransform _shadowRt;
        private CanvasGroup _group;
        private float _baseAlpha = 0.34f;
        private Vector2 _offset = new Vector2(4f, 6f);
        private float _lastAlpha = -1f;

        private void Awake()
        {
            _rt = transform as RectTransform;
            if (shadow != null) Bind();
        }

        /// <summary>投影是兄弟节点：记下它的底色透明度与相对偏移，之后跟着卡片一起淡入/落位。</summary>
        private void Bind()
        {
            _image = shadow.GetComponent<Image>();
            _shadowRt = shadow.transform as RectTransform;
            if (_image != null) _baseAlpha = _image.color.a;
            if (_rt != null && _shadowRt != null && _shadowRt.parent == _rt.parent)
                _offset = _shadowRt.anchoredPosition - _rt.anchoredPosition;
        }

        private void OnEnable()
        {
            if (shadow != null)
            {
                if (_shadowRt == null) Bind();
                shadow.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (shadow != null) shadow.SetActive(false);
        }

        private void OnDestroy()
        {
            if (shadow != null) Destroy(shadow);
        }

        private void LateUpdate()
        {
            if (shadow == null) return;
            if (_shadowRt == null) Bind();

            // 面板入场是「淡入 + 从下方 8px 落位」：投影必须同步，否则淡入的头 0.16 秒
            // 会看到一块不透明的黑影先到位、纸卡还半透明地浮在上面。
            if (_rt != null && _shadowRt != null)
            {
                Vector2 want = _rt.anchoredPosition + _offset;
                if ((_shadowRt.anchoredPosition - want).sqrMagnitude > 0.0004f)
                    _shadowRt.anchoredPosition = want;
            }

            if (_group == null) _group = GetComponent<CanvasGroup>();
            float a = _group != null ? _group.alpha : 1f;
            if (Mathf.Abs(a - _lastAlpha) > 0.004f)
            {
                _lastAlpha = a;
                if (_image != null)
                {
                    Color c = _image.color;
                    c.a = _baseAlpha * a;
                    _image.color = c;
                }
            }
        }
    }

    /// <summary>按钮的指针反馈：悬停抬起 1px，按下沉 2px。</summary>
    public class UiButtonFeel : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform _rt;
        private Vector2 _base;
        private bool _captured;
        private bool _hover;
        private bool _pressed;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            Capture();
        }

        private void Capture()
        {
            if (_captured || _rt == null) return;
            _base = _rt.anchoredPosition;
            _captured = true;
        }

        private void OnDisable()
        {
            if (_captured && _rt != null) _rt.anchoredPosition = _base;
            _hover = false;
            _pressed = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hover = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            _pressed = false;
            Apply();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            Apply();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            Apply();
        }

        private void Apply()
        {
            if (!_captured || _rt == null) return;
            float dy = 0f;
            if (_pressed) dy = 2f;        // y 向下为正：按下沉 2px
            else if (_hover) dy = -1f;    // 悬停抬高 1px
            _rt.anchoredPosition = _base + new Vector2(0f, dy);
        }
    }

    /// <summary>面板入场：淡入 + 从下方 8px 落位（0.16s）。面板 SetActive(true) 时重放。</summary>
    public class UiPanelIn : MonoBehaviour
    {
        private const float Duration = 0.16f;
        private CanvasGroup _group;
        private RectTransform _rt;
        private Vector2 _base;
        private float _t;

        private void Awake()
        {
            _rt = transform as RectTransform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            if (_rt != null) _base = _rt.anchoredPosition;
        }

        private void OnEnable()
        {
            if (_group == null) return;
            _t = 0f;
            _group.alpha = 0f;
            if (_rt != null) _rt.anchoredPosition = _base + new Vector2(0f, 8f);
        }

        private void Update()
        {
            if (_group == null) return;
            if (_t >= Duration) return;
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_t / Duration);
            // 先快后慢，收尾贴住
            float e = 1f - (1f - k) * (1f - k);
            _group.alpha = e;
            if (_rt != null) _rt.anchoredPosition = _base + new Vector2(0f, 8f * (1f - e));
            if (_t >= Duration && _rt != null) _rt.anchoredPosition = _base;
        }
    }
}
