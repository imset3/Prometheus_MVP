# 헬테 FSM 개발 — 보스 개발씬

- 전용 씬: `Assets/Scenes/BossDevelopmentScene.unity`
- 씬 이름은 이후 다른 보스도 같은 격리 환경을 재사용할 수 있도록 범용 `BossDevelopmentScene`으로 유지한다.
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

## 2026-07-31 — 우호적 패턴 프로토타입

이 기능은 `BossDevelopmentScene.unity`의 `Enable Friendly Pattern Prototype`에서만 활성화한다.
튜토리얼 씬에는 검증 전까지 승격하지 않는다.

- 기존 이도류 기본 공격, 블링크 X 베기, 칼 소환 패턴은 유지한다.
- 도입부에는 블링크 다음 `FakeBlink`가 등장한다.
  - 헬테가 사라졌다가 프로메 근처에 재등장하지만 공격하지 않는다.
  - `1.15초` 동안 빈틈을 보여 패닉 대시를 유도하는 장난성 패턴이다.
- 2페이즈에는 칼 소환과 블링크 다음 `CounterStance`가 등장한다.
  - 예고 `0.35초`, 카운터 판정 `0.75초`.
  - 자세 중 근접 공격하면 피해 없이 프로메를 밀어낸다.
  - 기다리면 `1.2초` 동안 헬테가 공격 가능한 상태로 열린다.
- 프로메 체력이 25% 이하가 되면 헬테는 후퇴하며, `20초` 뒤 다시 자비 행동을 사용할 수 있다.
  - 자비 행동이 사용 가능한 동안 현재 연계는 프로메 체력을 25% 아래로 낮추지 않는다.
  - 프로메 체력을 60%까지 복구하고 `1.8초` 기다린다.
  - 쓰러진 상대를 몰아붙이지 않는 헬테의 태도를 시스템으로 표현한다.
- 기본 공격 예고는 `0.38초`로 두어 첫 전투에서도 회피 방향을 읽을 수 있게 한다.
- 최종 시험은 `블링크 → 기본 → 카운터 → 칼 소환 → 기본 → 페이크` 6박 순환이다.

개발 계측 HUD는 페이크와 카운터 횟수를 별도로 집계한다.
아트 연결 시 `FakeBlinkVanish`, `FakeBlinkReappear`, `FakeBlinkPause`, `CounterTelegraph`,
`CounterStance`, `CounterSucceeded`, `CounterOpen`, `MercyRetreat` 상태에 VFX를 연결할 수 있다.

## 2026-07-31 — 실제 플레이 검증

- 개발 씬 자동 진입 → 선착장 이동 → 헬테 조우 대화 2줄 → 아레나 진입 순서 확인
- 이동 중 좁은 `BossArena_StartTrigger`를 한 프레임 사이에 지나가도 경로 스윕으로 전투 시작
- 보스 체력바 `5000/5000`과 전투 계측 오버레이 활성화
- 도입부 `BasicCombo → BlinkDash → FakeBlink` 순환 확인
- 프로메 체력 25% 구간에서 `MercyRetreat` 실행 확인
- Enter 기본 공격으로 헬테 체력 `5000 → 4750` 감소 확인
- 플레이어 사망 후 체크포인트·보스 전투 초기화 확인
- EditMode `50/50`, PlayMode `7/7`, Console 오류 `0`

UX 관찰:

- 기본 공격 예고 `0.38초`와 페이크 블링크 빈틈 `1.15초`는 실제 화면에서 구분 가능하다.
- 자비 행동은 `20초` 재충전과 60% 회복으로 두 차례 이상 재발동하는 것을 실제 플레이에서 확인했다.
- 공격을 전혀 회피하지 않는 플레이는 여전히 사망하지만, 대시를 섞어 패턴을 학습할 회복 구간은 확보됐다.
- 5분 완주 목표는 첫 3회 수동 완주에서 실제 유효 공격 횟수와 대시 성공률을 함께 측정해 최종 조정한다.

## 2026-07-31 — 전투 가독성 폴리싱

`BossCombatCuePresenter`는 색상만으로 상태를 구분하지 않고 다음 의미를 하단 안내로 표시한다.

| 상태 | 플레이어 안내 |
| --- | --- |
| `FakeBlinkVanish`, `FakeBlinkReappear` | 페이크 블링크 예고 |
| `FakeBlinkPause` | 이번 블링크는 공격하지 않는다는 공격 기회 |
| `CounterTelegraph` | 반격 자세 예고 |
| `CounterStance` | 지금 공격하면 밀려난다는 위험 안내 |
| `CounterSucceeded` | 공격이 막혔다는 결과 |
| `CounterOpen` | 카운터 종료 후 공격 기회 |
| `MercyRetreat` | 헬테가 거리를 두는 휴식 구간 |
| `FinalRushTransition` | 최종 시험 진입 |

- 2페이즈와 최종 시험 전환 중에는 헬테가 일시 무적이다. 페이즈 연출 도중 누적 공격으로 다음 구간을 건너뛰지 않는다.
- 보스 체력바는 피해 순간 흰색으로 점멸하고 실제 체력까지 부드럽게 감소해 타격 성공을 읽기 쉽게 한다.
- 헬테 PNG 시퀀스에 전용 클립이 아직 없어도 친화 패턴은 기존 슬롯으로 폴백한다.
  - 최종 시험 → `PhaseTransition`
  - 자비·페이크 빈틈·카운터 결과 → `Recover`
  - 페이크 소멸·재등장 → `BlinkVanish`, `BlinkReappear`
  - 카운터 예고 → `BasicWindup`
- 향후 전용 PNG 시퀀스를 받으면 상태 이름을 유지한 채 브리지 매핑만 전용 클립으로 교체한다.

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

- 헬테 최대 체력: `5000`
- 2페이즈 기준: 최대 체력의 `55%` (`2750`)
- 최종 러시 기준: 최대 체력의 `20%` (`1000`)
- 목표 전투 시간: `300초`
- 체력만 높은 전투를 피하기 위해 기존 `6000`에서 체력을 낮추고, 후반부의 이동·회복 템포를 높였다.

`BossArena_Controller`의 `HelteCombatTelemetryHost`가 전투 활성화부터 보스 처치까지 실제 시간을 잰다.
개발 씬에서는 우측 상단 오버레이로 경과 시간, 체력, 현재 템포, 기본 콤보·블링크·칼 소환 횟수를 확인할 수 있다.
보스 처치 시 Console에 목표 시간과의 차이 및 패턴 횟수가 기록된다. 재도전하면 시간과 패턴 카운트가 새로 시작된다.

### 전투 리듬

| 구간 | 패턴과 템포 |
| --- | --- |
| 도입 `100~55%` | 기본 공격 1~2회 뒤 블링크. 충분한 후딜로 회피와 공격 방식을 익힌다. |
| 2페이즈 `55~20%` | 기본 공격 1~2회 → 칼 소환 → 블링크. 회복 시간 25% 감소, 이동 시간 10% 감소, 투사체 속도 10% 증가. |
| 최종 러시 `20~0%` | 블링크 → 기본 → 칼 소환 → 기본의 4박 순환. 회복 시간 45% 감소, 이동 시간 22% 감소, 투사체 속도 20% 증가. |

예고 시간은 거의 그대로 유지한다. 난이도 상승은 보이지 않는 즉발 판정이 아니라 패턴 사이의 빈 시간 감소와 이동 속도 변화로 만든다.
2페이즈 피해량은 1.15배, 최종 러시는 1.3배지만 기본 피해가 낮아 튜토리얼 보스의 학습 성격을 유지한다.

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
- `FinalRushTransition`: 체력 20% 최종 시험 전환
- `FakeBlinkVanish`, `FakeBlinkReappear`, `FakeBlinkPause`: 공격하지 않는 페이크 블링크
- `CounterTelegraph`, `CounterStance`, `CounterSucceeded`, `CounterOpen`: 카운터 예고·판정·성공·빈틈
- `MercyRetreat`: 체력이 낮은 프로메에게 거리를 내주는 자비 행동
- `SwordFocus`: 칼 소환 집중
- `SwordVolley`: 칼 발사
- `Recover`: 선택적인 후딜 표시

VFX 오브젝트는 전투 로직의 Collider나 Rigidbody를 갖지 않는다. 최종 아트는 `Effect Root`만 교체하고 FSM과 히트박스 참조는 유지한다.
