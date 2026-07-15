# Prometheus MVP

`Prometheus`는 공중 구조물과 지형을 탐험하며 전투, 모듈, 보스 보상을 통해 회차 빌드를 구성하는 2D 액션 게임 프로토타입입니다.

현재 저장소는 튜토리얼부터 `CHAPTER_01` 진입까지의 기능 검증 범위를 담고 있습니다.

## 현재 구현 범위

- 플레이어 이동, 점프, 활공, 대시
- 근접 기본 공격과 전투 대상 체력/피격 처리
- 튜토리얼 퀘스트 8단계 진행
- 송신탑 활성화와 헬테 보스전
- 모듈 트리, 보유 모듈, 장착 칸 UI
- 튜토리얼 펄스 모듈: 전방 충격파, 35 피해, 3초 재사용 대기
- 튜토리얼 완료 후 `Chapter01` 씬 연결

## 조작

| 입력 | 동작 |
| --- | --- |
| `A` / `D` | 좌우 이동 |
| `Space` | 점프 / 공중에서 길게 눌러 활공 |
| `Enter` | 기본 공격 |
| `Left Shift` | 대시 |
| `2` | 장착 모듈 사용 |
| `I` | 모듈 트리 및 장착 UI 열기/닫기 |
| `F` | 송신탑 활성화 |

## 시작하기

1. Unity Hub에서 이 저장소 폴더를 추가합니다.
2. 프로젝트에 지정된 Unity 버전으로 에셋 임포트가 끝날 때까지 기다립니다.
3. `Assets/Scenes/SampleScene.unity`를 엽니다.
4. Play 버튼으로 튜토리얼을 시작합니다.

`Chapter01`은 Build Settings에 함께 등록되어 있습니다.

## 프로젝트 구조

```text
Assets/
  Scenes/                         # SampleScene, Chapter01
  _Project/
    Art/Fonts/                    # 한글 UI 폰트
    GameData/Tutorial/            # ScriptableObject 기반 튜토리얼 데이터
    Scripts/
      Runtime/                    # 게임 런타임 시스템
      Editor/                     # 씬 검증 및 제작 도구
      Tests/                      # EditMode 테스트
  Docs/narthex_requirements_md_files/
                                  # 기획 및 아키텍처 문서
Packages/
ProjectSettings/
```

## 주요 설계 원칙

- 게임플레이와 UI 오브젝트는 씬 계층에 미리 배치합니다.
- 런타임에는 새 GameObject 또는 Component를 생성하지 않고, 기존 오브젝트의 상태와 참조만 갱신합니다.
- 퀘스트, 모듈, 능력, 보상은 ScriptableObject 데이터와 Manager 요청을 통해 연결합니다.
- 모듈 트리 접근 권한은 영구 데이터이며, 모듈 포인트·해금·장착 상태는 회차 데이터입니다.

## 검증

- Unity EditMode 테스트: `Narthex.Tests`
- 튜토리얼 씬 검증 메뉴: `Narthex > Validation > Validate Active Tutorial Scene`

## 현재 다음 단계

- 모듈 강화 1~3단계
- 일반 스킬 슬롯 2개와 확장 스킬 슬롯 4개
- 추가 모듈 및 보스 모듈 트리
- 공격 모션과 2D 스프라이트 애니메이션
- 튜토리얼 대화 시스템
