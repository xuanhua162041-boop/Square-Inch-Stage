using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class HangingString : MonoBehaviour
{
    public GameObject ParticleObj;
    private LineRenderer _line;
    private BoxCollider _targetCollider;

    private Vector3 _fixedAnchorPos;
    private bool _isCut = false;
    private bool _isInitialized = false;

    private void Awake()
    {
        ParticleObj.active = false;
        _line = GetComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.useWorldSpace = true;
        _line.startWidth = 0.2f;
        _line.endWidth = 0.2f;
        _line.enabled = false;
    }

    public void Init(float ceilingY)
    {
        _targetCollider = GetComponent<BoxCollider>();
        float centerX = GetWorldCenter().x;
        _fixedAnchorPos = new Vector3(centerX, ceilingY, transform.position.z+0.1f);
        _isInitialized = true;
        _line.enabled = true;
        UpdateLinePositions();
    }

    private void LateUpdate()
    {
        if (!_isCut && _isInitialized)
        {
            UpdateLinePositions();
        }
    }

    Vector3 GetWorldCenter()
    {
        if (_targetCollider != null)
            return transform.TransformPoint(_targetCollider.center);
        return transform.position;
    }

    void UpdateLinePositions()
    {
        _line.SetPosition(0, _fixedAnchorPos);
        _line.SetPosition(1, GetWorldCenter());
    }

    public void Cut()
    {
        if (_isCut) return;
        _isCut = true;

        // 获取当前底端的位置
        Vector3 currentEnd = _line.GetPosition(1);

        // 【核心修改】回缩动画
        // 0.2秒缩回，使用 InBack 增加弹力感（崩断瞬间先往下沉一下）
        DOTween.To(() => currentEnd, x => {
            _line.SetPosition(1, x);
        }, _fixedAnchorPos, 0.5f)
        .SetEase(Ease.InBack)
        .OnComplete(() => {
            if (this != null && _line != null) _line.enabled = false;
        });

        ParticleObj.active = true;
    }
}