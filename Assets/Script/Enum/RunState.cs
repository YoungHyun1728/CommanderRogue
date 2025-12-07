using UnityEngine;

public enum RunState
{
    OnMap,          // 맵에서 다음 노드를 고르는 상태
    Ready,          // 전투, 이벤트 노드에서 선택지를 고르는 상태
    Battle,         // 전투중인 상태
    Reward,         // 라운드 클리어 후 보상, 상점 이용중
    Event,          // 이벤트 진행중
    Rest            // 휴식, 이것도 이벤트이긴한데 일단 넣어둠
}