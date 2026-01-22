using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class FallingTextEmitter : MonoBehaviour
{
    [Header("吊线设置")]
    public float stringLength = 10f; // 线有多长

    [Header("必需资源")]
    public GameObject charPrefab; // 必须挂载 HangingString 和 AutoFitCollider 的预制体

    [Header("物理与动画")]
    public float dropHeight = 5f;
    public float dropDuration = 0.5f;
    public Vector3 initialForce = new Vector3(0, -1, 0);

    // Shader 属性 ID 缓存
    private static readonly int DilateId = Shader.PropertyToID("_FaceDilate");
    private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");

    private TMP_Text _templateText;
    private Material _baseMaterial;

    private void Awake()
    {
        _templateText = GetComponent<TMP_Text>();
        if (_templateText.fontSharedMaterial != null)
            _baseMaterial = _templateText.fontSharedMaterial;

        _templateText.enabled = false;

        // 【关键】必须开启富文本，否则模版无法解析 <color> 等标签
        _templateText.richText = true;
    }

    public void Speak(double clipDuration)
    {
        // 1. 【核心修复】参数 true 表示：即使组件被 disable 了，也要强制立即更新！
        // 否则隐藏状态下的 TMP 字数为 0，导致不生成。
        _templateText.ForceMeshUpdate(true);

        int charCount = _templateText.textInfo.characterCount;

        if (charCount == 0)
        {
            Debug.LogWarning("FallingTextEmitter: 未检测到有效字符。");
            return;
        }

        float calculatedInterval = (float)clipDuration / charCount;
        calculatedInterval = Mathf.Max(calculatedInterval, 0.01f);

        StartCoroutine(ProcessSentenceRoutine(calculatedInterval));
    }

    IEnumerator ProcessSentenceRoutine(float interval)
    {
        List<HangingString> activeStrings = new List<HangingString>();

        // 【材质准备】底色设为纯白，以便叠加富文本的顶点颜色
        Material localMaterial = new Material(_baseMaterial);
        localMaterial.SetFloat(DilateId, 0f);
        localMaterial.SetColor(FaceColorId, Color.white);

        List<Rigidbody> localRigidbodies = new List<Rigidbody>();
        Vector3 currentPos = transform.position;

        TMP_TextInfo textInfo = _templateText.textInfo;

        // --- 1. 逐字生成阶段 ---
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo info = textInfo.characterInfo[i];

            // 跳过不可见字符
            if (!info.isVisible)
            {
                if (char.IsWhiteSpace(info.character))
                {
                    currentPos += transform.right * (info.xAdvance * transform.localScale.x);
                    yield return new WaitForSeconds(interval);
                }
                continue;
            }

            // ================= 【颜色获取逻辑】 =================
            Color32 parsedColor = Color.white;

            int matIndex = info.materialReferenceIndex;
            int vertIndex = info.vertexIndex;

            // 安全检查：防止 meshInfo 数组越界或未初始化
            if (textInfo.meshInfo != null && matIndex < textInfo.meshInfo.Length)
            {
                TMP_MeshInfo meshInfo = textInfo.meshInfo[matIndex];
                if (meshInfo.colors32 != null && vertIndex < meshInfo.colors32.Length)
                {
                    // 从网格数据中提取富文本颜色
                    parsedColor = meshInfo.colors32[vertIndex];
                }
            }
            // =================================================

            // 获取样式和字号
            FontStyles parsedStyle = info.style;
            float parsedSize = info.pointSize;

            SpawnSingleChar(info.character, parsedColor, parsedStyle, parsedSize, localMaterial, localRigidbodies, activeStrings, ref currentPos);

            yield return new WaitForSeconds(interval);
        }

        // --- 2. 阅读等待 ---
        yield return new WaitForSeconds(0.5f);

        // --- 3. 剪断 + 掉落 (带节奏感) ---
        for (int i = 0; i < localRigidbodies.Count; i++)
        {
            // A. 剪断绳子 (触发回缩动画)
            if (i < activeStrings.Count && activeStrings[i] != null)
                activeStrings[i].Cut();

            // B. 【悬停】绳子缩回去了，字在空中愣 0.15秒
            yield return new WaitForSeconds(0.15f);

            // C. 物理掉落
            if (localRigidbodies[i] != null)
            {
                // 【关键】必须先杀掉 Sway 摇摆动画，否则物理引擎会被动画卡住
                localRigidbodies[i].transform.DOKill();

                localRigidbodies[i].useGravity = true;
                localRigidbodies[i].AddForce(initialForce, ForceMode.Impulse);
            }

            // D. 随机间隔
            yield return new WaitForSeconds(Random.Range(0.05f, 0.1f));
        }

        yield return new WaitForSeconds(1f);

        // --- 4. 消失动画 (腐蚀 + 透明) ---
        Sequence seq = DOTween.Sequence();
        seq.Append(localMaterial.DOFloat(-1f, DilateId, 1.5f).SetEase(Ease.InQuint));
        seq.Join(localMaterial.DOColor(new Color(1, 1, 1, 0), FaceColorId, 1.5f).SetEase(Ease.InQuint));

        yield return seq.WaitForCompletion();

        // --- 5. 清理 ---
        foreach (var rb in localRigidbodies) if (rb != null) Destroy(rb.gameObject);
        if (localMaterial != null) Destroy(localMaterial);
    }

    private void SpawnSingleChar(char c, Color32 color, FontStyles style, float fontSize, Material mat, List<Rigidbody> rbs, List<HangingString> strings, ref Vector3 pos)
    {
        GameObject obj = Instantiate(charPrefab, pos, Quaternion.identity);
        obj.transform.SetParent(this.transform.parent);
        obj.transform.localScale = transform.localScale;

        TMP_Text tmp = obj.GetComponent<TMP_Text>();
        tmp.text = c.ToString();
        tmp.font = _templateText.font;

        // 【应用富文本属性】
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;

        tmp.fontSharedMaterial = mat;
        tmp.enableAutoSizing = false;

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rbs.Add(rb);

        // 适配碰撞体
        float charWidth = tmp.preferredWidth;
        AutoFitCollider fitter = obj.GetComponent<AutoFitCollider>();
        if (fitter != null) charWidth = fitter.UpdateColliderSize();

        // 初始化吊线
        HangingString hs = obj.GetComponent<HangingString>();
        if (hs != null)
        {
            hs.Init(pos.y + stringLength);
            strings.Add(hs);
        }

        pos += transform.right * charWidth;

        // --- 动画部分 ---

        // 1. 入场下落
        obj.transform.DOMoveY(dropHeight, dropDuration).From(isRelative: true).SetEase(Ease.OutBack);
        tmp.alpha = 0;
        tmp.DOFade(1, dropDuration);

        // 2. 微风摇曳 (Sway) - 模拟绳索悬挂感
        float initRotation = Random.Range(-3f, 3f);
        obj.transform.Rotate(0, 0, initRotation);

        float swayAngle = Random.Range(2f, 4f);
        float swayDuration = Random.Range(2f, 3f);

        obj.transform.DORotate(new Vector3(0, 0, initRotation + swayAngle), swayDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }
}