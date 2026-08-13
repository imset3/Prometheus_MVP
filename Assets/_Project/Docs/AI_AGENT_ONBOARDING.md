# Prometheus 신규 에이전트 기술 가이드

이 문서는 Codex, Claude Code 또는 다른 자동화 에이전트가 프로젝트를 처음 접했을 때 탐색에 시간을 낭비하지 않고 안전하게 수정·검증·배포하도록 만든 운영 기준서다.

> 기준일: 2026-08-13  
> 기준 브랜치/릴리스: `main` / `DEMO_V4`  
> 기준 배포: `DEMO_V4`  
> Unity: `6000.3.14f1`  
> 프로젝트 루트: `/Users/limseth/Unity/Unity_Projects/Prometheus_MVP`

커밋이 바뀌면 이 문서의 숫자보다 실제 코드, 씬, 테스트 결과를 우선한다. 제품명은 `Prometheus`지만 과거 프로젝트명인 `Narthex`가 namespace, asmdef, 저장 키에 남아 있다. 명시적인 마이그레이션 작업 없이 이를 일괄 변경하지 않는다.

## 1. 5분 안에 작업 준비하기

1. 프로젝트 루트에서 `AGENTS.md`, 이 문서, `AI_SCENE_TOOLKIT.md`를 읽는다.
2. `git status --short`로 사용자 변경 사항을 확인한다. 다른 사람의 변경을 복원하거나 덮어쓰지 않는다.
3. Unity 공식 MCP의 프로젝트 경로가 이 저장소와 일치하는지 확인한다.
4. Unity가 Play Mode 또는 컴파일 중이면 끝날 때까지 기다린다.
5. 수정 대상을 `rg`, MCP `find_gameobjects`, 컴포넌트 직렬화 조회로 먼저 특정한다.
6. 씬을 수정한다면 변경 전 스냅샷을 찍고 AI 명령을 `dryRun: true`로 먼저 실행한다.
7. 변경 후 컴파일, Console, Validator, 관련 테스트를 실행한다.
8. 씬 변경이면 Scene Doctor와 변경 후 스냅샷 비교까지 끝낸다.

셸 명령은 로컬 지침에 따라 항상 `rtk`를 앞에 붙인다.

```bash
rtk git status --short
rtk rg -n "TutorialJumpTrainingHost" Assets/_Project/Scripts
rtk git diff --check
```

## 2. 절대 지켜야 할 불변 조건

- 팀원이 배치한 씬의 자식 오브젝트를 통째로 재구성하지 않는다.
- `.unity` YAML을 직접 편집하지 않는다. 씬·마커·직렬화 값은 Unity MCP와 `PrometheusAiCommandRunner`를 우선한다.
- 씬 변경 전 `snapshot.capture`, 변경 후 `scene.doctor.scan`과 `snapshot.compare`를 수행한다.
- 모든 변경 명령은 먼저 `dryRun: true`로 예상 대상을 확인한다.
- 마커 ID를 좌표보다 우선한다. 마커가 없을 때만 계층 경로를 사용한다.
- `sragon000/Legacy/Tutorial Migration` 명령은 사용자가 리셋 또는 마이그레이션을 명시하지 않으면 실행하지 않는다.
- 플레이어 진행 ID, 퀘스트 ID, 저장 키는 호환성 계약이다. 임의로 이름을 바꾸지 않는다.
- 게임플레이 로직은 프레임 독립적이어야 한다. 물리는 `FixedUpdate`와 `Time.fixedDeltaTime`, 비물리 연출은 `Time.deltaTime`, 일시정지 중 UI는 `Time.unscaledDeltaTime`을 사용한다.
- 새 UI·게임플레이 오브젝트는 가능한 한 실제 씬 계층에 둔다. 현재 의도된 예외는 `TutorialPauseMenuHost`가 설치하는 일시정지 UI다.
- 아트 오브젝트와 충돌·진행 오브젝트를 분리한다. 스프라이트 교체가 Rigidbody2D, Collider2D, Trigger, 마커를 바꾸면 안 된다.
- 빌드와 릴리스는 테스트를 통과한 동일 커밋에서 만든다.

## 3. 현재 게임 흐름과 씬

Build Settings의 필수 순서는 다음과 같다.

| 순서 | 씬 | 역할 |
| --- | --- | --- |
| 0 | `Assets/Scenes/TitleScene.unity` | 타이틀, 새 게임, 이어하기, 설정, 로딩 |
| 1 | `Assets/_Project/Scenes/Boot.unity` | 호환용 부트스트랩 |
| 2 | `Assets/Scenes/TutorialScene.unity` | 챕터 0 전체 플레이 |
| 3 | `Assets/Scenes/Chapter01.unity` | 이후 연결용 자리 |

`BossDevelopmentScene`은 프로젝트 내부 Editor 개발용이며 릴리즈 Build Settings에는 넣지 않는다.

`TutorialScene` 한 씬 안에서 구역을 활성화·비활성화하고 페이드 전환한다. 구역을 서로 물리적으로 연결할 필요는 없다.

### 튜토리얼 퀘스트 순서

| ID | 내용 | 주의점 |
| --- | --- | --- |
| `QST-TUTO-001` | 회의장 → 숨겨진 방 → 패스키 → 복귀 | 숨겨진 방을 건너뛰면 안 됨 |
| `QST-TUTO-004` | 대시 훈련 | 불기둥 통과 후 도착 마커 |
| `QST-TUTO-006` | 더블점프 훈련 | 가장 높은 발판의 정상 마커 접촉 |
| `QST-TUTO-002` | 전방 투사체 회피 | 실제 회피 3회, 단순 점프 횟수 아님 |
| `QST-TUTO-003` | 근접 공격 | 중앙 허수아비 피격 |
| `QST-TUTO-005` | 원거리 공격 | 중앙 허수아비 3기, 스킬 `1` 해금 |
| `QST-TUTO-007` | 습격·외부 이동 | 사다리 상승과 외부 진격 연출 |
| `QST-TUTO-007-A` | F 전투 | 적 전멸 후 문 개방, 스킬 `2` 해금 |
| `QST-TUTO-007-B` | G 전투 | 두 그룹, 바람·용암, H 이동 |
| `QST-TUTO-008` | 헬테·엔딩 | 스킬 `3`, 보스전, 비행정 엔딩 |

내부 체크포인트 `TUTO_A_01`, `TUTO_B_01`, `TUTO_A_RETURN`은 별도 퀘스트가 아니다. 패스키 저장 ID는 `ITEM-ZENITH-AIRSHIP-PASSKEY`다.

### 현재 사용자 입력

| 입력 | 동작 |
| --- | --- |
| `A / D` | 이동 |
| `Space` | 점프·활공 유지·대화 진행 |
| `Mouse Left / Enter` | 기본 공격 |
| `Left Shift` | 대시 |
| `1` | 프로메 원거리 공격 |
| `2` | 테우스 집중포화 |
| `3` | 프로메 4연속 참격 |
| `F` | 상호작용 |
| `Esc` | 일시정지 |
| `F8` | 다음 구간과 진행 상태 함께 스킵 |
| `F9` | 헬테 직전과 진행 상태 함께 스킵 |

## 4. 코드 구조

의존 방향은 아래에서 위로 유지한다.

```text
Core
 ├─ Content
 ├─ Save
 └─ Gameplay
      ├─ SceneFlow
      └─ Presentation

Editor/Tools → 모든 Runtime asmdef
Tests       → Runtime asmdef
```

| 영역 | 경로 | 책임 |
| --- | --- | --- |
| Core | `Scripts/Runtime/Core` | 이벤트 버스, 서비스 초기화, 공통 상태 |
| Content | `Scripts/Runtime/Content` | 정의·데이터 모델 |
| Save | `Scripts/Runtime/Save` | 저장, 초기화, 이어하기 |
| Gameplay | `Scripts/Runtime/Gameplay` | 플레이어, 전투, 퀘스트, 훈련, 적, 헬테 |
| SceneFlow | `Scripts/Runtime/SceneFlow` | 타이틀, 전환, HUD 모드, 일시정지, 엔딩 |
| Presentation | `Scripts/Runtime/Presentation` | 대화, 애니메이션, BGM/SFX, HUD 표시 |
| Editor | `Scripts/Editor` | AI 명령, 배치 자동화, Validator, 빌드 |
| EditMode Tests | `Scripts/Tests`, `Scripts/EditorTests` | 순수 정책·도구 회귀 테스트 |
| PlayMode Tests | `Scripts/PlayModeTests` | 실제 씬과 런타임 흐름 테스트 |

### 가장 먼저 찾을 클래스

| 수정 요구 | 시작 파일 |
| --- | --- |
| 이동·점프·대시 | `Gameplay/PlayerMotorHost.cs`, `PlayerInputHost.cs` |
| 공격·체력 | `Gameplay/CombatActorHost.cs`, 공격 Host, `Presentation/CombatHealthTextPresenter.cs` |
| 훈련 순서 | `Gameplay/TutorialTrainingPhaseControllerHost.cs`, `TutorialImportedTrainingFlowHost.cs` |
| 점프 투사체 | `Gameplay/TutorialJumpTrainingHost.cs`, `TutorialJumpProjectileHazardHost.cs` |
| 퀘스트 흐름 | `Gameplay/TutorialQuestSequenceHost.cs`, `TutorialNarrativeSequenceHost.cs` |
| 대화 | `Presentation/TutorialDialoguePresenter.cs`, `DialogueViewModule.cs` |
| 유도 화살표 | `Presentation/TutorialObjectiveBeaconHost.cs` |
| 적 이동 | `Gameplay/TutorialGroundedEnemyMotorHost.cs` |
| F/G 전투 | `Gameplay/TutorialSequentialEncounterHost.cs`, `TutorialSimultaneousEncounterHost.cs` |
| 헬테 | `Gameplay/HelteBossPatternHost.cs`, 관련 FSM·정책 클래스 |
| BGM/SFX | `Presentation/TutorialMusicDirector.cs`, `TutorialSfxDirector.cs` |
| 저장·이어하기 | `Save/SaveSystem.cs`, `SceneFlow/GameLaunchSession.cs` |
| 타이틀·설정 | `SceneFlow/TitleScreenHost.cs` |
| 일시정지 | `SceneFlow/TutorialPauseMenuHost.cs` |
| 데모 엔딩 | `SceneFlow/TutorialDemoEndingSequenceHost.cs` |
| F8/F9 | `Presentation/TutorialDebugSectionSkipHost.cs` |

## 5. 중요한 현재 구현 상태

- 점프 훈련 투사체는 오른쪽 대포 발사 마커에서 출발하고 이동 시간은 `2.8초`다.
- 점프 훈련은 `ProjectileAvoided` 3회만 완료로 인정한다. 피격 시 0으로 초기화한다.
- 크리온 소개 카드는 `QST-TUTO-007`에 연결되지 않으며 표시되면 회귀다.
- 플레이어 최대 체력은 `500`이며 `PlayerHealthBarTrack/Fill/ValueText`가 게이지와 `현재 / 최대`를 표시한다.
- 설정 UI에는 전체 음량 하나만 노출한다. 저장 데이터의 `MusicVolume`, `SfxVolume` 필드는 구버전 호환을 위해 남아 있지만 UI에서는 항상 1로 정규화한다.
- F/G 전투 적은 Dynamic Rigidbody2D, 중력 3, 회전 고정, 고체 몸 Collider와 공용 지상 모터를 사용한다.
- 외부 행군 병력은 전투 AI가 아니라 연출 전용이다.
- 테우스는 숨겨진 방 이후에도 같은 인스턴스로 동행한다.
- 원거리 공격 해금은 훈련 완료 후 `1`, 집중포화는 F에서 `2`, 4연속 참격은 헬테에서 `3`이다.
- 타이틀에는 보스전 버튼이나 `BossDevelopmentScene` 직접 로드 경로를 두지 않는다.
- 데모 엔딩 중에는 HUD와 레벨 오브젝트를 숨기고 기존 제니스와 비행정만 보여준다.

## 6. 씬 수정 표준 절차

씬을 건드리지 않는 순수 코드 수정은 이 절차에서 스냅샷 단계를 생략할 수 있다. Transform, 활성 상태, 직렬화 참조, UI 계층, 마커를 바꾸면 전부 적용한다.

### 6.1 변경 전

1. 올바른 씬을 연다.
2. Play Mode가 아닌지 확인한다.
3. `snapshot.capture`로 기준 스냅샷을 저장한다.
4. 대상의 hierarchy path, marker ID, component type과 현재 값을 조회한다.

요청 파일은 `Temp/PrometheusSceneToolkit/request.json`, 응답은 `response.json`이다.

```json
{
  "version": "1",
  "requestId": "unique-change-preview",
  "command": "object.transform",
  "scenePath": "Assets/Scenes/TutorialScene.unity",
  "dryRun": true,
  "arguments": [
    { "key": "markerId", "value": "G-WIND-ENTRY" },
    { "key": "x", "value": "120.5" },
    { "key": "y", "value": "18" },
    { "key": "z", "value": "0" },
    { "key": "rotationZ", "value": "0" }
  ]
}
```

Unity 메뉴 `sragon000/AI Toolkit/Run Pending Command`를 실행한다. 응답의 대상과 before/after가 의도와 맞을 때만 `dryRun: false`로 다시 실행한다.

### 6.2 변경 후

1. 씬을 저장한다.
2. `scene.doctor.scan`을 실행한다.
3. 변경 후 스냅샷을 캡처하고 `snapshot.compare`를 실행한다.
4. 예상하지 않은 추가·삭제·수정이 있으면 커밋하지 않고 원인을 찾는다.
5. Validator와 관련 테스트를 실행한다.

Scene Doctor는 현재 튜토리얼·보스 개발씬에서 저작용 비가시 Collider 경고 약 54개를 보고할 수 있다. 숫자를 무조건 무시하지 말고 변경 전후의 개수와 hierarchy path를 비교한다. 새 경고나 플레이 경로를 가로막는 Collider는 결함으로 취급한다.

## 7. AI 명령 선택표

전체 명령 목록은 `PrometheusAiCommandRunner.cs`의 `SupportedCommands`가 최종 진실이다.

| 작업 | 권장 명령 |
| --- | --- |
| 씬 현황 | `scene.report` |
| 씬 위험 검사 | `scene.doctor.scan` |
| 스냅샷 | `snapshot.capture`, `snapshot.compare` |
| 마커 | `marker.list`, `marker.create`, `marker.move` |
| 위치·활성화 | `object.transform`, `object.set-active` |
| 직렬화 값 | `component.inspect`, `component.set` |
| 통행 공간 | `tilemap.clearance.audit`, `tilemap.clearance.apply` |
| 훈련 더미 | `tutorial.training-dummies.apply` |
| 적 물리 | `tutorial.enemy-physics.apply` |
| 적 투사체 아트 | `tutorial.enemy-projectile-art.apply` |
| 바람·대화 아트 | `tutorial.wind-dialogue-art.apply` |
| UI 폴리싱 | `tutorial.ui-polish.apply` |
| 플레이어 체력바 | `hud.player-health.apply` |
| 헬테 모션 | `boss.helte-animation-v2.apply`, `boss.helte-animation-v2.pacing` |
| BGM/SFX | `audio.music.apply`, `audio.sfx.apply` |
| 엔딩 | `tutorial.demo-ending.apply` |
| 구역 흐름 | `flow.validate` |
| 코드 영향 검색 | `code.usage` |

범용 명령으로 충분하지 않은 반복 작업은 제한된 새 AI 명령을 만든다. 대상 씬과 오브젝트 수를 검증하고, dry-run과 테스트를 반드시 지원해야 한다.

## 8. 수정 유형별 빠른 경로

### 단순 코드 버그

1. 관련 Host와 정책 테스트를 찾는다.
2. 실패를 재현하는 최소 EditMode 테스트를 먼저 추가한다.
3. 런타임 코드를 수정한다.
4. 컴파일 오류를 확인한다.
5. 관련 테스트 → 전체 EditMode → 필요 시 PlayMode 순으로 실행한다.

### 씬 배치·마커·충돌 문제

1. 플레이 중 멈춘 상태라면 Collider bounds와 hierarchy path를 먼저 읽는다.
2. Edit Mode로 돌아간다.
3. 스냅샷 → clearance audit 또는 component inspect → dry-run → apply를 수행한다.
4. 실제 계층에 반영됐는지 확인한다. 런타임 임시 생성으로 문제를 숨기지 않는다.
5. Scene Doctor, Validator, 해당 구간 PlayMode 테스트를 수행한다.

### 훈련 진행 문제

다음 네 가지를 함께 확인한다.

- 현재 quest ID
- 현재 `TutorialTrainingPhaseControllerHost` 단계
- 해당 단계의 root 활성 상태
- 완료 신호가 입력 횟수가 아니라 실제 목표 조건에서 발행되는지

허수아비나 투사체가 Editor에서는 보이지만 빌드에서 사라진다면 `EditorOnly` 태그, 마커 의존 런타임 생성, Build Settings 포함 여부를 먼저 확인한다.

### 적이 벽을 통과하거나 떠다니는 문제

- Rigidbody2D가 Dynamic인지
- gravityScale이 3인지
- Freeze Rotation이 켜졌는지
- 몸 Collider는 solid이고 공격 Hitbox만 Trigger인지
- `TutorialGroundedEnemyMotorHost`가 연결됐는지
- 발판과 벽이 물리 레이어에서 충돌하는지
- Transform 직접 이동이 남아 있지 않은지

적 교체 후에는 `tutorial.enemy-physics.apply`를 dry-run으로 실행해 정확히 F/G 전투 적 7기만 대상인지 확인한다.

### UI 문제

- 씬 저작 UI인지 런타임 설치 예외인지 먼저 구분한다.
- Canvas Scaler 기준 해상도와 RectTransform anchor/pivot를 확인한다.
- 텍스트가 프레임 밖으로 나가면 Best Fit으로 숨기지 말고 안전 영역과 wrap을 고친다.
- 대화, 보스, 엔딩 모드의 억제 배열은 `TutorialHudStateCoordinator`에서 확인한다.
- 타이틀과 일시정지 설정에는 현재 전체 음량만 있어야 한다.

### 저장·새 게임·이어하기 문제

- `GameLaunchSession`의 타이틀 정책
- `SaveSystem.ResetProgressForSceneStart()`
- `DevelopmentProgressResetManager`
- 현재 quest ID와 저장된 player position
- 데모 완료 시 continue 비활성화

새 게임은 진행 상태를 초기화하되 설정은 유지한다. 타이틀 초기화 버튼은 진행과 설정을 모두 초기화한다.

## 9. 테스트와 검증

### 최소 검증 단계

| 위험도 | 필수 검증 |
| --- | --- |
| 문서만 수정 | 링크·경로 확인, `git diff --check` |
| 순수 정책 코드 | 컴파일, 관련 EditMode 테스트 |
| 런타임 기능 | 컴파일, 관련 테스트, 전체 EditMode |
| 씬·진행·UI | Validator, Scene Doctor, 관련 PlayMode, 전체 EditMode |
| 빌드·릴리스 후보 | EditMode 전체, PlayMode 전체, Windows 후보 실기 확인 |

Unity 메뉴:

- `sragon000/Validation/Validate Active Tutorial Scene`
- `sragon000/Validation/Validate Training Marker Layout`
- `sragon000/Validation/Run All EditMode Tests`
- `sragon000/Validation/Run All PlayMode Tests`

MCP에서는 `run_tests`를 비동기로 시작하고 `test_status`를 폴링한다. 도구 호출이 300초에 timeout되어도 Unity 테스트가 끝났을 수 있으므로, 곧바로 실패로 판단하지 말고 `test_status`의 summary를 확인한다.

현재 기준:

- EditMode: `123/123`
- PlayMode: `15/15`
- Tutorial Scene Validator: 통과
- Training Marker Validator: 통과

핵심 PlayMode 파일은 `Assets/_Project/Scripts/PlayModeTests/TutorialSceneRuntimeSmokeTests.cs`다. 타이틀, 저장, 훈련, F/G, 바람, 헬테, UI와 엔딩을 실제 씬으로 검증한다.

## 10. 디버그와 복구

| 증상 | 먼저 볼 곳 |
| --- | --- |
| MCP가 응답하지 않음 | Editor status, 컴파일·domain reload, 프로젝트 경로 |
| 컴파일 도구 timeout | `recompile_status`; 완료 상태와 errors 배열 확인 |
| 테스트 도구 timeout | `test_status`; summary와 실패 목록 확인 |
| 씬에서 되지만 빌드에서 안 됨 | `EditorOnly`, Build Settings, 런타임 마커 fallback, 직렬화 참조 |
| 진행 없이 위치만 이동 | `TutorialDebugSectionSkipHost`의 quest/checkpoint 동기화 |
| 화면이 검게 남음 | 전환 coroutine, HUD mode, 활성 구역, fade CanvasGroup |
| 보이지 않는 벽 | `tilemap.clearance.audit`, Collider bounds, 비활성 문 상태 |
| 캐릭터가 공중에 뜸 | Sprite pivot과 Collider bottom, Rigidbody2D 중력 |
| 대화가 반복됨 | narrative event 중복 발행, pending queue, introduction definition 연결 |
| UI가 엔딩에 남음 | `TutorialHudStateCoordinator` result suppression group |

실패 원인을 고치지 않고 테스트 기대값만 바꾸지 않는다. 실제 요구가 변경된 경우에만 구현과 테스트를 함께 변경한다.

## 11. 빌드와 GitHub 릴리스

빌드 자동화: `Assets/_Project/Scripts/Editor/PrometheusReleaseBuildAutomation.cs`

Unity 메뉴:

- `sragon000/Build/Release/Build Windows x64`
- `sragon000/Build/Release/Build macOS`
- `sragon000/Build/Release/Build Windows and macOS`

현재 출력 루트는 `Builds/Release/DEMO_V4`다. 새 버전 배포 전 자동화 경로, README, 릴리스 노트의 버전을 함께 올린다.

릴리스 순서:

1. 작업 트리가 의도한 변경만 포함하는지 확인한다.
2. EditMode와 PlayMode 전체를 통과한다.
3. 커밋하고 `main`에 푸시한다.
4. 같은 커밋에서 Windows 후보를 빌드하고 실기 확인한다.
5. 같은 커밋에서 macOS를 빌드한다.
6. 각각 ZIP을 만들고 SHA256과 ZIP 무결성을 검증한다.
7. GitHub Release를 생성하고 tag가 정확한 커밋을 가리키는지 확인한다.
8. 원격 asset digest와 로컬 SHA256을 비교한다.

기본 해상도는 `1920×1080`이다. 타이틀 설정의 계층에 미리 배치된 해상도 드롭다운에 현재 디스플레이 지원 모드를 연결하며, 적용 후 10초 유지 확인에 실패하면 이전 설정으로 되돌린다.

## 12. Git 작업 규칙

- 시작과 종료에 `git status --short`를 확인한다.
- 사용자 또는 팀원의 기존 변경을 임의로 stage하지 않는다.
- 관련 없는 씬 저장 변경이 섞이면 원인을 확인한다.
- `git diff --check`를 통과시킨다.
- 커밋 메시지는 변경 결과를 설명한다.
- 빌드 산출물은 Git에 커밋하지 않고 GitHub Release 자산으로 올린다.
- 릴리스 tag가 축약 SHA가 아니라 원격에 존재하는 branch 또는 완전한 commit을 가리키는지 확인한다.

## 13. 완료 보고 형식

다른 에이전트는 작업 종료 시 최소한 아래를 보고한다.

- 변경한 사용자 체감 동작
- 수정한 주요 파일과 씬
- 실행한 Validator와 테스트 결과
- Scene Doctor의 신규 문제 유무
- 빌드 여부와 로컬 경로
- 커밋 SHA, 푸시 브랜치, 릴리스 URL
- 남은 위험 또는 직접 확인이 필요한 항목

“작동할 것 같다”가 아니라 검증 결과를 숫자로 쓴다.

## 14. 관련 문서

- [프로젝트 핸드오프](PROJECT_HANDOFF.md)
- [AI Scene Toolkit](AI_SCENE_TOOLKIT.md)
- [튜토리얼 마커 저작 가이드](TUTORIAL_MARKER_AUTHORING.md)
- [튜토리얼 E2E 체크리스트](TutorialEndToEndChecklist.md)
- [통합 레벨 계획](TutorialImportedLevelIntegrationPlan.md)
- [개발일지](DEVLOG.md)
- [DEMO_V4 릴리스 노트](RELEASE_DEMO_V4.md)

이 문서와 실제 코드가 충돌하면 다음 순서로 판단한다.

1. 사용자의 최신 명시 요구
2. 자동화 테스트가 표현하는 현재 계약
3. 실제 씬 직렬화 상태
4. 이 문서
5. 과거 설계·마이그레이션 문서
