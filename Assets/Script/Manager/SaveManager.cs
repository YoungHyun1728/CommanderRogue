using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;
    public SaveData saveData;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SaveGame()
    {
        string timeStamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filePath = Application.persistentDataPath + "/SaveData_" + timeStamp + ".json";
        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(Application.persistentDataPath + "/SaveData.json", json);
        Debug.Log("게임 저장 완료: " + Application.persistentDataPath + "/SaveData.json");
    }

    public void LoadGame()
    {
        string filePath = Application.persistentDataPath + "/SaveData.json";
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            saveData = JsonUtility.FromJson<SaveData>(json);
            Debug.Log("게임 로드 완료");
        }
        else
        {
            Debug.LogError("저장 파일이 존재하지 않습니다.");
        }
    }
}
