using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;
    public SaveData saveData;

    public bool loadRequested = false;
    public bool HasPendingRunLoad => loadRequested && saveData != null && saveData.isValid;
    public static bool pendingAutoLoad = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            if (pendingAutoLoad)
            {
                loadRequested = true;
                LoadGame();
                pendingAutoLoad = false;
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SaveGame(SaveData data)
    {
        if (data == null) return;
        data.isValid = true;
        saveData = data;

        string timeStamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filePath = Path.Combine(Application.persistentDataPath, "SaveData.json");
        string backupPath = Path.Combine(Application.persistentDataPath, $"SaveData_{timeStamp}.json");

        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, json);
            File.WriteAllText(backupPath, json); // 최근 백업 남김
            Debug.Log($"게임 저장 완료: {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"게임 저장 실패: {ex.Message}");
        }
    }

    public void LoadGame()
    {
        string filePath = Path.Combine(Application.persistentDataPath, "SaveData.json");
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                saveData = JsonUtility.FromJson<SaveData>(json);
                if (saveData != null) saveData.isValid = true;
                Debug.Log("게임 로드 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"저장 파일을 읽는 중 오류: {ex.Message}");
                saveData = null;
            }
        }
        else
        {
            Debug.LogError("저장 파일이 존재하지 않습니다.");
        }
    }

    public void RequestLoadOnNextScene()
    {
        loadRequested = true;
        pendingAutoLoad = true;
    }

    public void CancelLoadRequest()
    {
        loadRequested = false;
        pendingAutoLoad = false;
    }
}
