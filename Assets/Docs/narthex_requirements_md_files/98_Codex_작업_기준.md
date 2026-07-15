# Codex 작업 기준

## 1. 작업 원칙

이 문서는 이후 Codex가 직접 문서를 수정하거나 구현 기획을 확장할 때 따르는 작업 기준이다.

헬테와 CHAPTER_01 중간 보스 미노타우르스의 패턴은 확정되어 있다.

CHAPTER_01 최종 보스의 구체 패턴, 피해량, 판정 범위, 최종 보스 모듈 트리 테마는 입력 대기 상태이다.

## 2. 원장 문서

| 영역 | 원장 문서 | 수정 기준 |
| --- | --- | --- |
| 전체 진행 흐름 | [[01_게임_개요.md]] | 튜토리얼, STG-001~STG-006 순서 변경 시 수정 |
| 모듈 사용 책임 | [[06_모듈_스킬_시스템.md]] | 플레이어 입력, 데우스 발동, 전투 판정 책임 변경 시 수정 |
| 모듈 트리 / 영구 해금 | [[07_모듈_트리_시스템.md]] | 기본 트리, 보스 트리, 모듈 포인트 규칙 변경 시 수정 |
| 보스 구조 / 패턴 | [[10_ENEMY_BOSS_설계.md]] | 보스 패턴 상세 입력 시 우선 수정 |
| 회차 성장 | [[11_레벨업_시스템.md]] | 레벨, 경험치, 모듈 포인트 보상 변경 시 수정 |
| 저장 기준 | [[14_데이터_저장.md]] | 영구 데이터와 회차 데이터 경계 변경 시 수정 |
| 전투 이벤트 | [[16_전투_시스템.md]] | 공격, 명중, 사망, 보스 처치 이벤트 변경 시 수정 |
| 퀘스트 흐름 | [[17_퀘스트_시스템.md]] | 목표, 수락 방식, 보상 타입 변경 시 수정 |
| 회차 재시작 | [[18_회차_재시작_시스템.md]] | 사망 후 초기화/유지 기준 변경 시 수정 |
| Unity 구현 구조 | [[22_Unity_데이터_주도_구현_구조.md]] | Definition Asset, Runtime State, Executor 구조 변경 시 수정 |
| 기능/데이터/파일 구조 | [[23_기능_데이터_파일_구조.md]] | 폴더, asmdef, Addressables, 데이터 계층 변경 시 수정 |
| 시스템 갭 검토 | [[24_완성_시스템_갭_검토.md]] | 완성도 기준 허점 발견/해소 시 수정 |
| 개발 로드맵 | [[25_개발_로드맵.md]] | 구현 순서, 게이트, 산출물 변경 시 수정 |
| 즉시 개발 착수 구조 | [[26_즉시_개발_착수_구조.md]] | 첫 커밋 구조, Boot/SceneFlow/Save/Validator 기준 변경 시 수정 |
| 가상 구현 시뮬레이션 | [[27_가상_구현_시뮬레이션_검토.md]] | Unity 코드 작성 시 예상 문제와 방지 기준 변경 시 수정 |
| 최종 개발 로드맵 | [[28_최종_개발_로드맵.md]] | 실제 구현 순서와 Gate 변경 시 수정 |
| 2D 액션 최적화 | [[29_2D_액션_최적화_구조_검증.md]] | 렌더러, 스테이지 룸, 카메라 경계, 이동 감각, 히트박스, 보스 아레나 기준 변경 시 수정 |

## 3. 확정 데이터 경계

| 데이터 | 구분 | 초기화 시점 |
| --- | --- | --- |
| 기본 모듈 트리 접근 권한 | 영구 데이터 | 새 게임 시작 |
| 보스 모듈 트리 해금 기록 | 영구 데이터 | 새 게임 시작 |
| 레벨 / 경험치 | 회차 데이터 | 회차 재시작 |
| 모듈 포인트 | 회차 데이터 | 회차 재시작 |
| 모듈 해금 / 강화 / 장착 | 회차 데이터 | 회차 재시작 |
| 스테이지 진행 | 회차 데이터 | 회차 재시작 |
| 송신탑 상태 | 회차 데이터 | 회차 재시작 |
| 퀘스트 진행도 | 회차 데이터 | 회차 재시작 |
| 설정값 | 설정 데이터 | 설정 초기화 |

## 3.1 Unity 구현 데이터 경계

| 데이터 | 구현 형태 | 저장 형태 |
| --- | --- | --- |
| 모듈 / 효과 / 보스 패턴 / 퀘스트 원장 | ScriptableObject Definition Asset | 저장하지 않음 |
| 모듈 해금 / 장착 / 강화 | RuntimeState | moduleId, level, slotIndex |
| 보스 처치 / 모듈 트리 해금 | RuntimeState + PermanentData | bossId, treeId |
| 퀘스트 진행도 | RuntimeState + RunData | questId, conditionProgress, state |
| 효과 적용 상태 | RuntimeEffectInstance | effectId, remainingTime, stackCount |

## 3.2 Unity 6.3 URP 작업 기준

| 항목 | 기준 |
| --- | --- |
| 렌더 파이프라인 | URP 전용으로 고정하고 기본 씬은 URP 2D Renderer를 사용한다. Graphics / Quality 설정의 URP Asset 누락을 허용하지 않는다. |
| 커스텀 렌더 기능 | Unity 6.3 기준 URP Render Graph 방식으로 작성한다. |
| 머티리얼 | Built-in/HDRP 머티리얼을 게임 콘텐츠에 직접 사용하지 않는다. |
| 무거운 리소스 | Boss Prefab, VFX, Audio, Scene은 Addressables 기준으로 관리한다. |
| Definition 참조 | 가벼운 데이터 Definition은 직접 참조 가능하지만 Prefab/Audio/VFX 직접 참조는 제한한다. |
| 외부 에셋 | ThirdParty 원본을 직접 참조하지 않고 `_Project` 아래 정리본 또는 Variant를 사용한다. |

## 4. 이벤트 계약

| 이벤트명 | 발생 시스템 | 수신 시스템 | 필수 데이터 |
| --- | --- | --- | --- |
| PlayerDead | 전투 | 회차 재시작 / 저장 / 사운드 / UI | playerId, deathReason, currentStageId |
| EnemyKilled | 전투 | 레벨업 / 퀘스트 / 저장 / 사운드 | enemyId, enemyGrade, stageId, expAmount |
| BossKilled | 전투 | 퀘스트 / 모듈 트리 / 저장 / 사운드 / UI | bossId, stageId, unlockTreeId |
| ModuleUnlocked | 모듈 트리 | 저장 / UI / 사운드 | moduleId, treeId, spentModulePoint |
| ModuleEquipped | 모듈 트리 | 저장 / UI | moduleId, slotType, slotIndex |
| TowerActivated | 송신탑 | 저장 / 퀘스트 / 지도 / 사운드 | towerId, stageId |
| QuestCompleted | 퀘스트 | 보상 / 저장 / UI / 사운드 | questId, rewardList |
| PortalActivated | 포탈 | UI / 지도 / 사운드 | portalId, stageId, targetStageId |
| PortalUsed | 포탈 | 저장 / 퀘스트 / 카메라 / 사운드 | portalId, fromStageId, toStageId |
| RunRestarted | 회차 재시작 | 저장 / UI / 카메라 / 사운드 | runNumber, startStageId |

## 5. 최종 보스 패턴 입력 시 수정 순서

1. [[10_ENEMY_BOSS_설계.md]]에 최종 보스 패턴 표를 추가한다.
2. [[22_Unity_데이터_주도_구현_구조.md]] 기준에 맞춰 BossPatternDefinition, AbilityDefinition, EffectDefinition 연결 단위로 분해한다.
3. [[16_전투_시스템.md]]에 보스 공격 판정, 피해, 상태이상 연결을 추가한다.
4. [[07_모듈_트리_시스템.md]]에 보스 처치로 해금되는 모듈 트리 테마를 추가한다.
5. [[06_모듈_스킬_시스템.md]]에 보스 모듈 트리의 실제 모듈 목록을 추가한다.
6. [[17_퀘스트_시스템.md]]에 보스 퀘스트 완료 조건과 보상 ID를 구체화한다.
7. [[21_사운드.md]]에 보스 등장, 패턴, 처치 SFX/BGM 컨셉을 추가한다.
8. [[99_구조_검증_결과.md]]를 다시 갱신한다.

## 6. 금지된 재도입

| 항목 | 기준 |
| --- | --- |
| 이전 성장 시스템 | 다시 도입하지 않는다. |
| 이전 포인트 명칭 | 사용하지 않고 모듈 포인트로 통일한다. |
| 범위 밖 챕터 | 현재 범위 문서에 추가하지 않는다. |
| 송신탑 부활 | 송신탑은 부활 위치가 아니다. |
| 영구 모듈 강화 | 모듈 해금/강화/장착은 회차 데이터이다. |
| 콘텐츠 ID 하드 코딩 | Manager 내부에서 questId, bossId, moduleId 별 if/switch를 만들지 않는다. |
| 효과의 모듈 종속 | 효과를 특정 모듈 코드 안에 직접 박지 않는다. |
| ScriptableObject 직접 저장 | 저장 데이터에는 Definition 참조가 아니라 stableId와 런타임 값만 저장한다. |
| Resources 폴더 의존 | Addressables 또는 명시적 Registry 로딩으로 대체한다. |
| 런타임 GameObject/Component 생성 | `new GameObject`, `Instantiate`, `AddComponent`를 사용하지 않고 Hierarchy/Prefab에 사전 배치한다. |
| URP Compatibility Mode 의존 | Unity 6.3 기준 유지보수 위험이 있으므로 사용하지 않는다. |
| Built-in/HDRP Shader 유입 | URP 프로젝트 렌더링 오류를 만들 수 있으므로 검증에서 차단한다. |

## 7. 다음 작업 전 체크

```txt
금지 키워드 검색
→ 이벤트 계약 변경 여부 확인
→ 영구 데이터 / 회차 데이터 경계 확인
→ Definition / Runtime / Executor 경계 확인
→ URP / Addressables / 에셋 직접 참조 검증
→ 26번 즉시 착수 구조 기준과 충돌 여부 확인
→ 27번 가상 구현 시뮬레이션 위험 항목 반영 여부 확인
→ 28번 최종 개발 로드맵 Gate 변경 여부 확인
→ 29번 2D 액션 최적화 구조 기준과 충돌 여부 확인
→ 최종 보스 입력 대기 항목만 남았는지 확인
→ 총괄본 링크 확인
```
