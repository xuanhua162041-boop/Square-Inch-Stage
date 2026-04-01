using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    [Header("基础交互配置")]
    public float requireDuration = 1f;
    public bool triggerOnce = false;
    public string hoverText = "长按交互";

    [Header("进度条配置")]
    public GameObject ProgressBar;
    public float progressBarInitScale = 1.5f;

    [Header("事件回调")]
    public UnityEvent<Transform> onTrigger;
    public UnityEvent onHoverEnter;
    public UnityEvent onHoverExit;
    public UnityEvent onDown;
    public UnityEvent onUp;
    public UnityEvent<float> onProgress;

    [Header("音效配置")]
    public AudioClip hoverEnterSound;
    public AudioClip downSound;
    public AudioClip successSound;
    public AudioClip CountdownFinished;
    public AudioClip chargingSound;

    public bool _isPressed;
    public bool _isHovering;
    private bool _hasTriggered;
    private bool _hasPlayedCountdownFinished;
    private float _timer;

    private CanvasGroup _progressBarCanvasGroup;
    private RectTransform _progressBarRect;
    private RectTransform _progressBarParentRect;
    private Canvas _progressBarRootCanvas;

    private void Awake()
    {
        InitProgressBar();
    }

    private void InitProgressBar()
    {
        if (ProgressBar == null) return;

        _progressBarCanvasGroup = ProgressBar.GetComponent<CanvasGroup>();
        _progressBarRect = ProgressBar.GetComponent<RectTransform>();
        _progressBarParentRect = ProgressBar.transform.parent.GetComponent<RectTransform>();
        _progressBarRootCanvas = ProgressBar.GetComponentInParent<Canvas>();

        if (_progressBarCanvasGroup != null)
        {
            _progressBarCanvasGroup.alpha = 0f;
            _progressBarCanvasGroup.blocksRaycasts = false;
        }
        ProgressBar.transform.localScale = Vector3.zero;
    }

    private void OnMouseEnter()
    {
        if (_hasTriggered && triggerOnce) return;

        _hasPlayedCountdownFinished = false;
        _isHovering = true;
        onHoverEnter?.Invoke();

        if (hoverEnterSound != null)
        {
            AudioManager.Instance?.PlaySFX(hoverEnterSound);
        }

        if (!string.IsNullOrEmpty(hoverText) && InteractUIManager.Instance != null)
        {
            InteractUIManager.Instance.ShowOverUI(gameObject, hoverText);
        }
    }

    private void OnMouseExit()
    {
        _isHovering = false;
        onHoverExit?.Invoke();

        if (_isPressed)
        {
            ResetState();
            HideProgressBar();
        }

        if (InteractUIManager.Instance != null)
        {
            InteractUIManager.Instance.HideOverUI();
        }
    }

    private void OnMouseDown()
    {
        if (_hasTriggered && triggerOnce) return;

        _hasPlayedCountdownFinished = false;
        _isPressed = true;
        _timer = 0f;
        onDown?.Invoke();
        onProgress?.Invoke(0f);

        if (downSound != null)
        {
            AudioManager.Instance?.PlaySFX(downSound);
        }

        if (chargingSound != null)
        {
            AudioManager.Instance?.PlayLoop(chargingSound);
        }

        if (requireDuration == 0) return;
        ShowProgressBar();
        UpdateProgressBarFollowMouse();
    }

    private void OnMouseUp()
    {
        _isPressed = false;
        AudioManager.Instance?.StopLoop(chargingSound);

        if (_isHovering && _timer >= requireDuration)
        {
            if (!(_hasTriggered && triggerOnce))
            {
                ExecuteTrigger();
            }
            onUp?.Invoke();
        }
        HideProgressBar();
    }

    private void Update()
    {
        if (_isPressed && _isHovering)
        {
            _timer += Time.deltaTime;
            float progress = requireDuration > 0 ? Mathf.Clamp01(_timer / requireDuration) : 1f;
            onProgress?.Invoke(progress);

            if (progress >= 1 && !_hasPlayedCountdownFinished)
            {
                _hasPlayedCountdownFinished = true;
                if (CountdownFinished != null)
                {
                    AudioManager.Instance?.PlaySFX(CountdownFinished);
                }
            }
            UpdateProgressBarFollowMouse();
        }
    }

    private void ExecuteTrigger()
    {
        _hasTriggered = true;
        onTrigger?.Invoke(this.transform);
        if (successSound != null)
        {
            AudioManager.Instance?.PlaySFX(successSound);
        }
    }

    private void ResetState()
    {
        _isPressed = false;
        _timer = 0f;
        onProgress?.Invoke(0f);
        AudioManager.Instance?.StopLoop(chargingSound);
    }

    private void UpdateProgressBarFollowMouse()
    {
        if (_progressBarRect == null || _progressBarParentRect == null || _progressBarRootCanvas == null) return;

        Vector2 localPos;
        Camera cam = _progressBarRootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _progressBarParentRect,
            Input.mousePosition,
            cam,
            out localPos
        );

        _progressBarRect.localPosition = localPos;
    }

    private void ShowProgressBar()
    {
        if (_progressBarCanvasGroup == null) return;

        _progressBarCanvasGroup.DOKill();
        ProgressBar.transform.DOKill();
        _progressBarCanvasGroup.DOFade(1f, 0.3f);
        ProgressBar.transform.localScale = progressBarInitScale * Vector3.one;
    }

    private void HideProgressBar()
    {
        if (_progressBarCanvasGroup == null) return;

        _progressBarCanvasGroup.DOKill();
        ProgressBar.transform.DOKill();
        _progressBarCanvasGroup.DOFade(0f, 1.2f);
        ProgressBar.transform.DOScale(Vector3.zero, 1f).SetEase(Ease.OutBounce);
    }
}