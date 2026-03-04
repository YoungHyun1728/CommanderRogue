using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// RunManager 바이옴 로직 분리 (리플렉션 사용 안 함)
public partial class RunManager
{
    // ===== 지속형(파티) 원복을 위한 적용량 기록 =====
    private readonly Dictionary<Unit, float> _appliedMpRecoveryDelta = new();
    private readonly Dictionary<Unit, int> _appliedAttackRangeDelta = new();
    private readonly Dictionary<Unit, int> _appliedMaxMpDelta = new();

    // ===== 전투 시작 5초 버프/디버프 토큰(중복/겹침 안전) =====
    // 5초짜리 임시 배율은 전투 시작/효과 중첩/재호출 상황에서 꼬이기 쉽다.
    // 그래서 유닛별 "토큰 번호"를 저장해두고,
    // 코루틴이 끝날 때 현재 토큰과 일치할 때만 원복하도록 해서
    // "나중에 걸린 효과가 먼저 풀려버리는" 문제를 방지한다.
    private readonly Dictionary<Unit, int> _atkSpeedToken = new();
    private readonly Dictionary<Unit, int> _incomingDmgToken = new();

    // ---- 외부에서 호출되는 진입점들 ----
    // 바이옴이 바뀔 때: 이전 지속효과 제거 -> 새 지속효과 적용
    private void SwitchBiomePersistentEffects(BiomeType oldBiome, BiomeType newBiome)
    {
        RemoveBiomePersistentFromParty(oldBiome);
        ApplyBiomePersistentToParty(newBiome);
    }

    // 지속형 효과(파티) 적용
    private void ApplyBiomePersistentToParty(BiomeType biome)
    {
        var party = GetPartyUnitComponents();

        switch (biome)
        {
            case BiomeType.DeepForest:
                foreach (var u in party) ApplyMpRecoveryDelta(u, 2f);
                break;

            case BiomeType.Cave:
                foreach (var u in party) ApplyAttackRangeDelta(u, -1);
                break;

            case BiomeType.Labyrinth:
                // 예외: 미궁만 플레이어(파티)에게만 적용
                foreach (var u in party) ApplyMaxMpDelta(u, +50);
                break;
        }
    }

    // 지속형 효과(파티) 제거(원복)
    private void RemoveBiomePersistentFromParty(BiomeType biome)
    {
        var party = GetPartyUnitComponents();

        switch (biome)
        {
            case BiomeType.DeepForest:
                foreach (var u in party) RemoveMpRecoveryDelta(u);
                break;

            case BiomeType.Cave:
                foreach (var u in party) RemoveAttackRangeDelta(u);
                break;

            case BiomeType.Labyrinth:
                foreach (var u in party) RemoveMaxMpDelta(u);
                break;
        }
    }
    // 전투 시작 시점 효과
    // StartBattle() 끝에서 호출
    private void ApplyBiomeBattleStartEffects()
    {
        var party = GetPartyUnitComponents();
        var enemies = GetEnemyUnitComponents();

        // 지속형은 "전투 참여 전체"가 원칙이므로, 적에게는 전투 시작 시점에만 동일하게 적용
        ApplyBiomePersistentToEnemiesAtBattleStart(enemies);

        // 전투 시작 트리거(모든 캐릭터)
        foreach (var u in EnumerateAllBattleUnits(party, enemies))
        {
            if (u == null) continue;
            if (!IsAlive(u)) continue;

            switch (CurrentBiome)
            {
                case BiomeType.Lake:
                    AddMp(u, 100f);
                    break;

                case BiomeType.Snow:
                    ApplyAttackSpeedTempMultiplier(u, 0.7f, 5f); // -30%
                    FloatingTextPoolManager.Instance?.ShowStatus(
                        u.transform,
                        "공속 감소",
                        new Vector3(0f, 1.2f, 0f)
                    );
                    break;

                case BiomeType.Desert:
                    // 화상(데미지 증폭): 여기서는 "받는 피해 20% 증가"로 구현
                    ApplyIncomingDamageTempMultiplier(u, 1.2f, 5f);
                    FloatingTextPoolManager.Instance?.ShowStatus(
                        u.transform,
                        "화상",
                        new Vector3(0f, 1.2f, 0f)
                    );
                    break;

                case BiomeType.Plains:
                    ApplyAttackSpeedTempMultiplier(u, 1.3f, 5f); // +30%
                    FloatingTextPoolManager.Instance?.ShowStatus(
                        u.transform,
                        "공속 증가",
                        new Vector3(0f, 1.8f, 0f)
                    );
                    break;
            }
        }
    }

    // EndBattle(승리) 보상 들어가기 전에 호출
    private void ApplyBiomeBattleEndEffects()
    {
        // 숲: 전투 종료 후 파티 회복(전투 종료 시 적은 의미 없음)
        if (CurrentBiome != BiomeType.Forest) return;

        var party = GetPartyUnitComponents();
        foreach (var u in party)
        {
            if (u == null) continue;
            if (!IsAlive(u)) continue;
            HealByMaxHpPercent(u, 0.20f);
        }
    }

    // ---- 내부 유틸 ----
    private static bool IsAlive(Unit u) // 죽었는지 확인
    {
        if (u == null) return false;
        if (u.hp <= 0) return false;

        // FSM이 있으면 상태까지 확인 (Faint면 사망 처리)
        var fsm = u.GetComponent<UnitFSM>();
        if (fsm != null && fsm.CurrentState == UnitFSM.UnitState.Faint) return false;

        return true;
    }

    private static IEnumerable<Unit> EnumerateAllBattleUnits(List<Unit> party, List<Unit> enemies)
    {
        for (int i = 0; i < party.Count; i++) yield return party[i];
        for (int i = 0; i < enemies.Count; i++) yield return enemies[i];
    }

    private void ApplyBiomePersistentToEnemiesAtBattleStart(List<Unit> enemies)
    {
        // 미궁 제외(요구사항)
        switch (CurrentBiome)
        {
            case BiomeType.DeepForest:
                foreach (var e in enemies) if (e != null) ApplyMpRecoveryDelta_Enemy(e, -2f);
                break;

            case BiomeType.Cave:
                foreach (var e in enemies) if (e != null) ApplyAttackRangeDelta_Enemy(e, -1);
                break;

            // Labyrinth는 예외: 적에게 적용하지 않음
        }
    }

    private static void AddMp(Unit u, float amount)
    {
        u.mp = Mathf.Min(u.maxMp, u.mp + amount);
    }

    private static void HealByMaxHpPercent(Unit u, float percent)
    {
        double amount = u.maxHp * Mathf.Clamp01(percent);
        u.Heal(amount);
    }

    // ===== 지속형(파티) 적용/원복 =====
    private void ApplyMpRecoveryDelta(Unit u, float delta)
    {
        if (u == null) return;

        // 이미 적용돼 있으면 중복 적용 방지
        if (_appliedMpRecoveryDelta.ContainsKey(u)) return;

        float before = u.baseMpRecovery;
        float after = Mathf.Max(0f, before + delta);
        float applied = after - before;

        u.baseMpRecovery = after;
        _appliedMpRecoveryDelta[u] = applied;
    }

    private void RemoveMpRecoveryDelta(Unit u)
    {
        if (u == null) return;
        if (!_appliedMpRecoveryDelta.TryGetValue(u, out var applied)) return;

        u.baseMpRecovery = Mathf.Max(0f, u.baseMpRecovery - applied);
        _appliedMpRecoveryDelta.Remove(u);
    }

    private void ApplyAttackRangeDelta(Unit u, int delta)
    {
        if (u == null) return;
        if (_appliedAttackRangeDelta.ContainsKey(u)) return;

        int before = u.attackRange;
        int after = Mathf.Max(1, before + delta);
        int applied = after - before;

        u.attackRange = after;
        _appliedAttackRangeDelta[u] = applied;
    }

    private void RemoveAttackRangeDelta(Unit u)
    {
        if (u == null) return;
        if (!_appliedAttackRangeDelta.TryGetValue(u, out var applied)) return;

        u.attackRange = Mathf.Max(1, u.attackRange - applied);
        _appliedAttackRangeDelta.Remove(u);
    }

    private void ApplyMaxMpDelta(Unit u, int delta)
    {
        if (u == null) return;
        if (_appliedMaxMpDelta.ContainsKey(u)) return;

        int before = Mathf.RoundToInt(u.maxMp);
        int after = Mathf.Max(0, before + delta);
        int applied = after - before;

        u.maxMp = after;
        u.mp = Mathf.Min(u.maxMp, u.mp);

        _appliedMaxMpDelta[u] = applied;
    }

    private void RemoveMaxMpDelta(Unit u)
    {
        if (u == null) return;
        if (!_appliedMaxMpDelta.TryGetValue(u, out var applied)) return;

        int after = Mathf.Max(0, Mathf.RoundToInt(u.maxMp) - applied);
        u.maxMp = after;
        u.mp = Mathf.Min(u.maxMp, u.mp);

        _appliedMaxMpDelta.Remove(u);
    }

    // ===== 지속형(적) 전투 시작 적용(원복 불필요) =====
    private static void ApplyMpRecoveryDelta_Enemy(Unit u, float delta)
    {
        float after = Mathf.Max(0f, u.baseMpRecovery + delta);
        u.baseMpRecovery = after;
    }

    private static void ApplyAttackRangeDelta_Enemy(Unit u, int delta)
    {
        u.attackRange = Mathf.Max(1, u.attackRange + delta);
    }

    // ===== 5초 임시 배율(공속/피증) =====
    private void ApplyAttackSpeedTempMultiplier(Unit u, float multiplier, float duration)
    {
        if (u == null) return;

        int token = 1;
        _atkSpeedToken.TryGetValue(u, out token);
        token++;
        _atkSpeedToken[u] = token;

        u.attackSpeedMultiplier *= multiplier;
        StartCoroutine(CoRevertAttackSpeed(u, multiplier, duration, token));
    }

    private IEnumerator CoRevertAttackSpeed(Unit u, float multiplier, float duration, int token)
    {
        yield return new WaitForSeconds(duration);

        if (u == null) yield break;
        if (!_atkSpeedToken.TryGetValue(u, out var current) || current != token) yield break;

        // 원복
        if (multiplier != 0f)
            u.attackSpeedMultiplier /= multiplier;

        _atkSpeedToken.Remove(u);
    }

    private void ApplyIncomingDamageTempMultiplier(Unit u, float multiplier, float duration)
    {
        if (u == null) return;

        int token = 1;
        _incomingDmgToken.TryGetValue(u, out token);
        token++;
        _incomingDmgToken[u] = token;

        u.incomingDamageMultiplier *= multiplier;
        StartCoroutine(CoRevertIncomingDamage(u, multiplier, duration, token));
    }

    private IEnumerator CoRevertIncomingDamage(Unit u, float multiplier, float duration, int token)
    {
        yield return new WaitForSeconds(duration);

        if (u == null) yield break;
        if (!_incomingDmgToken.TryGetValue(u, out var current) || current != token) yield break;

        if (multiplier != 0f)
            u.incomingDamageMultiplier /= multiplier;

        _incomingDmgToken.Remove(u);
    }
}
