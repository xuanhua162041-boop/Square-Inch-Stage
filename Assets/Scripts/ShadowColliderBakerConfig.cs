using UnityEngine;

public class ShadowColliderBakerConfig : ScriptableObject
{
    [Header("ºæ±º¾«¶È")]
    [Range(0.005f, 0.5f)]
    public float worldPerPixel = 0.05f;

    [Header("Åö×²ÌåÅäÖÃ")]
    public bool convex = false;

    public static ShadowColliderBakerConfig Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Instance = Resources.Load<ShadowColliderBakerConfig>("ShadowColliderBakerConfig");
        if (Instance == null)
        {
            Instance = CreateInstance<ShadowColliderBakerConfig>();
        }
    }
}