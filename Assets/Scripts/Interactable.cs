using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
[RequireComponent (typeof(AudioSource))]
public class Interactable : MonoBehaviour
{
    [Header("设置")]
    [Tooltip("长按所需时间 0为瞬间触发即为点按")]
    public float requireDuration = 1f;
    [Tooltip("是否允许重复触发")]
    public bool triggerOnce = false;

    [Header("核心事件回调")]
    //把物体自己传出 在时间达标 且 松开鼠标时触发
    public UnityEvent<Transform> onTrigger;

    [Header("视觉/听觉 反馈")]
    [Header("长按进度条")]
    public GameObject ProgressBar;
    [Tooltip("移入")]
    public UnityEvent onHoverEnter;
    [Tooltip("移出")]
    public UnityEvent onHoverExit;
    [Tooltip("按下 蓄力")]
    public UnityEvent onDown;
    [Tooltip("抬起(进行重置 私有变量)")]
    public UnityEvent onUp;
    [Tooltip("长按进度")]
    public UnityEvent<float> onProgress;

    [Header("音效")]
    public AudioClip hoverEnterSound;
    public AudioClip downSound;
    public AudioClip successSound;

    [Tooltip("循环音效")]
    public AudioClip chargingSound;
    

    //内部状态
    private bool _isPressed;
    public bool _isHovering;
    private bool _hasTriggered;
    private float _timer;

    private CanvasGroup _progressImage;
    private RectTransform _barRect;
    private RectTransform _parentRect;
    private Canvas _rootCanvas;

    private void Awake()
    {
       

        if(ProgressBar != null)
        {
            _progressImage = ProgressBar.GetComponent<CanvasGroup>();
            _barRect = ProgressBar.GetComponent<RectTransform>();
            _parentRect = ProgressBar.transform.parent.GetComponent<RectTransform>(); 
            _rootCanvas = ProgressBar.GetComponentInParent<Canvas>();
            _progressImage.alpha = 0f;
            ProgressBar.transform.localScale = Vector3.zero;
            _progressImage.blocksRaycasts = false;
        }

       
    }

    /// <summary>
    /// 鼠标移入触发
    /// </summary>
    private void OnMouseEnter()
    {
        if (_hasTriggered && triggerOnce) return;
        _isHovering = true;
        onHoverEnter?.Invoke();

        AudioManager.Instance.PlaySFX(hoverEnterSound);

    }

    private void OnMouseExit()
    {
        _isHovering = false;
        onHoverExit?.Invoke();
        //如果按住的时候移除了  责 重置 进度
        if (_isPressed)
        {
            ResetState();
        }
    }
    //按下 开始计时
    private void OnMouseDown()
    {
        onProgress?.Invoke(0f);//重置进度
        if (_hasTriggered && triggerOnce) return;
        _isPressed = true;
        _timer = 0;
        onDown?.Invoke();

        AudioManager.Instance.PlaySFX(downSound);
        if (chargingSound != null)
        {
            AudioManager.Instance.PlayLoop(chargingSound);
        }

        ShowProgressBar();
        UpdateUIFollowMouse();
    }

    private void Update()
    {
        if (_isPressed && _isHovering)//鼠标在按下 以及 在物体表面时
        {
            _timer += Time.deltaTime;
            float progress = (requireDuration>0)?Mathf.Clamp01(_timer / requireDuration):1f;//意为  如果是长按 计算 当前百分比(长按的进度)  mathf.clamp01 ( 把 结果限制在0~1)
            onProgress?.Invoke(progress);

            UpdateUIFollowMouse();
        }
    }

    private void OnMouseUp()
    {
        _isPressed = false;

        AudioManager.Instance.StopLoop(chargingSound);

        //成功触发 条件:鼠标 得按在物体上 && 按住的时间得超过设定值
        if (_isHovering &&_timer >= requireDuration)
        {
            if(!(_hasTriggered && triggerOnce))
            {
                ExecuteTrigger();
            }
            else
            {
                //失败
            }

            onUp?.Invoke();
            

        }
        HideProgressBar();


    }

    void ExecuteTrigger()
    {
        _hasTriggered = true;
        onTrigger?.Invoke(this.transform);
        AudioManager.Instance.PlaySFX(successSound);
    }

    void ResetState()
    {
        _isPressed = false;
        _timer = 0f;
        onProgress?.Invoke(0f);
        AudioManager.Instance.StopLoop(chargingSound);
    }

  

    private void UpdateUIFollowMouse()
    {
        if (_barRect == null || _parentRect == null || _rootCanvas == null)return;
        Vector2 localPos;
        Camera cam = (_rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)?null:Camera.main;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentRect,
                Input.mousePosition,
                cam,
                out localPos
            );
        _barRect.localPosition = localPos;
    }

    private void ShowProgressBar()
    {
        if(_progressImage == null) return;
        _progressImage.DOKill();
        ProgressBar.transform.DOKill();

        _progressImage.DOFade(1, 0.3f);
        ProgressBar.transform.localScale = 1.5f * Vector3.one;
        //ProgressBar.transform.DOScale(1.5f,0.5f).SetEase(Ease.InBounce);

    }

    private void HideProgressBar()
    {
        if( _progressImage == null) return;
        _progressImage.DOKill();
        ProgressBar.transform.DOKill();

        _progressImage.DOFade(0, 1.2f);
        ProgressBar.transform.DOScale(0f, 1f).SetEase(Ease.OutBounce);

    }

}
