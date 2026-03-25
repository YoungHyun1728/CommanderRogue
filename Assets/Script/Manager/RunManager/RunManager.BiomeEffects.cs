using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바이옴의 "지속 효과"와 "전투 시작/종료 트리거 효과"를 전담하는 클래스.
/// RunManager가 바이옴 디테일을 직접 들고 있지 않도록 분리했다.
/// </summary>
public sealed class RunBiomeEffectsController
{
    private readonly RunManager _run;

    // ===== 지속형(파티) 원복을 위한 적용량 기록 =====
    // 바이옴이 바뀔 때 정확히 원상복구하기 위해 "실제로 적용된 delta"를 저장한다.
    private readonly Dictionary<Unit, float> _appliedMpRecoveryDelta = new Dictionary<Unit, float>();
    private readonly Dictionary<Unit, int> _appliedAttackRangeDelta = new Dictionary<Unit, int>();
    private readonly Dictionary<Unit, int> _appliedMaxMpDelta = new Dictionary<Unit, int>();

    // ===== 전투 시작 5초 버프/디버프 토큰 =====
    // 같은 유닛에 임시 배율이 겹칠 때 먼저 걸린 코루틴이 나중 효과를 되돌리는 문제를 막기 위한 토큰.
    private readonly Dictionary<Unit, int> _atkSpeedToken = new Dictionary<Unit, int>();
    private readonly Dictionary<Unit, int> _incomingDmgToken = new Dictionary<Unit, int>();

    public RunBiomeEffectsController(RunManager run)
    {
        _run = run;
    }

    /// <summary>
    /// 바이옴 전환 시 호출한다.
    /// 이전 바이옴의 지속 효과를 제거하고 새 바이옴 지속 효과를 적용한다.
    /// </summary>
    public void SwitchPersistentEffects(BiomeType oldBiome, BiomeType newBiome)
    {
        RemovePersistentFromParty(oldBiome);
        ApplyPersistentToParty(newBiome);
    }

    /// <summary>
    /// 파티에 적용되는 "지속형" 바이옴 효과를 적용한다.
    /// </summary>
    public void ApplyPersistentToParty(BiomeType biome)
    {
        var party = _run.GetPartyUnitComponents();

        switch (biome)
        {
            case BiomeType.DeepForest:
                foreach (var u in party) ApplyMpRecoveryDelta(u, 2f);
                break;
            case BiomeType.Cave:
                foreach (var u in party) ApplyAttackRangeDelta(u, -1);
                break;
            case BiomeType.Labyrinth:
                // 요구사항: 미궁은 플레이어 파티에만 적용.
                foreach (var u in party) ApplyMaxMpDelta(u, 50);
                break;
        }
    }

    /// <summary>
    /// 전투 시작 시점 트리거 효과를 적용한다.
    /// </summary>
    public void ApplyBattleStartEffects()
    {
        var party = _run.GetPartyUnitComponents();
        var enemies = _run.GetEnemyUnitComponents();

        // 지속형은 전투 참여 단위로 맞추기 위해 적에게는 전투 시작 시점에만 반영한다.
        ApplyPersistentToEnemiesAtBattleStart(enemies);

        foreach (var u in EnumerateAllBattleUnits(party, enemies))
        {
            if (u == null || !IsAlive(u)) continue;

            switch (_run.CurrentBiome)
            {
                case BiomeType.Lake:
                    AddMp(u, 100f);
                    break;

                case BiomeType.Snow:
                    ApplyAttackSpeedTempMultiplier(u, 0.7f, 5f); // -30%
                    FloatingTextPoolManager.Instance?.ShowStatus(
                        u.transform, "공격속도 감소", new Vector3(0f, 1.2f, 0f));
                    break;

                case BiomeType.Desert:
                    // "화상"을 받는 피해 배율 증가로 구현.
                    ApplyIncomingDamageTempMultiplier(u, 1.2f, 5f);
                    FloatingTextPoolManager.Instance?.ShowStatus(
                        u.transform, "화상", new Vector3(0f, 1.2f, 0f));
                    break;

                case BiomeType.Plains:
                    ApplyAttackSpeedTempMultiplier(u, 1.3f, 5f); // +30%
                    FloatingTextPoolManager.Instance?.ShowStatus(
                        u.transform, "공격속도 증가", new Vector3(0f, 1.8f, 0f));
                    break;
            }
        }
    }

    /// <summary>
    /// 전투 승리 직후(보상 진입 전) 트리거 효과를 적용한다.
    /// </summary>
    public void ApplyBattleEndEffects()
    {
        if (_run.CurrentBiome != BiomeType.Forest) return;

        var party = _run.GetPartyUnitComponents();
        foreach (var u in party)
        {
            if (u == null || !IsAlive(u)) continue;
            HealByMaxHpPercent(u, 0.20f);
        }
    }

    private void RemovePersistentFromParty(BiomeType biome)
    {
        var party = _run.GetPartyUnitComponents();

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

    private static bool IsAlive(Unit u)
    {
        if (u == null || u.hp <= 0) return false;
        var fsm = u.GetComponent<UnitFSM>();
        return fsm == null || fsm.CurrentState != UnitFSM.UnitState.Faint;
    }

    private static IEnumerable<Unit> EnumerateAllBattleUnits(List<Unit> party, List<Unit> enemies)
    {
        for (int i = 0; i < party.Count; i++) yield return party[i];
        for (int i = 0; i < enemies.Count; i++) yield return enemies[i];
    }

    private void ApplyPersistentToEnemiesAtBattleStart(List<Unit> enemies)
    {
        // Labyrinth는 적에게 적용하지 않는 예외 룰.
        switch (_run.CurrentBiome)
        {
            case BiomeType.DeepForest:
                foreach (var e in enemies) if (e != null) ApplyMpRecoveryDeltaEnemy(e, -2f);
                break;
            case BiomeType.Cave:
                foreach (var e in enemies) if (e != null) ApplyAttackRangeDeltaEnemy(e, -1);
                break;
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
        if (u == null || _appliedMpRecoveryDelta.ContainsKey(u)) return;

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
        if (u == null || _appliedAttackRangeDelta.ContainsKey(u)) return;

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
        if (u == null || _appliedMaxMpDelta.ContainsKey(u)) return;

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
    private static void ApplyMpRecoveryDeltaEnemy(Unit u, float delta)
    {
        u.baseMpRecovery = Mathf.Max(0f, u.baseMpRecovery + delta);
    }

    private static void ApplyAttackRangeDeltaEnemy(Unit u, int delta)
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
        _run.StartCoroutine(CoRevertAttackSpeed(u, multiplier, duration, token));
    }

    private IEnumerator CoRevertAttackSpeed(Unit u, float multiplier, float duration, int token)
    {
        yield return new WaitForSeconds(duration);

        if (u == null) yield break;
        if (!_atkSpeedToken.TryGetValue(u, out var current) || current != token) yield break;

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
        _run.StartCoroutine(CoRevertIncomingDamage(u, multiplier, duration, token));
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
