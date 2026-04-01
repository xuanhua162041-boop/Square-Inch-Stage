using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class TimeFreezeSFXController : MonoBehaviour
{
    private const float ShowIntensity = 1.66f;
    private const float HideIntensity = 5f;

    private Image image;
    private Material mat;

    private void Start()
    {
        // 获取Image组件
        image = GetComponent<Image>();
        if (image == null)
        {
            Debug.LogError("当前物体未挂载Image组件！", this);
            return;
        }

        // 从Image组件中获取它使用的材质（这才是正确的方式）
        mat = image.material;
        if (mat == null)
        {
            Debug.LogError("Image组件没有绑定材质！", this);
            return;
        }
        mat.DOFloat(HideIntensity, "_Intensity", 0.1f)
          .onComplete = () =>
          {
              image.enabled = false;
          };
       
    }

    public void ShowTimeFreezeSFX()
    {
        // 空值防护
        if (image == null || mat == null) return;

        image.enabled = true;
        mat.DOFloat(ShowIntensity, "_Intensity", 1.5f);
    }

    public void HideTimeFreezeSFX()
    {
        // 空值防护
        if (image == null || mat == null) return;

        mat.DOFloat(HideIntensity, "_Intensity", 2f)
           .onComplete = () =>
           {
               image.enabled = false;
           };
    }
}