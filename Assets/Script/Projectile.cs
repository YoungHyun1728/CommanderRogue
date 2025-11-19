using UnityEngine;

public class Projectile : MonoBehaviour
{
    private UnitFSM shooter;
    private GameObject target;
    private float speed = 6.0f;
    private Vector3 moveDir;
    
    public void Init(UnitFSM shooter, GameObject target)
    {
        this.shooter = shooter;
        this.target = target;
        
        if (target != null)
            moveDir = (target.transform.position - transform.position).normalized;

        float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle-90f);
        Debug.Log($"[Projectile] angle: {angle}");
    }

    void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
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
            Destroy(gameObject);
        }
    }
}
