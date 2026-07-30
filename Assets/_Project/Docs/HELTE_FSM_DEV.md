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

## 2026-07-30 — FSM 1차 폴리싱

### 재도전 초기화

- 전투 시작마다 `HeltePatternPlanner`를 초기화한다.
- 2페이즈 진입 연출 플래그를 초기화한다.
- 재도전 시 헬테를 개발 씬에 저장된 최초 위치·회전으로 복귀시킨다.
- 진행 중인 공격 Coroutine, 히트박스와 임시 연출 슬롯을 모두 정리한다.
- 이전 시도의 패턴 순서나 보스 위치가 다음 시도에 남아서는 안 된다.

### 블링크 돌진 흐름

```text
블링크 소멸
→ 좌/우 무작위 위치 재등장
→ 돌진 경로 예고
→ 무피해 돌진 이동
→ X 베기 예고
→ X 베기 판정
→ 후딜
```

- `DashTelegraph`와 `CrossSlashTelegraph`를 실제 이동·판정 상태와 분리했다.
- 돌진 경로는 `0.3초` 동안 먼저 보여준 뒤 이동한다.
- 돌진 이동 자체는 피해를 주지 않는다.
- 플레이어 또는 헬테가 사망하면 남아 있는 이동·경고·판정을 즉시 중단한다.

### 개발 씬 진입

- 부트스트랩 이후 HUD가 현재 퀘스트를 다시 읽도록 보정했다.
- 진입 확인 기준:
  - 진행: `튜토리얼 10/10 · TUTO_H_01`
  - 위치: `나디르 선착장`
  - 퀘스트: `QST-TUTO-008`
- 개발자 구간 스킵 시 비활성 상태였던 HUD도 현재 퀘스트로 강제 동기화한다.

### 테스트

- `HeltePatternPlanner_ResetRestartsTheOpeningSequence` EditMode 테스트 추가
- 헬테 개발 씬 PlayMode 테스트에 HUD 진행 ID와 위치 검증 추가
- Unity 스크립트 컴파일 오류 `0`
- Test Runner에서 EditMode 테스트 `47개` 발견 확인
