using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InkSplatterEmitter : MonoBehaviour
{
    public GameObject inkPrefab;
    public LayerMask layerMask;
    List<GameObject>Pool = new List<GameObject>();
    GameObject obj;
    public GameObject Push()
    {
        if (Pool.Count<1)
        {
            obj = Instantiate(inkPrefab);
        }
        else
        {
            obj = Pool[0];
            Pool.RemoveAt(0);
        }
        obj.SetActive(true);
        StartCoroutine(recolect(obj));
        return obj;

    }
    IEnumerator recolect(GameObject obj)
    {
        yield return new WaitForSeconds(1f);
        returnPool(obj);
    }
    public void returnPool(GameObject obj)
    {
        Pool.Add(obj);
        obj.SetActive(false);
    }
    

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnCollisionEnter(Collision collision)
    {
        //if (collision.gameObject.layer !=layerMask )return;

        Debug.Log("已触发墨迹OnCollisionEnter");
        Vector3 v3 = collision.collider.ClosestPoint(this.transform.position);
        GameObject summonObject =Push();
        summonObject.transform.position = v3;
    }
}
