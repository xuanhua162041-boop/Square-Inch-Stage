using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StarlightGuidance : MonoBehaviour
{
    private Animator animator;
    public bool playerNear = false;
    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
    }
    


    /*private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            Debug.Log("玩家已进入 memoryItem区域中!");
            animator.Play("ChaseTarget");
        }
    }*/
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            Debug.Log("玩家已进入 memoryItem区域中!");
            animator.Play("ChaseTarget");
        }
    }

    public void DesoryMe()
    {
        Destroy(this.gameObject);
    }
    
}

