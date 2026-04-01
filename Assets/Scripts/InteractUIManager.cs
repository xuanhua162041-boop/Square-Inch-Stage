using DG.Tweening;
using UnityEngine;
using TMPro;

public class InteractUIManager : MonoBehaviour
{
    public static InteractUIManager Instance;

    [Header("UI基础配置")]
    public GameObject overUIPrefab;
    public Canvas uiRootCanvas;

    [Header("偏移配置（这次必生效！）")]
    [Tooltip("UI左右偏移距离（X轴，正数即可，越大偏移越远）")]
    public float overUIOffset = 50f;
    [Tooltip("UI上下偏移距离（Y轴，物体上方偏移）")]
    public float overUIYOffset = 1f;

    [Header("模式选择")]
    [Tooltip("true=根据物体位置决定UI方向；false=根据鼠标位置决定UI方向")]
    public bool useObjectPositionMode = true;

    // 全局UI组件
    private GameObject _globalOverUI;
    private RectTransform _overUIRect;
    private CanvasGroup _overUICanvasGroup;
    private RectTransform _canvasRect;
    private TextMeshProUGUI _hoverTextTMP;
    private GameObject _leftBg;
    private GameObject _rightBg;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        InitGlobalUI();
    }

    private void InitGlobalUI()
    {
        if (overUIPrefab == null || uiRootCanvas == null)
        {
            Debug.LogError("缺预制体或Canvas！");
            return;
        }

        _globalOverUI = Instantiate(overUIPrefab, uiRootCanvas.transform);
        _globalOverUI.name = "GlobalOverUI";

        // ========== 强制锚点为中心（X偏移生效的唯一前提！） ==========
        _overUIRect = _globalOverUI.GetComponent<RectTransform>();
        _overUIRect.anchorMin = new Vector2(0.5f, 0.5f);
        _overUIRect.anchorMax = new Vector2(0.5f, 0.5f);
        _overUIRect.pivot = new Vector2(0.5f, 0.5f);
        _overUIRect.localScale = Vector3.one;

        _overUICanvasGroup = _globalOverUI.GetComponent<CanvasGroup>();
        _canvasRect = uiRootCanvas.GetComponent<RectTransform>();

        // 查找子物体（严格匹配名称）
        _hoverTextTMP = _globalOverUI.transform.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>();
        _leftBg = _globalOverUI.transform.Find("Left")?.gameObject;
        _rightBg = _globalOverUI.transform.Find("Right")?.gameObject;

        HideOverUI();
    }

    /// <summary>
    /// 显示UI：传交互物体 + 文本
    /// </summary>
    public void ShowOverUI(GameObject interactObj, string text)
    {
        if (_globalOverUI == null || interactObj == null || string.IsNullOrEmpty(text))
        {
            HideOverUI();
            return;
        }

        _hoverTextTMP.text = text;

        // 核心：计算UI位置（带X/Y偏移 + 方向判断）
        UpdateUIPosition(interactObj);

        // 显示动画
        _overUICanvasGroup.DOFade(1, 0.3f);
        _globalOverUI.transform.DOScale(Vector3.one, 0.3f);
    }

    public void HideOverUI()
    {
        _overUICanvasGroup.DOFade(0, 0.3f);
        _globalOverUI.transform.DOScale(Vector3.zero, 0.3f);
        _leftBg?.SetActive(false);
        _rightBg?.SetActive(false);
    }

    /// <summary>
    /// 核心方法：计算UI最终位置（X偏移必生效）
    /// </summary>
    private void UpdateUIPosition(GameObject interactObj)
    {
        Vector2 targetScreenPos;
        Vector3 objWorldPos = interactObj.transform.position;
        objWorldPos.y += overUIYOffset; // Y轴偏移

        // ========== 模式1：根据物体位置判断方向 ==========
        if (useObjectPositionMode)
        {
            targetScreenPos = Camera.main.WorldToScreenPoint(objWorldPos);
        }
        // ========== 模式2：根据鼠标位置判断方向 ==========
        else
        {
            targetScreenPos = Input.mousePosition;
            // 可选：鼠标模式下，UI也跟着物体Y轴偏移（否则UI在鼠标Y位置）
            targetScreenPos.y = Camera.main.WorldToScreenPoint(objWorldPos).y;
        }

        // ========== 关键：屏幕中线（划分左右） ==========
        float screenMidX = Screen.width / 2f;
        bool isLeftSide = targetScreenPos.x < screenMidX;

        // ========== 屏幕坐标 → Canvas本地坐标 ==========
        Vector2 uiLocalPos;
        Camera cam = uiRootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, targetScreenPos, cam, out uiLocalPos
        );

        // ========== X轴偏移核心逻辑（这次绝对生效！） ==========
        if (isLeftSide)
        {
            // 目标在左半区 → UI在右侧 + X偏移
            uiLocalPos.x += overUIOffset;
            _rightBg?.SetActive(true);
            _leftBg?.SetActive(false);
        }
        else
        {
            // 目标在右半区 → UI在左侧 + X偏移
            uiLocalPos.x -= overUIOffset;
            _leftBg?.SetActive(true);
            _rightBg?.SetActive(false);
        }

        // ========== 终极关键：用anchoredPosition赋值！！！ ==========
        _overUIRect.anchoredPosition = uiLocalPos;

        // 调试日志（看X偏移是否真的加上了）
        Debug.Log($"偏移值：{overUIOffset} | 目标在左半区：{isLeftSide} | 最终X：{uiLocalPos.x}");
    }
}