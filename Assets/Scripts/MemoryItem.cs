using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MemoryType
{
    Cup,
    Fabric,
    Light,
    Paper
}
public class MemoryItem : MonoBehaviour
{
    [SerializeField]
    public MemoryType _memoryType;
    public static MemoryType memoryType;

    private Animator anim;
    
    public AudioClip CollectTip;
    public GameObject Fire;
    public static Action<MemoryType> CollectMemoryAction;

    public static void CollectMemory()
    {
        CollectMemoryAction?.Invoke(memoryType);
    }
    // Start is called before the first frame update
    void Start()
    {
        anim = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
    }
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("我进入了");
        if (other.gameObject.tag == "Player")
        {
            anim.Play("Dissolve");
            Fire.SetActive(true);
            AudioManager.Instance.PlaySFX(CollectTip);
        }
        
    }

    public void DestoryMe()
    {
        memoryType = _memoryType;
        CollectMemory();
        Destroy(this.gameObject);
    }
    
  

  
}
