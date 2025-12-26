using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DesklampController : MonoBehaviour
{
    public GameObject lampCon;
    public GameObject mainLight;
    public float rotationSpeed;


    private void FixedUpdate()
    {
        var dir = lampCon.transform.position - mainLight.transform.position;
        //mainLight.transform.LookAt(mainLight.transform);
        Quaternion rotation = Quaternion.LookRotation(dir);
        lampCon.transform.rotation = rotation;

    }

}
