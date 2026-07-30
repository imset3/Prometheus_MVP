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

## 5분 전투 기준과 계측

- 헬테 최대 체력: `6000`
- 2페이즈 기준: 최대 체력의 `55%` (`3300`)
- 목표 전투 시간: `300초`
- 현재 수치는 기본 공격과 원거리 공격을 함께 사용하는 실제 플레이를 전제로 한 1차 기준이다.

`BossArena_Controller`의 `HelteCombatTelemetryHost`가 전투 활성화부터 보스 처치까지 실제 시간을 잰다.
개발 씬에서는 우측 상단 오버레이로 경과 시간, 체력, 기본 콤보·블링크·칼 소환 횟수를 확인할 수 있다.
보스 처치 시 Console에 목표 시간과의 차이 및 패턴 횟수가 기록된다. 재도전하면 시간과 패턴 카운트가 새로 시작된다.

첫 3회 완주 결과를 기준으로 다음 순서로 조정한다.

1. 평균이 `270초` 미만이면 최대 체력을 높이거나 플레이어 공격 기회를 줄인다.
2. 평균이 `330초`를 넘으면 최대 체력을 낮추거나 헬테의 후딜을 늘린다.
3. 시간이 맞아도 특정 패턴 횟수가 지나치게 적으면 체력 대신 패턴 플래너와 회복 시간을 조정한다.

## 패턴 VFX 연결

`TutorialHelte`의 `HeltePatternVfxHost`에서 `Bindings` 배열을 편집한다.

| 항목 | 용도 |
| --- | --- |
| State | VFX를 시작할 FSM 상태 |
| Effect Root | 계층에 미리 배치한 VFX 또는 프리팹 인스턴스 |
| Anchor | 보스, 검, 히트박스 등 VFX가 붙을 Transform |
| Local Offset | Anchor 기준 위치 보정 |
| Follow Anchor | 상태 중 Anchor를 계속 따라갈지 여부 |
| Apply Anchor Rotation | Anchor 회전을 VFX에 적용할지 여부 |
| Restart Particle Systems | 상태 진입마다 ParticleSystem을 처음부터 재생 |
| Deactivate On State Exit | 다른 상태로 넘어갈 때 VFX를 끌지 여부 |

하나의 상태에 여러 Binding을 추가할 수 있다. 예를 들어 `CrossSlash`에 검광, 충격파와 화면 플래시 슬롯을 각각 연결할 수 있다.

권장 상태 연결:

- `BasicWindup`: 기본 공격 예고
- `BasicLeftSlash`, `BasicRightSlash`: 좌·우 검광
- `BlinkVanish`, `BlinkReappear`: 소멸·재등장
- `DashTelegraph`: 돌진 경로 예고
- `DashApproach`: 이동 잔상·트레일
- `CrossSlashTelegraph`, `CrossSlash`: X 베기 예고·타격
- `PhaseTransition`: 2페이즈 전환
- `SwordFocus`: 칼 소환 집중
- `SwordVolley`: 칼 발사
- `Recover`: 선택적인 후딜 표시

VFX 오브젝트는 전투 로직의 Collider나 Rigidbody를 갖지 않는다. 최종 아트는 `Effect Root`만 교체하고 FSM과 히트박스 참조는 유지한다.
