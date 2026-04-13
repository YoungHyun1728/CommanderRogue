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
### [1. 절차적 맵 기반 진행](https://github.com/YoungHyun1728/CommanderRogue/blob/main/ProjectDescription/1.%20Procedural%20Map%20Progression%20Loop.md)


### [2. FSM을 이용한 2D 타일맵 전투 구현](https://github.com/YoungHyun1728/CommanderRogue/blob/main/ProjectDescription/2.%20Multi-Unit%202DTilemapCombat%20System%20based%20FSM.md)
<img width="1131" height="739" alt="image" src="https://github.com/user-attachments/assets/d2a0ce22-fcc6-44b6-870f-221d868f931a" />

### [3.  JSON 로컬 저장 + PlayFab을 이용한 챌린지모드 구현(PvP형 PvE)](https://github.com/YoungHyun1728/CommanderRogue/blob/main/ProjectDescription/3.%20Run%20Data%20Persistence%20and%20Asynchronous%20Challenge%20Battle%20System.md)
<img width="1135" height="704" alt="image" src="https://github.com/user-attachments/assets/72dec38c-25fa-4e00-b10a-26a2bf15541d" />

설명: 런 데이터는 RunSaveCoordinator가 SaveData로 변환하고 SaveManager가 JSON I/O를 담당한다.
챌린지는 파티 스냅샷을 만들어 로컬/서버(PlayFab) 양쪽에 저장·조회하는 비동기 PvE 구조다.

### [4. 오브젝트 풀링을 이용한 최적화](https://github.com/YoungHyun1728/CommanderRogue/blob/main/ProjectDescription/4.%20Object%20Pooling%20for%20Combat%20Resource%20Optimization.md)
<img width="1144" height="280" alt="image" src="https://github.com/user-attachments/assets/294684e1-14e1-45a1-a770-ddbfd477d4e0" />

### [5. UI Hover Tooltip](https://github.com/YoungHyun1728/CommanderRogue/blob/main/ProjectDescription/5.%20Hover%20Tooltip-Based%20UI%20Interaction%20System.md)
스킬 툴팁

<img width="1095" height="175" alt="image" src="https://github.com/user-attachments/assets/ab1db234-6b21-4211-bbf6-b9457f143c8e" />

이벤트 선택지 툴팁

<img width="784" height="176" alt="image" src="https://github.com/user-attachments/assets/adc64137-50e6-4f69-9150-ce8f25c94e42" />

아이템 설명 호버 툴팁

<img width="791" height="178" alt="image" src="https://github.com/user-attachments/assets/382461b9-c3f1-415f-bc2f-2a034278e391" />

설명: 포인터 이벤트를 트리거 클래스에서 받아 툴팁 시스템/패널로 전달하는 구조다.
UI 로직과 데이터 표시를 분리해서 확장성과 유지보수성을 높였다.
