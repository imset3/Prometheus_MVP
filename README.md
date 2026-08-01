# Prometheus MVP

`Prometheus`는 공중 도시 제니스와 하층 세계 나디르를 배경으로 하는 2D 액션 게임 프로토타입입니다.  
현재 저장소에는 새로 배치된 챕터 0 튜토리얼 레벨과 대화, 훈련, 습격, 외부 전투, 헬테 보스전까지 이어지는 1회 플레이 흐름이 구현되어 있습니다.

캐릭터와 배경은 최종 아트 연결 전 단계입니다. 레벨은 흰색 도형을 중심으로 구성되어 있으며, 팀원이 구역별 아트와 스프라이트를 교체해도 게임 로직이 유지되도록 비주얼과 판정 오브젝트를 분리합니다.

## 개발 환경

- Unity `6000.3.14f1` (Unity 6.3 LTS)
- Unity Input System
- uGUI 기반 튜토리얼 HUD
- ScriptableObject 기반 퀘스트·조건·모듈 데이터
- 단일 씬 내 구역 활성화와 페이드 전환 방식
- 공식 Unity Pipeline MCP `com.unity.pipeline 0.4.0-exp.1`

## 현재 기준 씬

- 메인 튜토리얼: `Assets/Scenes/TutorialScene.unity`
- 빌드 시작 씬: `Assets/_Project/Scenes/Boot.unity`
- 튜토리얼 이후 연결 씬: `Assets/Scenes/Chapter01.unity`
- 보스 격리 개발씬: `Assets/Scenes/BossDevelopmentScene.unity`
- AI 아트 적용 리뷰씬: `Assets/Scenes/AIReview/TutorialScene_FPilot_Review.unity`
- AI 적용 전 보존본: `Assets/Scenes/AIReview/TutorialScene_Backup_PreAIPilot_20260801.unity`

기존 튜토리얼 씬은 팀원이 새로 배치한 레벨과 최신 기능이 통합된 `TutorialScene`으로 대체되었습니다. 구역 최고 부모는 한글 이름을 사용하고, 코드가 연결된 Manager·Host 오브젝트는 영문 이름을 유지할 수 있습니다.

`BossDevelopmentScene`은 헬테 FSM과 전투 밸런스를 튜토리얼 진행에서 분리해 검증하는 개발 전용 씬입니다. `AIReview` 아래 두 씬은 아트 적용 전후 비교용이며 플레이어 빌드에는 포함하지 않습니다. 최종 튜토리얼 기준은 계속 `TutorialScene`입니다.

## 챕터 0 진행 흐름

튜토리얼은 씬을 여러 개로 나누지 않습니다. 서로 떨어진 구역을 하나의 씬에 배치하고, 진행 조건을 만족하면 화면을 페이드한 뒤 다음 구역 시작점으로 이동합니다.

| 순서 | 구역 | 주요 내용 |
| --- | --- | --- |
| 1 | 회의장 | 프로메와 동료들의 작전 대화, 테우스 소개 |
| 2 | 숨겨진 방 | `F` 상호작용으로 사다리 이동, 테우스가 빛으로 변해 비행선 패스키를 비춤, 패스키 획득 |
| 3 | 회의장 | 패스키 획득 후 동료들과 대화, 훈련장 이동 결정 |
| 4 | 복도 | 훈련장으로 이동하는 연결 구간 |
| 5 | 훈련장 | 대시 → 더블 점프 → 점프 → 기본 공격 → 원거리 공격 순차 훈련 |
| 6 | 복도 | 훈련 종료 후 이동 중 습격 발생, 어두운 화면과 달리는 소리로 긴급 이동 연출 |
| 7 | 회의장 | 습격 상황 확인과 긴급 대화 |
| 8 | 복도 | 외부로 향하는 탈출 동선 |
| 9 | 외부 | 침공 상황 공개, 테우스의 세계관 자막 |
| 10 | 외부 전투 구역 1 | 구역 진입 시 적 동시 활성화, 전멸 후 다음 구역 개방 |
| 11 | 외부 전투 구역 2 | 두 그룹이 순차 활성화되는 전투, 전멸 후 진행 |
| 12 | 선착장·보스전 | 화염·바람·용암 기믹 통과, 헬테 조우와 보스전, 결과 흐름 |

플레이어가 빠르게 이동해도 필수 대화와 안내가 건너뛰어지지 않도록 이동 잠금, 구역 게이트, 진행 조건을 함께 사용합니다. 좌측 상단에는 현재 구역 이름이 표시됩니다.

## 훈련장

훈련장은 한 공간을 순차적으로 재사용합니다. 이전 행동을 다른 장소에서 미리 수행해도 훈련 횟수에 포함되지 않으며, 현재 단계가 완료된 뒤에만 다음 훈련 오브젝트가 활성화됩니다.

| 단계 | 훈련 | 완료·실패 규칙 |
| --- | --- | --- |
| 1 | 대시 | 불기둥 3개를 무적 대시로 통과한 뒤 오른쪽 도착 마커에 접촉 |
| 2 | 더블 점프 | 활성화된 발판을 이용해 가장 높은 발판의 도착 마커에 접촉 |
| 3 | 점프 | 오른쪽 벽에서 1초 간격으로 발사되는 낮은 투사체 회피. 피격 시 해당 훈련을 재시작 |
| 4 | 기본 공격 | 훈련 적에게 유효 근접 공격을 3회 적중 |
| 5 | 원거리 공격 | `2` 키로 관통 도형을 발사해 훈련 표적 3개 적중 |

모든 훈련을 완료하기 전에는 훈련장 출구가 열리지 않습니다.

## 조작

| 동작 | 키보드·마우스 | 비고 |
| --- | --- | --- |
| 이동 | `A` / `D`, 방향키 | 마지막 이동 방향이 캐릭터가 바라보는 방향 |
| 점프·더블 점프 | `Space` | 지상 점프 후 공중에서 한 번 추가 점프 |
| 활공 | 공중에서 `Space` 유지 | 버튼을 누르고 있을 때만 활공 |
| 대시 | `Left Shift` | 대시 중 무적, 종료 후 재사용 대기시간 `0.5초` |
| 기본 공격 | 마우스 왼쪽 | `0.5초` 안에 연속 입력하면 1 → 2 → 3타 |
| 원거리 공격 | `2` | 현재 바라보는 방향으로 발사, 재사용 대기시간 `1.5초` |
| 상호작용 | `F` | 사다리, 패스키 등 |
| 대화 진행 | `Space` | 필수 대화 한 줄씩 진행 |
| 인벤토리 | `Tab` | 보유 장비와 시스템 상태 확인 |
| 모듈 트리 | `I` | 모듈 시스템 확인 |

튜토리얼의 `2`번 공격은 **나르텍스 펄스 모듈이 아니라 프로메의 기본 원거리 공격**입니다. 모듈 시스템 자체는 유지하지만, 퀘스트와 펄스 모듈은 현재 튜토리얼 진행에서 사용하지 않습니다.

## 주요 시스템

### 이동과 전투

- 이동, 점프, 더블 점프, 활공, 무적 대시
- 플레이어 이동 방향에 따른 캐릭터 좌우 반전
- 3단 근접 공격 콤보
- 바라보는 방향으로 발사하는 관통 원거리 공격
- 공격 예고, 체력, 피격 무적 시간과 사망 처리
- 구역 전멸 조건과 문 잠금·해제

### 훈련과 재시작

- 현재 훈련 구역 안에서 수행한 행동만 집계
- 단계별 오브젝트 활성화와 로컬 재시작
- 플레이어 사망 시 현재 구역 시작점에서 재시작
- 전투 중 사망 시 해당 전투 구역의 적과 게이트 초기화
- 활공·환경 기믹 실패 시 가까운 안전 지점으로 복귀

### 외부 구역과 환경 기믹

- 전투 구역 1: 적 전체 동시 활성화
- 전투 구역 2: 이동 진행도에 따라 두 그룹 순차 활성화
- 플레이어를 추적하는 임시 도형 적
- 화염과 용암 접촉 피해
- 바람 상승기류와 `Space` 유지 활공
- 구간별 안전 지점 저장

### 헬테 보스전

- 전투 시작 시 화면 하단 보스 체력바 표시
- 이도류 일반 연속 공격
- 블링크 재배치 후 돌진과 X 베기
- 2페이즈 진입 후 세 자루 칼 순차 소환·발사
- 페이크 블링크, 카운터 자세, 저체력 자비 후퇴로 비적대적인 성격 표현
- 체력 `55%`에서 2페이즈, `20%`에서 최종 시험 진입
- 전환 중 무적 처리와 패턴별 VFX 연결 슬롯
- 목표 전투 시간 `300초`와 패턴 횟수를 기록하는 개발 계측 HUD
- 보스전 진입·완료 대화와 결과 흐름

### 대화와 HUD

- 노션 챕터 0 시나리오 순서를 기준으로 한 필수 대화
- 테우스의 비차단 세계관 자막
- 안내 카드 표시 후 `1초`가 지나면 아무 키로 닫기
- 좌측 상단 현재 위치 표시
- 현재 목표와 훈련 진행 횟수 표시
- 플레이어 머리 위 목표 방향 화살표
- 대화·안내·보스 체력바가 서로 가리지 않도록 HUD 상태 조정
- 불필요한 홀로그램 배경과 카메라 연출 제거

## PNG 시퀀스 적용 도구

메뉴 `sragon000 > Art > Character PNG Sequence Setup`에서 프로메와 헬테의 PNG 시퀀스를 계층 오브젝트에 적용할 수 있습니다.

- 프레임 파일명이 `000`, `001`, `002` 순서로 끝나면 숫자 기준으로 자동 정렬
- Idle·Run 등 일부 모션만 준비된 상태에서도 부분 적용
- 부모 도형의 크기를 넘지 않도록 자동 맞춤
- 스프라이트 발 위치를 기존 바닥 기준선에 정렬
- 이동 방향에 따른 좌우 반전
- Idle, Run, Jump, Fall, Attack 1·2·3 등 FSM 확장 가능
- 적용 초기화 시 생성된 비주얼을 제거하고 기존 도형 상태 복원

원본 PNG는 `Assets/_Project/Art/Motions/`, 도구가 생성한 Animation·Controller는 `Assets/_Project/Art/Generated/`에 저장합니다. Rigidbody와 충돌 판정은 캐릭터 부모에 유지하고, 자식 스프라이트는 시각 표현만 담당합니다.

## 프로젝트 구조

```text
Assets/
  Scenes/
    TutorialScene.unity              # 새 레벨이 통합된 현재 기준 씬
    BossDevelopmentScene.unity       # 헬테 FSM·밸런스 격리 개발씬
    AIReview/                        # AI 아트 적용 전후 검토용 씬
    Chapter01.unity
  TileMap/                           # 팀원이 공유한 구역별 TileMap 프리팹
  _Project/
    Art/
      Motions/                       # 원본 PNG 시퀀스
      Generated/                     # 자동 생성 Animation·Controller
      AIConcepts/                    # 배경·적·타일셋 AI 리뷰 에셋
      PlatformTiles/AI/              # 플랫폼 타일 생성본과 매니페스트
    Docs/                            # 인수인계·레벨 통합 문서
    GameData/Tutorial/               # 퀘스트·조건 ScriptableObject
    Scenes/Boot.unity
    Scripts/
      Runtime/Gameplay/              # 이동, 전투, 훈련, 적, 환경 기믹
      Runtime/Presentation/          # 대화, HUD, 페이드, 연출
      Runtime/SceneFlow/             # 진행과 HUD 상태 조정
      Editor/                        # 씬 적용·검증·PNG 도구
      Tests/                         # EditMode 테스트
      PlayModeTests/                 # 훈련·전체 흐름 런타임 테스트
```

## 도구 빠른 시작

팀원과 AI는 같은 `Prometheus Scene Toolkit`을 사용합니다.

1. Unity에서 `sragon000 > Prometheus Scene Toolkit`을 엽니다.
2. 씬 수정 전 `스냅샷` 탭에서 기준 스냅샷을 생성합니다.
3. 사람은 `마커` 탭에서 위치와 범위를 조정하고, AI는 JSON 명령을 먼저 `dryRun: true`로 실행합니다.
4. `Scene Doctor`와 `구역 흐름` 탭에서 참조·Collider·진행 연결을 검사합니다.
5. `기존 도구` 탭에서 검증 또는 플레이 테스트를 실행합니다.
6. 수정 후 스냅샷을 다시 생성해 변경 내용을 비교합니다.

`Legacy Migration`은 과거 씬 복구용입니다. 일반 레벨 작업에서는 실행하지 않으며, 실행 전에 반드시 씬 스냅샷을 남깁니다.

## 공식 Unity MCP

Unity 자동화는 비공식 `unityMCP` 대신 공식 Unity Pipeline MCP만 사용합니다.

- 패키지: `com.unity.pipeline 0.4.0-exp.1`
- Codex 서버 명령: `unity mcp --project-path <프로젝트 경로>`
- 씬 수정 전 스냅샷과 `dryRun` 실행
- 수정 후 Scene Doctor, 사후 스냅샷 비교와 Console 오류 확인
- 수작업으로 배치된 씬에는 `Legacy Migration` 자동 적용 금지

AI는 `PrometheusAiCommandRunner`의 JSON 명령을 우선 사용하고, 사람은 `sragon000 > Prometheus Scene Toolkit`에서 같은 기능을 사용할 수 있습니다.

## 문서

- [개발일지](Assets/_Project/Docs/DEVLOG.md) — 주요 기능 변경, 검증 결과와 Git 이력
- [AI Scene Toolkit 사용 설명서](Assets/_Project/Docs/AI_SCENE_TOOLKIT.md) — 사람·Codex·Claude Code·Unity MCP 공통 작업 방법
- [마커 기반 레벨 저작 가이드](Assets/_Project/Docs/TUTORIAL_MARKER_AUTHORING.md) — 마커 이동만으로 기능 위치를 조정하는 규칙
- [프로젝트 인수인계](Assets/_Project/Docs/PROJECT_HANDOFF.md) — 현재 구현 상태, 시나리오와 주의사항
- [튜토리얼 레벨 통합 계획](Assets/_Project/Docs/TutorialImportedLevelIntegrationPlan.md) — 구역 통합 구조와 작업 기준
- [헬테 FSM 개발 문서](Assets/_Project/Docs/HELTE_FSM_DEV.md) — 분리된 보스전 개발 기준

## 실행 방법

1. Unity Hub에서 저장소 폴더를 프로젝트로 추가합니다.
2. Unity `6000.3.14f1`로 열고 에셋 임포트와 스크립트 컴파일이 끝날 때까지 기다립니다.
3. `Assets/Scenes/TutorialScene.unity`를 엽니다.
4. Play를 눌러 회의장부터 헬테 보스전까지 확인합니다.

빌드 전체 흐름을 확인할 때는 `Assets/_Project/Scenes/Boot.unity`에서 시작합니다.

## 검증 도구

- 활성 튜토리얼 씬 검사: `sragon000 > Validation > Validate Active Tutorial Scene`
- 훈련장 마커 검사: `sragon000 > Validation > Validate Training Marker Layout`
- 수정 훈련장 플레이 테스트: `sragon000 > Tests > Tutorial > Imported Training`
- 전체 튜토리얼 플레이 테스트: `sragon000 > Tests > Tutorial > Full Tutorial`
- G→H 구간 플레이 테스트: `sragon000 > Tests > Tutorial > G Wind To H`
- 전체 도구 모음: `sragon000 > Prometheus Scene Toolkit > 기존 도구`
- EditMode 테스트 어셈블리: `Narthex.Tests`
- PlayMode 테스트 어셈블리: `Narthex.PlayModeTests`

최근 공식 MCP 검증 결과는 EditMode `53/53`, PlayMode `7/7`, Console 오류 `0`입니다. 보스 개발씬 테스트에는 전투 진입, 2페이즈·최종 시험 전환, 전환 무적 해제와 처치 후 계측 종료가 포함됩니다.

검증 시에는 새 레벨 씬을 연 상태인지 먼저 확인합니다. `Legacy Migration`은 자동 실행되지 않으며, 명시적인 복구 작업이 아니라면 팀원이 배치한 씬에 적용하지 않습니다.

## 아트 연결 원칙과 남은 작업

현재 구현은 레벨 흐름과 기능 검증을 위한 블록아웃 단계입니다.

`Assets/_Project/Art/AIConcepts`와 `Assets/_Project/Art/PlatformTiles/AI`의 결과물은 최종 아트가 아니라 팀 검토용 후보입니다. `AIReview` 씬에서 비교한 뒤 승인된 에셋만 `TutorialScene`에 수작업으로 승격합니다.

- 구역 최고 부모의 위치는 유지하면서 내부 도형을 최종 배경·플랫폼 스프라이트로 교체
- 플레이어, 동료, 적, 헬테의 최종 PNG 시퀀스 연결
- 임시 도형 적을 실제 Enemy 프리팹과 FSM으로 교체
- 화염·바람·용암 VFX와 피격 피드백 연결
- 대화창 초상화, SFX, BGM과 환경음 연결
- 전체 플레이 난이도, 이동 거리, 자막 노출 시간 최종 조정
- 지원 해상도와 실제 게임패드 QA

최종 아트 교체 과정에서도 부모 Rigidbody, Collider, 진행 Trigger, Stable ID는 유지해야 합니다.
