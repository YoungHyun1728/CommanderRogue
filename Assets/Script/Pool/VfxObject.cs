using System.Collections;
using UnityEngine;

/// <summary>
/// VFX 오브젝트 (root에) 풀링에 사용되는 VFX 오브젝트 클래스
/// VFX 프리팹에 attach 해서 사용
/// Sprite 애니메이션 길이를 duration으로 지정해서 Play() 호출
/// </summary>

public class VfxObject : MonoBehaviour
{
    private VfxPoolEntry poolEntry;

    public void SetPoolEntry(VfxPoolEntry entry)
    {
        poolEntry = entry;
    }

    // duration 초 뒤 자동으로 풀로 복귀
    public void Play(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(AutoDespawn(duration));
    }

    private IEnumerator AutoDespawn(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, duration));
        Despawn();
    }

    public void Despawn()
    {
        if (poolEntry != null && VfxPoolManager.Instance != null)
            VfxPoolManager.Instance.Release(this, poolEntry);
        else
            Destroy(gameObject);
    }
}