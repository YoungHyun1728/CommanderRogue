# 1. 절자척 맵 진행 기반 진행

#### 관련 클래스
- MapGenerator : 맵 생성 및 초기화
- RunManager : 노드 진입 및 진행 상태 관리
- EnemySpawnManager : 전투/보스 적 생성
- EventManager : 이벤트 선택 및 패널 구성
- RewardManager : 보상풀 관리

<img width="1142" height="748" alt="image" src="https://github.com/user-attachments/assets/b161b1a0-4603-4a46-bb84-6e5def098dac" />

#### 작동 흐름
1. GameScene 진입 시 MapGenerator가 맵을 생성합니다.
2. 이어하기면 저장된 맵 복원, 새 게임이면 새 맵을 생성합니다.
3. 플레이어가 맵에서 다음 노드 선택합니다.
4. RunManager가 노드 타입에 따라 진입 함수 호출합니다.
5. 전투 / 이벤트가 끝나면 보상을 선택 후 적용한 뒤 다음 노드 선택단계로 이어집니다.

   회복노드는 바로 다음 노드 선택으로 넘어갑니다.

   이때 진행 단계가 저장이 됩니다.
7. 3 ~ 5 가 반복됩니다.

## 작동 흐름 세부 설명

GameScene에 진입시 MapGenerator 클래스가 Map을 생성합니다.

InitializeMap()은 이어하기 버튼으로 진입했다면 저장된 맵을 생성하고, 새로하기로 진입했다면 새로운맵을 생성합니다.
한 라운드 당 5개의 노드가 있고 다음 노드로 이어지는 길은 1~2개로 랜덤으로 생성되며 자신의 위치에서 -1 ~ +1 (위 아래로 한칸씩)만 이어지게 했습니다.
노드의 종류는 전투, 이벤트, 회복, 보스 노드가 있습니다.

<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/f21a1152-525c-4c2d-97f3-ba003d0260cf" />

200라운드 + 50라운드 구성으로 총 250라운드로 진행되는 절차적 맵을 생성해 이어지는 노드들을 선택해 기본적인 진행루프를 구성했습니다.



## 전투 노드
<img width="77" height="64" alt="image" src="https://github.com/user-attachments/assets/2ee4c1a2-1b12-4ab7-ac83-6d52e79e1fca" />

전투노드에 들어가면 RunManager 클래스가 EnterReady()를 호출합니다.

EnterReady()가 호출되면 새로운 적 스폰 전에 DespawnCurrentEnemies()를 호출해 기존 적 정보를 모두 삭제합니다.

그 다음 EnemySpawnManager 클래스가 SpawnBattle() 함수를 호출해 라운드에 맞는 적을 소환해 줍니다. 

라운드 증가에 따라 레벨이 높은적이 스폰되고 더욱 많은 장비를 장착하게 합니다. 

<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/d70982cb-fd8a-4438-9ca0-a8a86e7378b5" />

< 전투 노드 진입 이미지 >

<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/6b42680a-9c23-4a2a-b681-368ae19b0f22" />

< 라운드 진행에 따른 적 캐릭터 장비 착용 이미지 >

전투를 시작하고 RunBattleCoordinator가 아군이나 적 유닛이 HP가 0이 될때마다 CheckEndBattle() 함수를 호출 하여 어느 한쪽의 유닛 수가 0이 되었는지를 확인합니다.
0이 되면 전투가 종료 되었다고 판단하고 승리시 보상페이즈로 넘어가고 패배시 게임 결과창을 띄웁니다.

<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/ebfe8271-d4dd-4f42-aab6-de5bbe1fef0c" />

< 게임 패배 결과창 UI >

전투 중 체력이 0이 된 아군은 오브젝트를 비활성으로 처리하고 적은 레벨에 따라 주어질 경험치와 골드를 계산 후 Destroy로 처리합니다.

## 이벤트 노드
<img width="77" height="64" alt="image" src="https://github.com/user-attachments/assets/d180c514-761c-4ed3-affe-c778ab04e105" />

이벤트 노드 진입시 
`RunManager` 클래스가 EnterEvent(node) 를 호출해 `EventManager`가 이벤트를 시작하게 합니다.

이벤트는 `EventManager`에 있는 eventPool(SO리스트)에서 조건(라운드/바이옴/등장횟수)에 맞는 이벤트를 골라 

랜덤으로 뽑아 옵니다. 뽑힌 이벤트의 데이터를 기반으로 이벤트 패널을 구성합니다.

이벤트 패널은 이벤트 이름, 설명, 이미지, 선택지(선택지 툴팁) 으로 구성됩니다.
<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/b069985c-9ac8-4352-8399-9e50a0f5e408" />

`EventChoiceTooltipTrigger` 클래스에 IPointerEnterHandler, IPointerExitHandler를 사용하여
선택지 버튼에 마우스포인터를 올리면 해당 선택지에 입력되어 있는 결과를 미리 볼 수 있는 툴팁이 나오게 했습니다.

선택지는 이벤트마다 다르지만 크게 골드소모, 골드획득, 체력회복, 피해, 지나가기가 있고 이 중 지나가기 선택지는
`RunManager`.EnterShopOnlyFromLeave() 함수가 호출되어 보상없이 상점만 이용이 가능하게 됩니다.

각 Choice는 outcomes(List<EventOutcome>)를 가지며, Outcome은 발동 조건(확률/스탯 판정)과 적용 효과(즉시 효과·다음 전투 패널티·다음 이벤트 분기·보상풀 변경)를 함께 정의합니다.

다음 이벤트 분기는 해당 선택지를 선택 시 다른 이벤트로 넘어가는 Outcome 이고 특정 이벤트에서만 사용됩니다.

`RewardManager`의 기본 보상풀(globalPool)에는 공통 보상 아이템이 들어 있고, 특정 이벤트 전용 아이템은 eventId를 key로 가진 eventPool에 분리해 둡니다. RunRewardCoordinator가 GiveReward()를 호출하면 `RewardManager`가 먼저 eventId와 일치하는 eventPool을 우선 선택해 보상을 뽑고, 부족한 슬롯은 설정(fillRestFromGlobal)에 따라 globalPool에서 채워 최종 보상 목록을 구성합니다. 



## 회복 노드
<img width="58" height="49" alt="image" src="https://github.com/user-attachments/assets/6731dc12-e20e-4eb4-9cf0-e4cde8817b67" />

회복노드 진입시 `RunManager`에서 아군 캐릭터 리스트를 순회하며 ReviveToEmptyTile(false) 함수를 호출해 체력이 0이 되어서 비활성화 되었던 아군캐릭터들을 포함해서 모든 캐릭터가 체력이 가득찹니다. 
ReviveToEmptyTile(bool) 함수는 캐릭터가 기절상태라면 회복시키면서 빈타일을 찾아 다시 활성화시키고 있다면 회복만 시키고 return 합니다.
추가로 `ToastManager`가 모든 아군이 회복 되었다는 메세지를 보여주며 다음라운드 선택으로 바로 이어집니다.

<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/d2bd21ef-edb6-4b51-bcc1-1428ffb15787" />
<br />


## 보스 노드
<img width="58" height="49" alt="image" src="https://github.com/user-attachments/assets/240b6d00-4df4-4cf9-939b-32680908c614" />

보스의 종류는 지정된 라운드마다 나오는 네크로맨서 보스와 20라운드마다 바이옴의 마지막에 등장하는 바이옴리더 총 2종류의 보스가 있습니다.

보스노드도 전투노드와 같이 `RunManager`가 EnterReady()를 호출하고 우선순위에 따라 (네크로맨서 -> 바이옴리더 -> 일반 배틀) 순으로 확인하고 스폰을 하기 때문에 같은 SpawnBattle() 함수로 호출을 하지만 네크로맨서보스는 기존바이옴 적 유닛 풀에서 랜덤으로 생성하는 것이 아니라 SpawnOneFixed() 함수를 네크로맨서배틀 유닛리스트를 순회하며 모두 소환합니다. 고정된 배틀용 유닛풀을 만들고 LastNecromancerIndex로 인덱스를 찾아서 라운드에 맞는 적 유닛풀을 찾아 모두 스폰하게 했습니다.

바이옴리더의 경우 바이옴마다 보스가 2종류가 있어서 SelectBossSet(List<UnitData> list, int bossIndex) 함수가 몇번째 보스전 인지에 따라 List<Unitdata> 로 결과를 반환합니다.

보스전에선 해당 보스의 간단한 다이얼로그도 구현하여 전투 시작전에 대사를 넣어 구현해봤습니다.

<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/ef502b48-3902-47b4-a685-4799a17f572a" />

< 보스노드 진입 이미지 >

## 보상 페이즈


