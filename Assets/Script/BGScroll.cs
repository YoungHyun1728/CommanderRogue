using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BGScroll : MonoBehaviour
{
    public float scrollSpeed;  // 배경이 스크롤되는 속도
    public float resetPosition = -15f;   // 배경이 왼쪽으로 벗어나는 위치
    public float startPosition = 15f;   // 배경이 오른쪽에서 다시 시작하는 위치

    private Vector3 initialPosition;

    void Start()
    {
        scrollSpeed = Random.Range(1f, 3f);
        initialPosition = transform.position;
    }

    void Update()
    {
         // 배경을 왼쪽으로 이동
        transform.Translate(Vector3.left * scrollSpeed * Time.deltaTime);

       // 배경이 화면 왼쪽 끝을 벗어나면 위치를 오른쪽으로 재배치
        if (transform.position.x < resetPosition)
        {
            Vector3 newPosition = new Vector3(startPosition, transform.position.y, transform.position.z);
            transform.position = newPosition;
        }
    }
}
