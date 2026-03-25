using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if PLAYFAB_SDK_PRESENT
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.DataModels;
using EntityKeyPF = PlayFab.DataModels.EntityKey;
using PlayFab.Json;
#endif

/// <summary>
/// 챌린지 파티 스냅샷을 보관/업로드하는 서비스.
/// </summary>
public static class ChallengeService
{
    private const string FileName = "challenge_parties.json";

    public static string LocalPath =>
        Path.Combine(Application.persistentDataPath, FileName);

    public static ChallengePartySnapshot GetLatestSnapshot(string excludeCreatorId = null)
    {
        var list = LoadLocalList();
        if (list == null || list.Count == 0) return null;

        list.Sort((a, b) => string.Compare(b.createdAtIsoUtc, a.createdAtIsoUtc, StringComparison.Ordinal));
        foreach (var snap in list)
        {
            if (!string.IsNullOrEmpty(excludeCreatorId) &&
                !string.IsNullOrEmpty(snap.creatorId) &&
                snap.creatorId == excludeCreatorId)
                continue;
            return snap;
        }

        return list[0];
    }

    public static ChallengePartySnapshot GetLatestSnapshotExceptParty(string excludePartyId, string excludeCreatorId = null)
    {
        var list = LoadLocalList();
        if (list == null || list.Count == 0) return null;

        list.Sort((a, b) => string.Compare(b.createdAtIsoUtc, a.createdAtIsoUtc, StringComparison.Ordinal));
        foreach (var snap in list)
        {
            if (!string.IsNullOrEmpty(excludePartyId) && snap.partyId == excludePartyId)
                continue;
            if (!string.IsNullOrEmpty(excludeCreatorId) &&
                !string.IsNullOrEmpty(snap.creatorId) &&
                snap.creatorId == excludeCreatorId)
                continue;
            return snap;
        }

        return null;
    }

    public static void SaveLocal(ChallengePartySnapshot snapshot)
    {
        if (snapshot == null) return;

        var list = LoadLocalList();
        list.Add(snapshot);

        try
        {
            string json = JsonUtility.ToJson(new Wrapper { items = list }, prettyPrint: true);
            File.WriteAllText(LocalPath, json);
            Debug.Log($"[Challenge] 로컬 스냅샷 저장 완료: {LocalPath} (총 {list.Count}개)");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Challenge] 로컬 스냅샷 저장 실패: {ex.Message}");
        }
    }

    public static List<ChallengePartySnapshot> LoadLocalList()
    {
        try
        {
            if (!File.Exists(LocalPath)) return new List<ChallengePartySnapshot>();

            string json = File.ReadAllText(LocalPath);
            var wrapper = JsonUtility.FromJson<Wrapper>(json);
            return wrapper?.items ?? new List<ChallengePartySnapshot>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Challenge] 로컬 스냅샷 로드 실패: {ex.Message}");
            return new List<ChallengePartySnapshot>();
        }
    }

    /// <summary>
    /// PlayFab에 스냅샷을 업로드한다. (Legacy CloudScript Execute)
    /// </summary>
    public static void UploadToPlayFab(ChallengePartySnapshot snapshot, Action<bool, string> onComplete = null)
    {
#if PLAYFAB_SDK_PRESENT
        if (snapshot == null)
        {
            onComplete?.Invoke(false, "snapshot null");
            return;
        }

        if (string.IsNullOrEmpty(PlayFabSettings.staticSettings.TitleId))
        {
            Debug.LogError("[Challenge] PlayFab TitleId가 비어 있습니다. PlayFabSharedSettings.asset에서 TitleId를 설정하세요.");
            onComplete?.Invoke(false, "TitleId missing");
            return;
        }

        EnsureLoginAndToken(
            () =>
            {
                var req = new ExecuteCloudScriptRequest
                {
                    FunctionName = "SaveChallenge",
                    FunctionParameter = new Dictionary<string, object>
                    {
                        { "snapshot", snapshot }
                    },
                    GeneratePlayStreamEvent = false
                };

                PlayFabClientAPI.ExecuteCloudScript(req,
                    res =>
                    {
                        if (res.Error != null)
                        {
                            onComplete?.Invoke(false, res.Error.Message);
                            return;
                        }
                        Debug.Log($"[Challenge] PlayFab 업로드 완료: {snapshot.partyId} (CloudScript)");
                        onComplete?.Invoke(true, null);
                    },
                    err =>
                    {
                        Debug.LogError($"[Challenge] PlayFab 업로드 실패: {err.GenerateErrorReport()}");
                        onComplete?.Invoke(false, err.ErrorMessage);
                    });
            },
            err =>
            {
                Debug.LogError($"[Challenge] PlayFab 로그인 실패: {err}");
                onComplete?.Invoke(false, err);
            });
#else
        Debug.Log("[Challenge] PlayFab SDK 미포함. UploadToPlayFab는 스킵합니다.");
        onComplete?.Invoke(false, "PlayFab SDK not present");
#endif
    }

    [Serializable]
    private class Wrapper
    {
        public List<ChallengePartySnapshot> items = new();
    }

#if PLAYFAB_SDK_PRESENT
    private static void EnsureLoginAndToken(Action onReady, Action<string> onError)
    {
        if (IsLoggedIn())
        {
            onReady?.Invoke();
            return;
        }

        string customId = SystemInfo.deviceUniqueIdentifier;
        if (string.IsNullOrEmpty(customId))
            customId = Guid.NewGuid().ToString("N");

        var req = new LoginWithCustomIDRequest
        {
            TitleId = PlayFabSettings.staticSettings.TitleId,
            CustomId = customId,
            CreateAccount = true,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetUserAccountInfo = true
            }
        };

        PlayFabClientAPI.LoginWithCustomID(req,
            result =>
            {
                CacheEntityToken(result.EntityToken);
                onReady?.Invoke();
            },
            error =>
            {
                onError?.Invoke(error.GenerateErrorReport());
            });
    }

    private static bool IsLoggedIn()
    {
        return PlayFabSettings.staticPlayer != null &&
               !string.IsNullOrEmpty(PlayFabSettings.staticPlayer.EntityId) &&
               !string.IsNullOrEmpty(PlayFabSettings.staticPlayer.EntityToken);
    }

    private static void CacheEntityToken(EntityTokenResponse token)
    {
        if (token == null || token.Entity == null) return;
        PlayFabSettings.staticPlayer.EntityToken = token.EntityToken;
        PlayFabSettings.staticPlayer.EntityId = token.Entity.Id;
        PlayFabSettings.staticPlayer.EntityType = token.Entity.Type;
    }

    /// <summary>
    /// 서버(CloudScript)에서 챌린지 스냅샷 목록을 받아온다.
    /// 서버 스크립트는 전투력 오름차순으로 최대 50개를 내려주도록 구성되어 있음.
    /// </summary>
    public static void GetSnapshotListFromServer(string excludePartyId, string excludeCreatorId, Action<List<ChallengePartySnapshot>, string> onComplete)
    {
        // 현재 서버 스크립트에서 제외 파라미터는 사용하지 않지만,
        // 서명 호환성을 유지하기 위해 인자를 받아 둔다.
        _ = excludePartyId;
        _ = excludeCreatorId;

        if (!IsLoggedIn())
        {
            EnsureLoginAndToken(() => GetSnapshotListFromServer(excludePartyId, excludeCreatorId, onComplete), err => onComplete?.Invoke(null, err));
            return;
        }

        var req = new ExecuteCloudScriptRequest
        {
            FunctionName = "GetChallengeList",
            FunctionParameter = new Dictionary<string, object>(),
            GeneratePlayStreamEvent = false
        };

        PlayFabClientAPI.ExecuteCloudScript(req,
            res =>
            {
                try
                {
                    if (res.Error != null)
                    {
                        onComplete?.Invoke(null, res.Error.Message);
                        return;
                    }

                    string json = res.FunctionResult is string s
                        ? s
                        : PlayFabSimpleJson.SerializeObject(res.FunctionResult);

                    var list = PlayFabSimpleJson.DeserializeObject<List<ChallengePartySnapshot>>(json);
                    onComplete?.Invoke(list, list == null ? "parse failed" : null);
                }
                catch (System.Exception ex)
                {
                    onComplete?.Invoke(null, ex.Message);
                }
            },
            err => onComplete?.Invoke(null, err.GenerateErrorReport()));
    }

    /// <summary>
    /// 서버(CloudScript)에서 랜덤 챌린지 스냅샷을 받아온다. (호환용)
    /// </summary>
    public static void GetRandomSnapshotFromServer(string excludePartyId, string excludeCreatorId, Action<ChallengePartySnapshot, string> onComplete)
    {
        GetSnapshotListFromServer(excludePartyId, excludeCreatorId,
            (list, err) =>
            {
                if (list != null && list.Count > 0)
                    onComplete?.Invoke(list[0], err);
                else
                    onComplete?.Invoke(null, err ?? "empty list");
            });
    }
#endif
}
