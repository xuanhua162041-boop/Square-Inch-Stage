using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CamerBulr : MonoBehaviour
{
    Volume volume;
    DepthOfField ofField;
    float OffBulrFocusDistance =45.2f;
    float BulrFocusDistance =0.1f;
    float CurrentBulrValue = 0;
    private void Awake()
    {

        volume = GetComponent<Volume>();
        volume.profile.TryGet<DepthOfField>(out ofField);
        ofField.focusDistance.value = OffBulrFocusDistance;
        CurrentBulrValue = ofField.focusDistance.value;
        MemoryItem.CollectMemoryAction += BulrStart;
        MemoryCanvasUI.OnVideoEndAction += OffBulrStart;
    }
    public void BulrStart(MemoryType type)
    {
        StartCoroutine(BulrStartIE());
    }
    
    IEnumerator  BulrStartIE()
    {
        while (CurrentBulrValue>= BulrFocusDistance)
        {
            CurrentBulrValue -= 0.4f;
            ofField.focusDistance.value = CurrentBulrValue;
            yield return new WaitForSeconds(0.01f);
        }
        
    }
    public void OffBulrStart()
    {
        StartCoroutine (OffBulrStartIE());
    }

    IEnumerator OffBulrStartIE()
    {
        while (CurrentBulrValue <= OffBulrFocusDistance)
        {
            CurrentBulrValue += 0.4f;
            ofField.focusDistance.value = CurrentBulrValue;
            yield return new WaitForSeconds(0.01f);
        }

    }
    private void OnDestroy()
    {
        StopCoroutine(OffBulrStartIE());
        StopCoroutine(BulrStartIE());
    }
}
