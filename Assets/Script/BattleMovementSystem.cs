using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-5)]
public class BattleMovementSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TileMapManager tileMapManager;

    [Header("Tick")]
    [SerializeField] private float tickInterval = 0.12f;
    private float tickTimer;

    private readonly List<UnitGridAgent> agents = new();

    public void Register(UnitGridAgent agent)
    {
        if (agent != null && !agents.Contains(agent))
            agents.Add(agent);
    }

    public void Unregister(UnitGridAgent agent)
    {
        agents.Remove(agent);
    }

    private void Update()
    {
        tickTimer += Time.deltaTime;
        if (tickTimer < tickInterval) return;
        tickTimer = 0f;

        Step();
    }

    private void Step()
    {
        if (tileMapManager == null) return;

        // 1) Intent 수집
        var intents = new List<MoveIntent>(agents.Count);
        for (int i = 0; i < agents.Count; i++)
        {
            var a = agents[i];
            if (a == null) continue;
            if (a.TryBuildIntent(out MoveIntent intent))
                intents.Add(intent);
        }

        if (intents.Count == 0) return;

        // 2) 목적지별 그룹
        var byDest = new Dictionary<Vector2Int, List<MoveIntent>>();
        foreach (var it in intents)
        {
            if (!byDest.TryGetValue(it.to, out var list))
                byDest[it.to] = list = new List<MoveIntent>();
            list.Add(it);
        }

        // 3) 목적지마다 승자 선정(아직 적용 X)
        var approvedMoves = new List<MoveIntent>();
        var willFree = new HashSet<Vector2Int>(); // 이번 틱에 비워질 from 타일

        foreach (var kv in byDest)
        {
            var contenders = kv.Value;

            contenders.Sort((a, b) =>
            {
                int p = b.priority.CompareTo(a.priority); 
                if (p != 0) return p;
                return b.unitId.CompareTo(a.unitId);
            });

            var winner = contenders[0];

            // from이 지금도 내가 점유(-1) 중이어야 함
            if (tileMapManager.GetTileStatus(winner.from) != -1)
                continue;

            approvedMoves.Add(winner);
            willFree.Add(winner.from);
        }

        if (approvedMoves.Count == 0) return;

        // 4) 목적지 검증:
        // dest가 점유(-1)인데, 그 칸이 이번 틱에 비워질 예정이 아니면 이동 불가
        bool changed;
        do
        {
            changed = false;

            // 현재 approvedMoves 기준으로 willFree 재계산
            willFree.Clear();
            for (int k = 0; k < approvedMoves.Count; k++)
                willFree.Add(approvedMoves[k].from);

            for (int i = approvedMoves.Count - 1; i >= 0; i--)
            {
                var mv = approvedMoves[i];
                int destStatus = tileMapManager.GetTileStatus(mv.to);

                // dest가 점유(-1)인데, 그 점유자가 이번 틱에 실제로 빠지지 않으면 컷
                if (destStatus == -1 && !willFree.Contains(mv.to))
                {
                    mv.agent.NotifyMoveRejected();   // 있으면 호출(없으면 빼도 됨)
                    approvedMoves.RemoveAt(i);
                    changed = true;
                }
            }
        }
        while (changed);

        if (approvedMoves.Count == 0) return;

        // 5) 동시 적용 1단계: from 모두 비우기
        foreach (var mv in approvedMoves)
            tileMapManager.SetTileStatus(mv.from, 0);

        // 6) 동시 적용 2단계: dest 모두 점유
        foreach (var mv in approvedMoves)
            tileMapManager.SetTileStatus(mv.to, -1);

        // 7) 실제 이동(비주얼 이동) 시작
        foreach (var mv in approvedMoves)
            mv.agent.CommitMove(mv.to);
    }

    public struct MoveIntent
    {
        public int unitId;
        public double priority;
        public Vector2Int from;
        public Vector2Int to;
        public UnitGridAgent agent;
    }
}
