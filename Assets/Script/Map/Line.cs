using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Line : MonoBehaviour
{
    public GameObject startNode;
    public GameObject endNode;
    void Update()
    {
        // 쓸모없어진 라인 삭제
        if (startNode == null || endNode == null)
        {
            Debug.Log("One of the nodes is null. Destroying the line.");
            Destroy(gameObject);
        }
    }
}
