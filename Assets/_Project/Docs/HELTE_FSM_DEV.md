# 헬테 FSM 개발 씬

- 전용 씬: `Assets/Scenes/HelteBossFsmDev.unity`
- 목적: 헬테 보스의 FSM, 패턴 타이밍, 히트박스, 전투 연출 슬롯을 튜토리얼 진행과 분리해 검증한다.
- PlayMode를 시작하면 저장 데이터를 변경하지 않고 `QST-TUTO-008` 헬테 구간으로 자동 이동한다.
- `TutorialScene.unity`에서는 헬테 FSM 실험이나 튜닝을 직접 진행하지 않는다.
- 검증이 끝난 설정만 별도 회귀 테스트 후 튜토리얼 씬에 반영한다.
- 이 씬은 개발 전용이므로 플레이어 빌드의 Build Settings에는 추가하지 않는다.

현재 검증 범위:

- 헬테 전용 부트스트랩 존재
- 선착장과 `H_Helte_Integration` 활성화
- 구형 `BossArena_EntryGate_ART_SLOT` 미존재
- `TutorialBossArenaHost` 참조 유효
- `HelteBossPatternHost` 존재
