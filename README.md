# Prometheus MVP

`Prometheus`는 공중 구조물과 지형을 탐험하며 전투, 모듈, 보스 보상을 통해 회차 빌드를 구성하는 2D 액션 게임 프로토타입입니다.

현재 저장소에는 단일 `TutorialScene`에서 진행되는 튜토리얼 기본 로직과 `CHAPTER_01` 전환 흐름이 구현되어 있습니다. 캐릭터와 배경은 최종 아트 연결 전 단계이므로 도형과 명시적인 `ART_SLOT`을 사용합니다.

## 개발 환경

- Unity `6000.3.14f1`
- Unity Input System
- uGUI 기반 튜토리얼 HUD
- ScriptableObject 기반 퀘스트·모듈·보상 데이터

## 튜토리얼 구현 범위

튜토리얼은 하나의 씬 안에서 6개 구역을 페이드 전환하며 진행합니다.

1. `Z01` 아다마스 HQ — 프로메와 동료들의 작전 대화, 테우스 소개, 숨겨진 활공방의 비행선 패스키 획득 후 훈련장 이동
2. `Z02` 훈련장 — 대시 낙하물 회피, 점프·활공 투사체 회피, 공격과 펄스 훈련
3. `Z03` 장비 전달 — 크리온의 더블 점프 부츠와 모듈 전달, 장비 시험과 릴레이 활성화
4. `Z04` 외곽 전투 I — 순차 적 조우
5. `Z05` 외곽 전투 II — 다중 웨이브 조우
6. `Z06` 광물 저장고 — 세계관 자막, 헬테 조우와 보스전, 결과 화면

주요 시스템은 다음과 같습니다.

- 이동, 점프, 더블 점프, 활공, 대시
- 마우스·키보드·게임패드 방향을 반영하는 근접 3연속 공격
- 펄스 모듈 전방 투사체와 입력 미지정 상태의 원거리 공격 슬롯
- 공격 예고, 피격 무적 시간, 체력 UI와 헬테 보스 체력바
- 헬테 기본 공격, 블링크 돌진, X 베기, 2페이즈 칼 소환 패턴
- 스페이스바로 진행하는 대화와 테우스의 비차단 세계관 자막
- 플레이어 머리 위 목표 방향 화살표
- 구역별 체크포인트, 사망·훈련 실패 재시작, 페이드 전환
- 대시 훈련 낙하 전에 착지 위치를 알려주는 교체형 경고 도형
- 3초 지연 후 아무 키로 닫는 테우스 소개 카드와 잘못된 방향 4단계 반응
- 숨겨진 방의 활공 안내, 낙하 복구 상승기류, 비행선 패스키 저장·복원
- 대화·보스전·결과 화면별 HUD 겹침 방지
- 카메라 진행 방향 예측, 보스 프레이밍, 제한된 화면 흔들림
- 자막 크기·패널 대비·공격 경고 강도를 포함한 접근성 기본값
- 최종 모델 교체를 위한 `Visual_ART_BIND`, 발 위치, 콜라이더, 공격 판정 계약

세부 레벨 흐름과 좌표는 [TutorialLevelDesignPlan.md](Assets/_Project/Docs/TutorialLevelDesignPlan.md)를 참고합니다.

## 조작

| 동작 | 키보드·마우스 | 게임패드 |
| --- | --- | --- |
| 이동 | `A`/`D` 또는 방향키 | 왼쪽 스틱 / D-pad |
| 조준 방향 | 마우스 위치 | 오른쪽 스틱 |
| 점프·더블 점프·활공 | `Space` | South 버튼 |
| 근접 공격 | 마우스 왼쪽 / `Enter` | West 버튼 |
| 대시 | `Left Shift` | 왼쪽 스틱 클릭 |
| 펄스 모듈 | `2` | D-pad 오른쪽 |
| 모듈 트리 | `I` | D-pad 위쪽 |
| 인벤토리 | `Tab` | Select 버튼 |
| 상호작용 | `F` | North 버튼 |
| 대화 진행 | `Space` | South 버튼 |

원거리 공격 기능은 사전 배치되어 있지만 발동 입력은 아직 지정하지 않았습니다.

## 실행 방법

1. Unity Hub에서 저장소 폴더를 프로젝트로 추가합니다.
2. Unity `6000.3.14f1`로 열고 에셋 임포트와 스크립트 컴파일이 끝날 때까지 기다립니다.
3. 전체 빌드 흐름은 `Assets/_Project/Scenes/Boot.unity`에서 시작합니다.
4. 튜토리얼만 확인하려면 `Assets/Scenes/TutorialScene.unity`를 열고 Play를 누릅니다.

Build Settings에는 `Boot`, `TutorialScene`, `Chapter01` 순서로 등록되어 있습니다.

## 프로젝트 구조

```text
Assets/
  Scenes/
    TutorialScene.unity          # 단일 씬 튜토리얼
    Chapter01.unity              # 튜토리얼 이후 연결 씬
  _Project/
    Art/                         # 폰트, Material, 최종 아트 연결 지점
    Docs/                        # 레벨디자인 및 개발 문서
    GameData/Tutorial/           # 퀘스트 조건과 ScriptableObject 데이터
    Scenes/Boot.unity            # 빌드 시작 씬
    Scripts/
      Runtime/                   # 게임플레이, 프레젠테이션, 저장, 씬 흐름
      Editor/                    # 튜토리얼 씬 설정과 Validator
      Tests/                     # EditMode 테스트
      PlayModeTests/             # 오프닝부터 헬테 완료까지 런타임 흐름 테스트
  Docs/narthex_requirements_md_files/
                                # 기획 및 아키텍처 원본 문서
Packages/
ProjectSettings/
```

## 주요 설계 원칙

- 튜토리얼은 씬을 나누지 않고 구역 Root 활성화와 페이드로 전환합니다.
- 게임플레이와 UI 오브젝트는 씬에 사전 배치하고 런타임에는 상태와 참조를 갱신합니다.
- 퀘스트, 모듈, 능력, 보상은 Stable ID를 가진 ScriptableObject 데이터로 연결합니다.
- 저장된 완료 퀘스트를 기준으로 현재 체크포인트를 복구합니다.
- 플레이스홀더 비주얼과 콜라이더·공격 판정을 분리해 최종 아트 교체가 게임 로직에 영향을 주지 않도록 합니다.

## 검증

- Unity EditMode 테스트 어셈블리: `Narthex.Tests`
- Unity PlayMode 테스트 어셈블리: `Narthex.PlayModeTests`
- 현재 테스트 결과: EditMode `34/34`, PlayMode `3/3` 통과
- PlayMode 전체 흐름 검증: HQ 작전 대화 → 숨겨진 활공방 → 훈련 → 장비 전달 → 외곽 전투 2개 → 헬테 → 결과 화면
- 튜토리얼 씬 검증: `Narthex > Validation > Validate Active Tutorial Scene`
- 씬 자동 구성 도구: `Narthex > Tutorial > Apply Requested Gameplay Features`

## 남은 작업

튜토리얼 기본 로직은 완료됐으며, 이후 작업은 콘텐츠 완성과 폴리싱 단계입니다.

- 처음부터 헬테 처치까지 전체 플레이 QA와 난이도 조정
- 최종 캐릭터·배경·애니메이션을 `ART_SLOT`과 `Visual_ART_BIND`에 연결
- SFX, BGM, VFX 연결과 화면 피로도 실기 점검
- 헬테 패턴 수치, 판정, 연출 타이밍 조정
- 대화 문구와 자막 노출 타이밍 최종 교정
- 지원 해상도 및 실제 게임패드 테스트
