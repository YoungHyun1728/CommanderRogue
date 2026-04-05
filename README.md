# CommanderRogue
2D 타일맵기반 오토배틀러 로그라이크

설명 영상 링크 : 

플레이 링크 : 

# 주요기능
| 주요 구현 | 주요 기능 |
|:----|:----|
|절차적 맵 기반 진행 루프 구현 (노드 선택, 페이즈 전환, 노드 진입 처리) |	Unity C#, 상태 전환 로직, 데이터 드리븐 노드 설계 |
|FSM 기반 2D 타일맵 전투 구현	| FSM, 타일 점유/이동 로직, A* 경로탐색, Coroutine|
|런 데이터 저장/복원 및 비동기 챌린지 전투 구현 (유사 PvP형 PvE)	| JSON 직렬화/역직렬화, 로컬 파일 I/O, PlayFab SDK, CloudScript|
|오브젝트 풀링 기반 전투 리소스 최적화 | (투사체/VFX/FloatingText)	Object Pool Pattern, 메모리/GC 최적화|
|Hover Tooltip 기반 UI 상호작용 구현 |	Unity UI EventSystem (IPointerEnter/Exit)|
1. 절차적 맵 기반 진행

<img width="1142" height="748" alt="image" src="https://github.com/user-attachments/assets/b161b1a0-4603-4a46-bb84-6e5def098dac" />


GameScene에 진입시 MapGenerator 클래스가 Map을 생성합니다.

InitializeMap()은 이어하기 버튼으로 진입했다면 저장된 맵을 생성하고, 새로하기로 진입했다면 새로운맵을 생성합니다.



<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/f21a1152-525c-4c2d-97f3-ba003d0260cf" />

200라운드 + 50라운드 구성으로 총 250라운드로 진행되는 절차적 맵을 생성해 이어지는 노드들을 선택해 기본적인 진행루프를 구성했습니다.

<img width="77" height="64" alt="image" src="https://github.com/user-attachments/assets/2ee4c1a2-1b12-4ab7-ac83-6d52e79e1fca" />

전투 노드

전투노드에 들어가면 RunManager 클래스가 EnterReady()를 호출합니다.

EnterReady()가 호출되면 새로운 적 스폰 전에 DespawnCurrentEnemies()를 호출해 기존 적 정보를 모두 삭제합니다.

그 다음 EnemySpawnManager 클래스가 SpawnBattle() 함수를 호출해 라운드에 맞는 적을 소환해 줍니다. 

라운드 증가에 따라 레벨이 높은적이 스폰되고 더욱 많은 장비를 장착하게 합니다. 

<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/d70982cb-fd8a-4438-9ca0-a8a86e7378b5" />

< 전투 노드 진입 이미지 >

<img width="77" height="64" alt="image" src="https://github.com/user-attachments/assets/d180c514-761c-4ed3-affe-c778ab04e105" />

이벤트 노드

이벤트 노드 진입시 
RunManager 클래스가 EnterEvent(node) 를 호출해 EventManager가 이벤트를 시작하게 합니다.

이벤트는 EventManager에 있는 eventPool(SO리스트)에서 조건(라운드/바이옴/등장횟수)에 맞는 이벤트를 골라 

랜덤으로 뽑아 옵니다. 뽑힌 이벤트의 데이터를 기반으로 이벤트 패널을 구성합니다.

이벤트 패널은 이벤트 이름, 설명, 이미지, 선택지(선택지 툴팁) 으로 구성됩니다.
<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/b069985c-9ac8-4352-8399-9e50a0f5e408" />

EventChoiceTooltipTrigger 클래스에 IPointerEnterHandler, IPointerExitHandler를 사용하여
선택지 버튼에 마우스포인터를 올리면 해당 선택지에 입력되어 있는 결과를 미리 볼 수 있는 툴팁이 나오게 했습니다.

선택지는 이벤트마다 다르지만 크게 골드소모, 골드획득, 체력회복, 피해, 지나가기가 있고 이 중 지나가기 선택지는
RunManager.EnterShopOnlyFromLeave() 함수가 호출되어 보상없이 상점만 이용이 가능하게 됩니다.

각 Choice는 outcomes(List<EventOutcome>)를 가지며, Outcome은 발동 조건(확률/스탯 판정)과 적용 효과(즉시 효과·다음 전투 패널티·다음 이벤트 분기·보상풀 변경)를 함께 정의합니다.

다음 이벤트 분기는 해당 선택지를 선택 시 다른 이벤트로 넘어가는 Outcome 이고 특정 이벤트에서만 사용됩니다.

RewardManager의 기본 보상풀(globalPool)에는 공통 보상 아이템이 들어 있고, 특정 이벤트 전용 아이템은 eventId를 key로 가진 eventPool에 분리해 둡니다. RunRewardCoordinator가 GiveReward()를 호출하면 RewardManager가 먼저 eventId와 일치하는 eventPool을 우선 선택해 보상을 뽑고, 부족한 슬롯은 설정(fillRestFromGlobal)에 따라 globalPool에서 채워 최종 보상 목록을 구성합니다. 

<img width="58" height="49" alt="image" src="https://github.com/user-attachments/assets/6731dc12-e20e-4eb4-9cf0-e4cde8817b67" />

회복 노드
회복노드 진입시 RunManager에서 아군 캐릭터 리스트를 순회하며 ReviveToEmptyTile(false) 함수를 호출해 체력이 0이 되어서 비활성화 되었던 아군캐릭터들을 포함해서 모든 캐릭터가 체력이 가득찹니다. 
ReviveToEmptyTile(bool) 함수는 캐릭터가 기절상태라면 회복시키면서 빈타일을 찾아 다시 활성화시키고 타일맵에 온전히 있다면 회복만 시키고 return 합니다. false 는 100% 회복, true는 절반만 회복합니다. 부활아이템 사용 시 호출되는 함수라   
추가로 ToastManager가 모든 아군이 회복 되었다는 메세지를 보여주며 다음라운드 선택으로 바로 이어집니다.

<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/d2bd21ef-edb6-4b51-bcc1-1428ffb15787" />
<br />
<img width="58" height="49" alt="image" src="https://github.com/user-attachments/assets/240b6d00-4df4-4cf9-939b-32680908c614" />

보스 노드

보스의 종류는 지정된 라운드마다 나오는 네크로맨서 보스와 20라운드마다 바이옴의 마지막에 등장하는 바이옴리더 총 2종류의 보스가 있습니다.

보스노드도 전투노드와 같이 RunManager가 EnterReady()를 호출하고 우선순위에 따라 (네크로맨서 -> 바이옴리더 -> 일반 배틀) 순으로 확인하고 스폰을 하기 때문에 같은 SpawnBattle() 함수로 호출을 하지만 네크로맨서보스는 기존바이옴 적 유닛 풀에서 랜덤으로 생성하는 것이 아니라 SpawnOneFixed() 함수를 네크로맨서배틀 유닛리스트를 순회하며 모두 소환합니다. 고정된 배틀용 유닛풀을 만들고 LastNecromancerIndex로 인덱스를 찾아서 라운드에 맞는 적 유닛풀을 찾아 모두 스폰하게 했습니다.

바이옴리더의 경우 바이옴마다 보스가 2종류가 있어서 SelectBossSet(List<UnitData> list, int bossIndex) 함수가 몇번째 보스전 인지에 따라 List<Unitdata> 로 결과를 반환합니다.

보스전에선 해당 보스의 간단한 다이얼로그도 구현하여 전투 시작전에 대사를 넣어 구현해봤습니다.

<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/ef502b48-3902-47b4-a685-4799a17f572a" />

< 보스노드 진입 이미지 >

2. FSM을 이용한 2D 타일맵 전투 구현
<img width="1131" height="739" alt="image" src="https://github.com/user-attachments/assets/d2a0ce22-fcc6-44b6-870f-221d868f931a" />

3.  JSON 로컬 저장 + PlayFab을 이용한 챌린지모드 구현(PvP형 PvE)
<img width="1135" height="704" alt="image" src="https://github.com/user-attachments/assets/72dec38c-25fa-4e00-b10a-26a2bf15541d" />

설명: 런 데이터는 RunSaveCoordinator가 SaveData로 변환하고 SaveManager가 JSON I/O를 담당한다.
챌린지는 파티 스냅샷을 만들어 로컬/서버(PlayFab) 양쪽에 저장·조회하는 비동기 PvE 구조다.

4. 오브젝트 풀링을 이용한 최적화
<img width="1144" height="280" alt="image" src="https://github.com/user-attachments/assets/294684e1-14e1-45a1-a770-ddbfd477d4e0" />

5. UI Hover Tooltip
스킬 툴팁

<img width="1095" height="175" alt="image" src="https://github.com/user-attachments/assets/ab1db234-6b21-4211-bbf6-b9457f143c8e" />

이벤트 선택지 툴팁

<img width="784" height="176" alt="image" src="https://github.com/user-attachments/assets/adc64137-50e6-4f69-9150-ce8f25c94e42" />

아이템 설명 호버 툴팁

<img width="791" height="178" alt="image" src="https://github.com/user-attachments/assets/382461b9-c3f1-415f-bc2f-2a034278e391" />

설명: 포인터 이벤트를 트리거 클래스에서 받아 툴팁 시스템/패널로 전달하는 구조다.
UI 로직과 데이터 표시를 분리해서 확장성과 유지보수성을 높였다.
