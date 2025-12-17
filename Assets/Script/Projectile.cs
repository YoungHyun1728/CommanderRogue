using UnityEngine;

public class Projectile : MonoBehaviour
{
    private UnitFSM shooter;
    [SerializeField]private GameObject target;
    private float speed = 8.0f;
    private Vector3 moveDir;

    private ProjectilePoolEntry poolEntry;   //투사체가 속해 있는 풀 정보

    public void SetPoolEntry(ProjectilePoolEntry entry)
    {
        poolEntry = entry;
    }
    
    public void Init(UnitFSM shooter, GameObject target)
    {
        this.shooter = shooter;
        this.target = target;
        
        if (target != null)
            moveDir = (target.transform.position - transform.position).normalized;

        float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle-90f);
    }

    void Update()
    {
        if (target == null)
        {
            Despawn();
            return;
        }

        transform.position += moveDir * speed * Time.deltaTime;

        
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (target == null || shooter == null)
            return;

        if (other.gameObject == target)
        {
            shooter.PerformAttack(target);
            Despawn();
        }
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
