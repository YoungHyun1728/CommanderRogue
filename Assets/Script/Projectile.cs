using UnityEngine;
using System.Collections.Generic;

public class Projectile : MonoBehaviour
{
    // 기본공격용 투사체
    private UnitFSM shooter;
    [SerializeField]private GameObject target;

    // 공통 속성
    private float speed = 15.0f;
    private Vector3 moveDir;
    private float lifeTime = 5.0f;
    private float spawnTime;

    private ProjectilePoolEntry poolEntry;   //투사체가 속해 있는 풀 정보

    // 스킬용 투사체
    private bool isSkillProjectile = false;
    private Unit casterUnit;
    private List<SkillEffectDefinition> onHitEffects;
    private VfxType hitVfxType = VfxType.None;
    private float hitVfxDuration = 0.4f;
    private Vector3 hitVfxOffset = Vector3.zero;

    private bool piercing = false;
    private int remainingHits = 1;

    public void SetPoolEntry(ProjectilePoolEntry entry)
    {
        poolEntry = entry;
    }
    
    // 기본공격용 Init
    public void Init(UnitFSM shooter, GameObject target, float speedOverride = 14f, float lifeTimeOverride = 3.5f)
    {
        this.shooter = shooter;
        this.target = target;

        isSkillProjectile = false;
        casterUnit = null;
        onHitEffects = null;
        hitVfxType = VfxType.None;
        hitVfxOffset = Vector3.zero;
        piercing = false;
        remainingHits = 1;

        speed = speedOverride;
        lifeTime = lifeTimeOverride;
        spawnTime = Time.time;
        
       if (target != null)
        {
            Vector3 aim = GetAimPos(target);
            moveDir = (aim - transform.position).normalized;
        }

        float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle-90f);
    }

    private Vector3 GetAimPos(GameObject targetGO)
    {
        if (targetGO == null) return transform.position;

        // 1) UnitFSM AimPoint 우선
        var fsm = targetGO.GetComponent<UnitFSM>();
        if (fsm != null && fsm.AimPoint != null)
            return fsm.AimPoint.position;

        // 2) Collider2D center
        var col = targetGO.GetComponent<Collider2D>();
        if (col != null)
            return col.bounds.center;

        // 3) fallback
        return targetGO.transform.position;
    }

    
    // 스킬투사체: 타겟 추적(시작방향은 target 기준)    
    public void InitSkillToTarget(
        Unit casterUnit,
        GameObject target,
        float speedOverride,
        float lifeTimeOverride,
        List<SkillEffectDefinition> onHitEffects,
        VfxType hitVfxType = VfxType.None,
        float hitVfxDuration = 0.4f,
        Vector3 hitVfxOffset = default,
        bool piercing = false,
        int maxHits = 1
    )
    {
        this.casterUnit = casterUnit;
        this.target = target;
        this.onHitEffects = onHitEffects;
        this.hitVfxType = hitVfxType;
        this.hitVfxDuration = hitVfxDuration;
        this.hitVfxOffset = hitVfxOffset;

        isSkillProjectile = true;
        shooter = casterUnit != null ? casterUnit.GetComponent<UnitFSM>() : null;

        speed = speedOverride;
        lifeTime = lifeTimeOverride;
        spawnTime = Time.time;

        this.piercing = piercing;
        this.remainingHits = Mathf.Max(1, maxHits);

        if (target != null)
        {
            Vector3 aim = GetAimPos(target);
            moveDir = (aim - transform.position).normalized;
        }
        else
        {
            moveDir = Vector3.right;
        }
        RotateToDir(moveDir);
    }

    // 스킬투사체: 고정방향
    public void InitSkillForward(
        Unit casterUnit,
        Vector3 dir,
        float speedOverride,
        float lifeTimeOverride,
        List<SkillEffectDefinition> onHitEffects,
        VfxType hitVfxType = VfxType.None,
        float hitVfxDuration = 0.4f,
        Vector3 hitVfxOffset = default,
        bool piercing = false,
        int maxHits = 1
    )
    {
        this.casterUnit = casterUnit;
        this.target = null;
        this.onHitEffects = onHitEffects;
        this.hitVfxType = hitVfxType;
        this.hitVfxDuration = hitVfxDuration;
        this.hitVfxOffset = hitVfxOffset;

        isSkillProjectile = true;
        shooter = casterUnit != null ? casterUnit.GetComponent<UnitFSM>() : null;

        speed = speedOverride;
        lifeTime = lifeTimeOverride;
        spawnTime = Time.time;

        this.piercing = piercing;
        this.remainingHits = Mathf.Max(1, maxHits);

        moveDir = (dir.sqrMagnitude > 0.0001f) ? dir.normalized : Vector3.right;

        //RotateToDir(moveDir);
    }

    void Update()
    {
        // 수명 만료
        if (Time.time - spawnTime >= lifeTime)
        {
            Despawn();
            return;
        }

        // 기본공격 투사체는 타겟 없어지면 despawn
        if (!isSkillProjectile && target == null)
        {
            Despawn();
            return;
        }

        transform.position += moveDir * speed * Time.deltaTime;        
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isSkillProjectile)
        {
            if (casterUnit == null) return;

            // 타겟 있는 스킬투사체: 그 타겟만 맞으면 처리
            if (target != null)
            {
                if (other.gameObject != target) return;
                ApplySkillHit(other.gameObject);
                return;
            }

            // 타겟 없는(방향) 스킬투사체: 적 유닛이면 히트
            // (태그 기준: 내가 Player면 EnemyUnit, 내가 Enemy면 PlayerUnit)
            string enemyTag = (shooter != null && shooter.CompareTag("EnemyUnit")) ? "PlayerUnit" : "EnemyUnit";
            if (!other.CompareTag(enemyTag)) return;

            ApplySkillHit(other.gameObject);
            return;
        }

        // ===== 기본공격 투사체 로직 =====
        if (target == null || shooter == null)
            return;

        if (other.gameObject == target)
        {
            shooter.PerformAttack(target);
            Despawn();
        }
    }

    private void ApplySkillHit(GameObject hitGO)
    {
        // 히트 VFX
        if (hitVfxType != VfxType.None && VfxPoolManager.Instance != null)
        {
            Vector3 hitPos = hitGO != null ? GetAimPos(hitGO) : transform.position;
            var vfx = VfxPoolManager.Instance.Get(hitVfxType, hitPos + hitVfxOffset, Quaternion.identity);
            if (vfx != null) vfx.Play(hitVfxDuration);
        }

        // 스킬 이펙트
        if (onHitEffects != null && onHitEffects.Count > 0)
        {
            var casterFsm = casterUnit.GetComponent<UnitFSM>();
            var skills = casterUnit.GetComponent<UnitSkillSystem>();
            var status = casterUnit.GetComponent<UnitStatusEffectController>();

            SkillContext ctx = new SkillContext(casterUnit, casterFsm, skills, status, hitGO);

            foreach (var e in onHitEffects)
            {
                if (e == null) continue;
                e.Execute(ctx);
            }
        }

        remainingHits--;
        if (!piercing || remainingHits <= 0)
            Despawn();
    }

    private void RotateToDir(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private void Despawn()
    {
        if (poolEntry != null && ProjectilePoolManager.Instance != null)
        {
            ProjectilePoolManager.Instance.Release(this, poolEntry);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
