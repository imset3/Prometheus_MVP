---
system_id: SYS-22
source: 구현 아키텍처 기준
sync: "총괄본은 Obsidian 임베드(![[파일명]]) 방식으로 연결됨"
---

## 연결 시스템
- [[06_모듈_스킬_시스템.md|06. 모듈 스킬 시스템]]
- [[07_모듈_트리_시스템.md|07. 모듈 트리 시스템]]
- [[08_효과_분류.md|08. 효과 분류]]
- [[10_ENEMY_BOSS_설계.md|10. ENEMY BOSS 설계]]
- [[14_데이터_저장.md|14. 데이터 저장]]
- [[16_전투_시스템.md|16. 전투 시스템]]
- [[17_퀘스트_시스템.md|17. 퀘스트 시스템]]
- [[23_기능_데이터_파일_구조.md|23. 기능 데이터 파일 구조]]
- [[24_완성_시스템_갭_검토.md|24. 완성 시스템 갭 검토]]
- [[25_개발_로드맵.md|25. 개발 로드맵]]
- [[26_즉시_개발_착수_구조.md|26. 즉시 개발 착수 구조]]
- [[27_가상_구현_시뮬레이션_검토.md|27. 가상 구현 시뮬레이션 검토]]
- [[28_최종_개발_로드맵.md|28. 최종 개발 로드맵]]
- [[29_2D_액션_최적화_구조_검증.md|29. 2D 액션 최적화 구조 검증]]

## 공통 용어 연결
- [[06_모듈_스킬_시스템.md|모듈(스킬)]]
- [[08_효과_분류.md|효과]]
- [[10_ENEMY_BOSS_설계.md|보스 패턴]]
- [[17_퀘스트_시스템.md|퀘스트]]
- [[14_데이터_저장.md|데이터 저장]]

---

# 22. Unity 데이터 주도 구현 구조

## 1. 구현 목표

이 문서는 유니티에서 퀘스트, 보스 패턴, 모듈, 효과, 보상을 하드 코딩하지 않고 추가/수정할 수 있는 구현 구조를 정의한다.

핵심 목표는 코드가 콘텐츠 ID를 직접 분기하지 않고, 사람이 Unity Editor에서 Definition Asset을 만들고 서로 연결해 새 기능을 구성할 수 있게 하는 것이다.

## 2. 핵심 원칙

| ID | 원칙 | 설명 | 우선순위 |
| --- | --- | --- | --- |
| UNITY-ARCH-01 | Definition과 Runtime을 분리한다. | ScriptableObject는 고정 데이터, RuntimeInstance는 현재 회차 상태와 실행 상태만 가진다. | 상 |
| UNITY-ARCH-02 | 저장 데이터는 ID와 진행 상태만 저장한다. | ScriptableObject 자체를 저장하지 않고 stableId, level, progress, unlocked 여부만 저장한다. | 상 |
| UNITY-ARCH-03 | 시스템 Manager는 콘텐츠 ID로 분기하지 않는다. | `if bossId == ...`, `switch questId` 같은 구현을 금지한다. | 상 |
| UNITY-ARCH-04 | 실행은 Executor가 담당한다. | Module, Quest, BossPattern, Effect는 데이터를 읽고 각 전용 Executor가 처리한다. | 상 |
| UNITY-ARCH-05 | 새 콘텐츠 추가는 에셋 생성으로 끝나야 한다. | 새 퀘스트, 새 모듈, 새 보스 패턴은 코드 수정 없이 Definition Asset 추가/연결로 가능해야 한다. | 상 |
| UNITY-ARCH-06 | 새 기능 타입만 코드 확장 대상이다. | 기존 노드로 표현할 수 없는 완전히 새로운 동작만 새 Executor/Node 클래스를 추가한다. | 상 |

## 3. 권장 Unity 에셋 구조

```txt
Assets/GameData
├─ ContentRegistry
│  └─ GameContentRegistry.asset
├─ Modules
│  ├─ Trees
│  └─ ModuleDefinitions
├─ Abilities
│  ├─ ActionDefinitions
│  ├─ HitboxDefinitions
│  └─ ProjectileDefinitions
├─ Effects
│  └─ EffectDefinitions
├─ Bosses
│  ├─ BossDefinitions
│  └─ PatternDefinitions
├─ Quests
│  ├─ QuestDefinitions
│  ├─ ConditionDefinitions
│  └─ RewardDefinitions
├─ Stages
└─ Audio
```

## 3.1 Unity 6.3 URP 프로젝트 기준

Unity 6.3 URP에서는 렌더링, 머티리얼, 커스텀 렌더 기능을 프로젝트 초기에 고정한다. 콘텐츠 시스템은 렌더 파이프라인에 직접 의존하지 않지만, VFX, Material, Shader, Renderer Feature 참조 방식은 URP 기준을 따른다.

| 항목 | 기준 | 위험 |
| --- | --- | --- |
| Render Pipeline | URP 전용 프로젝트로 생성하고 Graphics / Quality에 URP Asset을 명시한다. | 품질 단계별 URP Asset 누락 시 빌드 또는 특정 품질에서 렌더링이 달라진다. |
| Renderer Feature | Unity 6.3에서는 URP Render Graph 기준으로 작성한다. | Compatibility Mode 기반 커스텀 패스는 후속 버전에서 깨질 수 있다. |
| Material / Shader | URP Lit, Shader Graph URP Target, URP 호환 커스텀 셰이더만 사용한다. | Built-in/HDRP 머티리얼 유입 시 핑크 머티리얼 또는 누락 렌더링이 발생한다. |
| VFX / Post Process | URP Renderer Feature 또는 Volume Profile을 통해 연결한다. | 보스 패턴 코드에서 카메라 효과를 직접 제어하면 렌더 구조와 결합된다. |
| Quality | 품질별 URP Asset과 Renderer Asset을 별도 관리한다. | 모바일/PC 품질 전환 시 그림자, 라이트, 후처리 결과가 달라질 수 있다. |

URP 관련 에셋은 콘텐츠 Definition과 분리해 `Assets/Rendering` 또는 `Assets/Art/Materials_URP` 아래에서 관리한다.

## 3.2 권장 프로젝트 폴더 구조

```txt
Assets
├─ _Project
│  ├─ Scripts
│  │  ├─ Runtime
│  │  ├─ Editor
│  │  └─ Tests
│  ├─ GameData
│  │  ├─ Registry
│  │  ├─ Modules
│  │  ├─ Effects
│  │  ├─ Abilities
│  │  ├─ Bosses
│  │  ├─ Quests
│  │  └─ Stages
│  ├─ Prefabs
│  ├─ Scenes
│  ├─ UI
│  ├─ Audio
│  └─ Rendering
│     ├─ URPAssets
│     ├─ RendererAssets
│     ├─ Materials
│     ├─ Shaders
│     └─ VolumeProfiles
├─ ThirdParty
└─ AddressableAssetsData
```

`ThirdParty`는 원본 보관 영역으로 두고, 실제 게임에서 사용하는 Prefab, Material, Texture는 `_Project` 아래로 복사 또는 Variant로 정리한 뒤 사용한다. 외부 에셋 원본을 직접 참조하지 않는다.

## 4. Definition Asset 목록

| Definition | 역할 | 참조 대상 | 저장 여부 |
| --- | --- | --- | --- |
| ContentRegistry | 모든 고정 데이터의 ID 검색 원장 | 전체 Definition | 저장하지 않음 |
| ModuleTreeDefinition | 기본/보스 모듈 트리 구성 | ModuleDefinition 목록 | ID만 저장 |
| ModuleDefinition | 모듈 해금, 장착, 강화, 발동 데이터 | AbilityDefinition, EffectDefinition | ID/강화/장착만 저장 |
| AbilityDefinition | 모듈 또는 보스 패턴이 실행하는 행동 묶음 | ActionDefinition 목록 | 저장하지 않음 |
| ActionDefinition | 판정 생성, 이동, 투사체, 소환, 사운드, VFX 같은 실행 단위 | Hitbox/Effect/Audio 등 | 저장하지 않음 |
| HitboxDefinition | 공격 판정 형태, 크기, 유지 시간, 명중 규칙 | EffectDefinition 목록 | 저장하지 않음 |
| ProjectileDefinition | 투사체 속도, 수명, 충돌 규칙 | HitboxDefinition, EffectDefinition | 저장하지 않음 |
| EffectDefinition | 피해, 넉백, 경직, 상태이상, 스탯 보정 | StackRule, ResistRule | ID/남은 시간만 저장 |
| BossDefinition | 보스 기본 정보와 페이즈 구성 | BossPatternDefinition 목록 | 처치 여부만 저장 |
| BossPatternDefinition | 보스 패턴의 조건, 실행 행동, 후속 규칙 | AbilityDefinition, ConditionDefinition | 런타임 카운터만 저장 |
| QuestDefinition | 퀘스트 목표, 조건, 보상, 다음 퀘스트 | QuestCondition, QuestReward | 상태/진행도만 저장 |
| QuestConditionDefinition | 위치 도달, 처치, 입력, 모듈 사용 같은 완료 조건 | 이벤트 타입, 대상 ID | 진행도만 저장 |
| QuestRewardDefinition | ModulePoint, TreeUnlock, StageUnlock 같은 보상 | 대상 ID와 수량 | 지급 여부만 저장 |
| StageDefinition | 스테이지 씬, 룸, 포탈, 스폰, 프리로드, 밸런스 연결 | RoomDefinition, PortalDefinition, BalanceProfileDefinition | currentStageId만 저장 |
| RoomDefinition | 전투 구역, 보스 아레나, 카메라 경계, 잠금/해제 조건 | CameraBounds, Encounter, BossDefinition | 룸 클리어 상태만 저장 |
| CameraRenderProfileDefinition | URP 2D Renderer, Pixel Perfect, Orthographic Size, Volume 연결 | URP Renderer Asset, Volume Profile | 저장하지 않음 |
| BalanceProfileDefinition | CHAPTER_01 HP, 피해량, 보상량, 모듈 비용 곡선 | StatTable, RewardTable | 저장하지 않음 |

## 4.1 직접 참조와 Addressables 기준

ScriptableObject Definition이 모든 에셋을 직접 참조하면 ContentRegistry 하나가 게임 전체 Prefab, Audio, VFX, Texture를 한 번에 끌고 올 수 있다. 따라서 가벼운 데이터와 무거운 리소스를 분리한다.

| 참조 대상 | 방식 | 이유 |
| --- | --- | --- |
| ModuleDefinition, EffectDefinition, QuestDefinition | 직접 참조 허용 | 가벼운 고정 데이터이며 검증이 쉽다. |
| Boss Prefab, Enemy Prefab, VFX Prefab | Addressables AssetReference | 스테이지 또는 보스 진입 시 비동기 로드한다. |
| BGM, SFX, Voice | Addressables AssetReference 또는 Audio Bank | 사운드 메모리를 전투 단위로 관리한다. |
| Material, Texture, Sprite | Prefab 또는 Addressable 참조 | 렌더 리소스 직접 로드를 통제한다. |
| Scene | Addressables SceneReference 또는 BuildSettings Scene ID | 스테이지 전환과 로딩 화면을 분리한다. |

`Resources` 폴더는 사용하지 않는다. 예외가 필요하면 빌드 전에 별도 승인 항목으로 문서화한다.

### 4.2 Hierarchy / Prefab 사전 배치 규칙

모든 GameObject, Component, 시스템 Host, 스테이지 기물은 Hierarchy 또는 Prefab에 사전 배치한다. Runtime 코드의 `new GameObject`, `Instantiate`, `AddComponent`는 사용하지 않는다. Runtime에서 생성 가능한 것은 순수 C# RuntimeState, DTO, 이벤트 payload뿐이다.

SceneDefinition에는 런타임에서 `SceneAsset`을 직접 저장하지 않는다. 런타임 필드는 `sceneAddress` 문자열 또는 Addressables `AssetReference`로 두고, Editor 검증에서만 SceneAsset을 사용한다.

## 5. 런타임 실행 구조

```txt
GameContentRegistry 로드
→ Definition ID 검증
→ PermanentData / RunData / SettingsData 로드
→ Stage 진입
→ QuestManager가 QuestDefinition 활성화
→ BossDirector 또는 EnemyAI가 PatternDefinition 선택
→ ModuleSystem 또는 BossPatternExecutor가 AbilityDefinition 실행 요청
→ AbilityExecutor가 ActionDefinition을 순서대로 실행
→ CombatSystem이 Hitbox / Projectile 명중 처리
→ EffectSystem이 EffectDefinition 적용
→ GameEventBus가 EnemyKilled / BossKilled / QuestCompleted 등 발행
→ Quest / ModuleTree / Save / UI / Sound가 이벤트 수신
```

## 6. 공통 실행 단위

모듈, 보스 패턴, 적 공격은 서로 다른 시스템처럼 보이지만 실행 단위는 동일하게 사용한다.

| 실행 단위 | 사용처 | 예시 |
| --- | --- | --- |
| AbilityDefinition | 모듈, 보스 패턴, ENEMY 공격 | 쌍검 잔상, 미노타우르스 돌진, 증기탄 |
| ActionDefinition | Ability 내부 순차 실행 | 대기, 이동, 판정 생성, 투사체 발사, 소환, 사운드 재생 |
| EffectDefinition | 명중 또는 조건 충족 시 적용 | 피해, 넉백, 경직, 둔화, 지속 피해 |
| ConditionDefinition | 퀘스트/패턴/보상 조건 | HP 50% 이하, 근접 공격 3회 후, BossKilled |
| RewardDefinition | 퀘스트/보스 처치 보상 | 모듈 포인트 지급, 보스 모듈 트리 해금 |

## 7. 모듈 구현 기준

```txt
ModuleDefinition
→ treeId, slotType, unlockCost, maxUpgradeLevel
→ abilityDefinition
→ passiveEffectDefinitions
→ upgradeModifierDefinitions
```

모듈은 자체 코드로 행동을 만들지 않는다. 직접 사용형 모듈은 AbilityDefinition을 실행하고, 패시브/강화 모듈은 EffectDefinition 또는 ModifierDefinition을 장착 상태에 따라 적용한다.

## 8. 효과 구현 기준

```txt
EffectDefinition
→ effectType
→ duration
→ tickInterval
→ stackRule
→ targetFilter
→ statModifiers
→ actionOnApply / actionOnTick / actionOnExpire
```

효과는 전투에만 종속되지 않는다. 모듈, 보스 패턴, 퀘스트 보상, 장비, 송신탑 버프가 같은 EffectDefinition을 참조할 수 있어야 한다.

## 9. 보스 패턴 구현 기준

```txt
BossDefinition
→ phaseDefinitions
→ patternDefinitions

BossPatternDefinition
→ patternId
→ phase
→ triggerConditions
→ priority
→ cooldown
→ abilityDefinition
→ nextPatternRule
→ runtimeCounters
```

보스 AI는 패턴 내용을 코드로 알지 않는다. BossDirector는 현재 Phase, 조건, 쿨타임, 우선순위만 평가해 PatternDefinition을 선택하고, 실행은 AbilityExecutor에 위임한다.

## 10. 퀘스트 구현 기준

```txt
QuestDefinition
→ questId
→ acceptPolicy
→ startConditions
→ completeConditions
→ conditionCombineRule
→ rewardDefinitions
→ nextQuestIds
```

퀘스트는 UI, 보스 패턴, 저장 시스템을 직접 호출하지 않는다. QuestManager는 이벤트를 받아 조건 진행도를 갱신하고, 완료 시 QuestCompleted와 Reward 요청 이벤트만 발생시킨다.

## 11. 이벤트 버스 계약

| 이벤트 | 필수 데이터 | 사용처 |
| --- | --- | --- |
| AbilityRequested | casterId, abilityId, sourceType | 모듈, 보스 패턴 |
| AbilityExecuted | casterId, abilityId, result | 사운드, 로그, 쿨타임 |
| HitConfirmed | attackerId, targetId, hitboxId, effectIds | 전투, 효과 |
| EffectApplied | targetId, effectId, sourceId, duration | UI, 사운드, 저장 |
| EnemyKilled | enemyId, stageId, expAmount | 퀘스트, 레벨, 저장 |
| BossKilled | bossId, stageId, unlockTreeId | 퀘스트, 모듈 트리, 저장 |
| QuestCompleted | questId, rewardIds | 보상, UI, 저장 |
| RewardGranted | rewardId, targetId, amount | UI, 저장, 사운드 |

## 12. Editor 검증 규칙

| 검증 항목 | 실패 처리 |
| --- | --- |
| Definition stableId 중복 | 빌드 전 오류 |
| 누락된 참조 | 빌드 전 오류 |
| 저장 대상 ID 변경 | 경고 후 마이그레이션 요구 |
| 퀘스트 다음 ID 순환 | 오류 |
| 보스 패턴에 AbilityDefinition 없음 | 오류 |
| AbilityDefinition에 실행 Action 없음 | 오류 |
| EffectDefinition의 stackRule 누락 | 경고 |
| 최종 보스 입력 대기 슬롯 | CHAPTER_01 최종 보스 개발 전까지 허용 |

## 12.1 Asset Validator 추가 규칙

| 검증 항목 | 실패 처리 |
| --- | --- |
| Definition이 Runtime 중 변경 가능한 필드를 포함 | 오류 |
| Definition에서 무거운 Prefab/Audio/VFX를 직접 참조 | 경고 또는 오류 |
| Addressable address 중복 | 오류 |
| Addressable group 미지정 | 오류 |
| URP 비호환 Shader 사용 | 오류 |
| Built-in/HDRP Material 참조 | 오류 |
| ThirdParty 원본 에셋 직접 참조 | 경고 |
| QuestDefinition 순환 참조 | 오류 |
| EffectDefinition 무한 중첩 가능 | 오류 |
| BossPatternDefinition 조건이 영원히 충족될 수 없음 | 경고 |
| 저장 데이터에 없는 stableId 삭제 | 마이그레이션 요구 |

## 12.2 런타임 안전 규칙

| 위험 | 방지 기준 |
| --- | --- |
| ScriptableObject 런타임 변형 | Definition은 읽기 전용으로 취급하고 RuntimeInstance에 복사해 변경한다. |
| 이벤트 추적 불가 | 문자열 이벤트 대신 타입이 있는 이벤트 구조체와 필수 payload를 사용한다. |
| 과도한 노드 추상화 | ActionDefinition 타입은 제한된 목록으로 시작하고 새 타입은 검증/테스트와 함께 추가한다. |
| 비동기 로딩 누락 | Addressables 로드는 Stage/Boss 진입 전 Preload 단계에서 완료한다. |
| 에셋 언로드 누락 | Stage 종료, Boss 처치, RunRestart 시 Addressables handle을 해제한다. |
| 저장 호환성 파손 | stableId 변경 시 MigrationTable을 먼저 작성한다. |

## 13. 금지 구현

| 금지 항목 | 이유 |
| --- | --- |
| Manager 내부에서 콘텐츠 ID별 if/switch 분기 | 새 콘텐츠 추가 때마다 코드가 깨진다. |
| 퀘스트가 UI를 직접 갱신 | UI 교체와 퀘스트 로직이 결합된다. |
| 보스 패턴을 Animator 이벤트만으로 완성 | 판정, 피해, 보상, 퀘스트 연결을 검증하기 어렵다. |
| 효과를 모듈 코드 안에 직접 작성 | 같은 효과를 보스, 장비, 보상에서 재사용할 수 없다. |
| 저장 데이터에 ScriptableObject 참조 직접 저장 | 에셋 경로 변경과 빌드 환경에 취약하다. |
| Resources 폴더 의존 | 빌드 포함 범위와 메모리 로딩을 통제하기 어렵다. |
| URP Compatibility Mode 의존 | Unity 6.3 이후 유지보수 위험이 높다. |
| 외부 에셋 원본 직접 참조 | 패키지 업데이트와 삭제 시 게임 데이터가 깨진다. |

## 14. 확장 순서

1. 새 EffectDefinition을 만든다.
2. 새 Hitbox/Projectile/ActionDefinition이 필요하면 만든다.
3. AbilityDefinition에 ActionDefinition을 순서대로 연결한다.
4. ModuleDefinition, BossPatternDefinition, EnemyAttackDefinition 중 필요한 곳에서 AbilityDefinition을 참조한다.
5. QuestDefinition 또는 RewardDefinition에서 완료 조건과 보상을 연결한다.
6. Editor Validator로 ID, 참조, 저장 경계를 검증한다.
7. PlayMode 테스트로 실행 이벤트가 끝까지 도는지 확인한다.
