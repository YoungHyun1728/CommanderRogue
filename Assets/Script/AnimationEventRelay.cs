using UnityEngine;

using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    [SerializeField] private UnitFSM fsm;

    private void Reset()
    {
        // 보통 부모에 FSM이 있으니까 자동으로 잡아줌
        if (fsm == null) fsm = GetComponentInParent<UnitFSM>();
    }

    public void ExecuteAttackFromEvent()
    {
        if (fsm == null) fsm = GetComponentInParent<UnitFSM>();
        fsm?.ExecuteAttackFromAnimationEvent();
    }

    public void OnAttackAnimEnd()
    {
        if (fsm == null) fsm = GetComponentInParent<UnitFSM>();
        fsm?.OnAttackAnimEnd();
    }

}