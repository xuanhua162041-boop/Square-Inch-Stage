using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshTrailTut : MonoBehaviour
{
    public float activeTime = 1f;

    [Header("Mesh Related")]
    public float meshRefreshRate =0.1f;

    private bool isTrailActive;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isTrailActive = true;
            StartCoroutine(ActivateTrail(activeTime));

        }
    }

    IEnumerator ActivateTrail(float timeActive)
    {
        while (timeActive > 0)
        {
            timeActive -= meshRefreshRate;
            yield return new WaitForSeconds(meshRefreshRate);
        }
    }
}
