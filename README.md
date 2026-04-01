# CommanderRogue
2D 타일맵기반 오토배틀러 로그라이크

설명 영상 링크 : 
플레이 링크 : 

주요기능
1. 절차적 맵 기반 진행

GameScene에 진입시 MapGenerator 클래스가 Map을 생성합니다.

InitializeMap()은 이어하기 버튼으로 진입했다면 저장된 맵을 생성하고, 새로하기로 진입했다면 새로운맵을 생성합니다.



<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/f21a1152-525c-4c2d-97f3-ba003d0260cf" />

200라운드 + 50라운드 구성으로 총 250라운드로 진행되는 절차적 맵을 생성해 이어지는 노드들을 선택해 기본적인 진행루프를 구성했습니다.

<img width="77" height="64" alt="image" src="https://github.com/user-attachments/assets/2c7c3a8d-e556-4c8c-8d5a-b449bfdf2ee6" />전투 노드 클릭시
전투노드에 들어갑니다. 


2. FSM을 이용한 2D 타일맵 전투 구현

그 외 기능
1. PlayFab을 이용한 클리어 후 서버 저장 기반 PVE

2. UI Tooltip

3. Json을 이용한 로컬 저장 ( 강화 레벨 저장, 진행도, 파티 정보 )

4. 풀링을 이용한 최적화 ( 데미지 / 회복량, 투사체, VFX )


