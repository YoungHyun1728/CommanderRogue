using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 챌린지 모드 전용 상태와 상대 스냅샷 수급 흐름을 담당한다.
/// RunManager는 전투/맵 상태 전환만 오케스트레이션하고,
/// 챌린지 관련 데이터/통신/큐 관리는 이 클래스로 분리한다.
/// </summary>
public sealed class RunChallengeCoordinator
{
    private bool challengeModeActive;
    private string lastUploadedPartyId = "";
    private string challengePartyName = "";
    private string challengeOpponentName = "";
    private ChallengePartySnapshot cachedChallengeSnapshot;
    private readonly Queue<ChallengePartySnapshot> challengeQueue = new Queue<ChallengePartySnapshot>();

    public bool ChallengeModeActive => challengeModeActive;
    public string ChallengePartyName => challengePartyName;
    public string ChallengeOpponentName => challengeOpponentName;

    /// <summary>
    /// 일반 런 시작 시 챌린지 관련 상태를 모두 초기화한다.
    /// </summary>
    public void ResetForNewRun()
    {
        challengeModeActive = false;
        lastUploadedPartyId = "";
        challengePartyName = "";
        challengeOpponentName = "";
        cachedChallengeSnapshot = null;
        challengeQueue.Clear();
    }

    public bool IsChallengeCombatNode(NodeType nodeType)
    {
        return challengeModeActive && nodeType == NodeType.Combat;
    }

    /// <summary>
    /// 현재 런의 파티를 챌린지 파티로 등록한다.
    /// </summary>
    public bool TryActivateFromCurrentParty(RunManager runManager, string partyName, out string errorMessage)
    {
        errorMessage = null;

        if (!string.IsNullOrEmpty(partyName) && partyName.Length > 10)
        {
            errorMessage = "파티 이름은 최대 10자까지 가능합니다.";
            return false;
        }

        try
        {
            var snapshot = ChallengeSnapshotBuilder.Build(runManager, partyName);
            ChallengeService.SaveLocal(snapshot);
            ChallengeService.UploadToPlayFab(snapshot);

            Debug.Log($"[Challenge] 파티 스냅샷 등록 완료. ID={snapshot.partyId}, Name={snapshot.partyName}");

            lastUploadedPartyId = snapshot.partyId;
            challengePartyName = snapshot.partyName;
            challengeOpponentName = "";
            challengeModeActive = true;
            cachedChallengeSnapshot = null;
            challengeQueue.Clear();

            return true;
        }
        catch (System.Exception ex)
        {
            errorMessage = $"[Challenge] 스냅샷 생성/저장 중 오류: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 큐/서버/로컬 폴백 순서로 다음 상대 스냅샷을 가져와 캐시한다.
    /// </summary>
    public IEnumerator FetchAndCacheNextOpponent()
    {
        cachedChallengeSnapshot = null;
        challengeOpponentName = "";

        // 1) 기존 큐에 쌓인 상대를 먼저 사용한다.
        if (challengeQueue.Count > 0)
        {
            cachedChallengeSnapshot = challengeQueue.Dequeue();
            challengeOpponentName = cachedChallengeSnapshot != null ? cachedChallengeSnapshot.partyName : "";
            Debug.Log($"[Challenge] 큐에서 다음 파티 사용: {challengeOpponentName} (남은 {challengeQueue.Count}개)");
            yield break;
        }

        // 2) 서버 목록 수신을 시도한다.
        bool done = false;
        string error = null;

        ChallengeService.GetSnapshotListFromServer(lastUploadedPartyId, null,
            (list, err) =>
            {
                error = err;
                done = true;

                if (list != null && list.Count > 0)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var snap = list[i];
                        if (snap == null) continue;
                        challengeQueue.Enqueue(snap);
                    }

                    if (challengeQueue.Count > 0)
                    {
                        cachedChallengeSnapshot = challengeQueue.Dequeue();
                        challengeOpponentName = cachedChallengeSnapshot.partyName;
                        Debug.Log(
                            $"[Challenge] 서버에서 {list.Count}개 수신. " +
                            $"첫 파티: {challengeOpponentName} (id={cachedChallengeSnapshot.partyId})");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Challenge] 서버 스냅샷 수신 실패: {err}");
                }
            });

        float wait = 0f;
        while (!done && wait < 3f)
        {
            wait += Time.deltaTime;
            yield return null;
        }

        // 3) 응답 지연/실패 시 큐를 한 번 더 확인한다.
        if (cachedChallengeSnapshot == null && challengeQueue.Count > 0)
        {
            cachedChallengeSnapshot = challengeQueue.Dequeue();
            challengeOpponentName = cachedChallengeSnapshot != null ? cachedChallengeSnapshot.partyName : "";
        }

        // 4) 서버 실패 시 로컬 최신 스냅샷으로 폴백한다.
        if (!done || cachedChallengeSnapshot == null)
        {
            cachedChallengeSnapshot = ChallengeService.GetLatestSnapshotExceptParty(lastUploadedPartyId, null);
            if (cachedChallengeSnapshot == null && !string.IsNullOrEmpty(lastUploadedPartyId))
                cachedChallengeSnapshot = ChallengeService.GetLatestSnapshotExceptParty(null, null);

            if (cachedChallengeSnapshot == null)
            {
                Debug.LogWarning("[Challenge] 사용할 스냅샷을 찾지 못했습니다. 기본 스폰으로 대체합니다.");
                challengeOpponentName = "";
            }
            else
            {
                challengeOpponentName = cachedChallengeSnapshot.partyName;
            }
        }
        else
        {
            // 서버 정상 수신 케이스에서도 이름을 명시적으로 동기화한다.
            challengeOpponentName = cachedChallengeSnapshot.partyName;
        }

        if (!string.IsNullOrEmpty(error))
            Debug.LogWarning($"[Challenge] 서버 응답 경고: {error}");
    }

    /// <summary>
    /// 캐싱된 상대 스냅샷이 있으면 챌린지 적 스폰을 실행한다.
    /// </summary>
    public bool TrySpawnChallengeEnemies(
        EnemySpawnManager enemySpawnManager,
        List<UnitData> challengeEnemyUnitPool,
        System.Func<string, Equipment> equipmentResolver)
    {
        if (!challengeModeActive || cachedChallengeSnapshot == null) return false;
        if (enemySpawnManager == null) return false;

        enemySpawnManager.SpawnChallengeEnemies(
            cachedChallengeSnapshot,
            null,
            challengeEnemyUnitPool,
            equipmentResolver);

        return true;
    }

    /// <summary>
    /// 디버그용: 현재 파티를 복제해 여러 개의 챌린지 스냅샷으로 업로드한다.
    /// </summary>
    public void DebugUploadChallengeCopies(RunManager runManager, int count)
    {
        if (runManager == null || count <= 0) return;

        var baseSnap = ChallengeSnapshotBuilder.Build(runManager, "DebugParty");
        for (int i = 0; i < count; i++)
        {
            var clone = JsonUtility.FromJson<ChallengePartySnapshot>(JsonUtility.ToJson(baseSnap));
            clone.partyId = System.Guid.NewGuid().ToString("N");
            clone.partyName = $"{baseSnap.partyName}_{i + 1}";
            ChallengeService.SaveLocal(clone);
            ChallengeService.UploadToPlayFab(clone);
        }

        Debug.Log($"[Challenge] Debug party {count}개 업로드 요청 완료");
    }
}
