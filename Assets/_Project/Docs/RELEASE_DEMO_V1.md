# DEMO_V1 릴리스 노트

`DEMO_V1`은 Prometheus 챕터 0 튜토리얼을 처음부터 데모 엔딩까지 플레이할 수 있는 Windows·macOS 배포본입니다.

## 배포 파일

- `Prometheus_MVP_DEMO_V1_Windows.zip`
- `Prometheus_MVP_DEMO_V1_macOS.zip`
- `Prometheus_MVP_DEMO_V1_SHA256.txt`

압축을 완전히 해제한 뒤 Windows는 `Prometheus_MVP.exe`, macOS는 `Prometheus_MVP.app`을 실행합니다. Windows 실행 파일과 함께 배포된 `Prometheus_MVP_Data`, DLL 파일은 같은 폴더에 유지해야 합니다.

## 포함 내용

- 애니메이션 타이틀, 새 게임·이어하기·보스전·설정·종료 메뉴
- 지원 해상도 드롭다운과 창 모드·전체 화면·창 없는 전체 화면
- 회의장, 숨겨진 방, 복도, 순차 훈련장, 외부 전투 구역과 선착장
- 근접 공격, 원거리 공격, 테우스 집중포화, 프로메 4연속 참격
- 헬테 2페이즈 보스전, 보스 체력 HUD와 데모 엔딩
- BGM, 상황별 SFX, 대화 초상화, 타일·배경·캐릭터·VFX 통합본
- 일시정지 설정, 저장 후 타이틀 복귀와 이어하기

## 기본 설정

- 기본 해상도: `1920×1080`
- 실제 지원 해상도를 실행 PC에서 감지해 드롭다운에 표시
- 플레이 시작 씬: `Assets/Scenes/TitleScene.unity`
- Unity 버전: `6000.3.14f1`

## 핵심 조작

- 이동: `A/D` 또는 방향키
- 점프·더블 점프·활공: `Space`
- 대시: `Left Shift`
- 기본 공격: 마우스 왼쪽
- 원거리 공격 / 테우스 집중포화 / 4연속 참격: `1 / 2 / 3`
- 상호작용: `F`
- 일시정지: `Esc`

상세한 진행, 저장, 설정과 개발용 실행법은 저장소 루트의 [README](../../../README.md)를 참고합니다.
