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

# 전체문서
### [1. 절차적 맵 기반 진행](https://github.com/YoungHyun1728/CommanderRogue/blob/main/ProjectDescription/1.%20Procedural%20Map%20Progression%20Loop.md)

### [2. FSM을 이용한 2D 타일맵 전투 구현](https://github.com/YoungHyun1728/CommanderRogue/blob/main/ProjectDescription/2.%20Multi-Unit%202DTilemapCombat%20System%20based%20FSM.md)

### [3.  JSON 로컬 저장 + PlayFab을 이용한 챌린지모드 구현(PvP형 PvE)](https://github.com/YoungHyun1728/CommanderRogue/blob/main/ProjectDescription/3.%20Local%20Save%20Run%20Data%20and%20Challenge%20Battle%20System.md)

### [4. 오브젝트 풀링을 이용한 최적화](https://github.com/YoungHyun1728/CommanderRogue/blob/main/ProjectDescription/4.%20Object%20Pooling%20for%20Combat%20Resource%20Optimization.md)

### [5. 캐릭터 정보 UI와 Hover Tooltip](https://github.com/YoungHyun1728/CommanderRogue/blob/main/ProjectDescription/5.%20UnitInfo%20UI%20%26%20Hover%20Tooltip.md)
