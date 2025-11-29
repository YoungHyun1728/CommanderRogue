using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;    // instance를 static으로 선언해 전역에서 사용가능

    void Awake()
    {
        if(instance != null)
        {
            Destroy(instance);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject); // 씬 이동 후에도 파괴되지 않음
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }
}
