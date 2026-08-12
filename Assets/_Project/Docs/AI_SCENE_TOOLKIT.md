# Prometheus AI Scene Toolkit

> 문서 위치: [README](../../../README.md) → AI Scene Toolkit 사용 설명서  
> 마커 배치 규칙: [튜토리얼 마커 저작 가이드](TUTORIAL_MARKER_AUTHORING.md)

## 목적

이 도구는 Codex, Claude Code, Unity MCP와 사람이 같은 씬 작업 API를 사용하게 한다.

- AI: JSON 명령과 JSON 응답을 사용한다.
- 사람: `sragon000 > Prometheus Scene Toolkit` 창에서 시각 배치와 검토를 한다.
- 검증·테스트·분석·아트 도구: 통합창의 `기존 도구` 탭에서 호출한다.
- 과거 Setup: `Legacy Migration`으로 격리되어 있으며 자동 실행되지 않는다.
- 씬은 명령 파일을 만들었다는 이유만으로 변경되지 않는다.
- 모든 변경 명령은 `dryRun: true`를 기본값으로 사용한다.

## 구성

| 기능 | AI | 사람 |
| --- | --- | --- |
| 통합 씬 작업 | `scene.report` | 개요 탭 |
| 마커 배치 | `marker.create`, `marker.move`, `marker.list` | 마커 탭 |
| Scene Doctor | `scene.doctor.scan`, `scene.doctor.repair-safe` | Scene Doctor 탭 |
| 구역 흐름 | `flow.validate` | 구역 흐름 탭 |
| 스냅샷 | `snapshot.capture`, `snapshot.compare` | 스냅샷 탭 |
| 컴포넌트 수정 | `component.inspect`, `component.set` | Inspector |
| 오브젝트 활성화 | `object.set-active` | Hierarchy |
| 오브젝트 위치 | `object.transform` | Scene View Transform 도구 |
| 튜토리얼 배경판 | `background.backplate.apply` | Inspector / Scene View |
| 제니스 연속 접근 | `background.zenith-approach.apply` | Inspector / Scene View |
| 튜토리얼 적응형 음악 | `audio.music.apply` | Inspector / AudioSource |
| 튜토리얼 효과음 | `audio.sfx.apply` | Inspector / AudioSource |
| 데모 비행정 엔딩 | `tutorial.demo-ending.apply` | Inspector / Scene View |
| 헬테 전용 PNG 모션 v2 | `boss.helte-animation-v2.apply` | Animator / SpriteRenderer |
| 헬테 모션 가독성 속도 | `boss.helte-animation-v2.pacing` | Animator / HelteBossPatternHost |
| 기존 원거리 더미 통합 | `tutorial.training-dummies.apply` | Hierarchy / Collider2D |
| 원거리 적 투사체 아트 | `tutorial.enemy-projectile-art.apply` | SpriteRenderer / Scene View |
| 코드 영향 확인 | `code.usage` | 씬 검색 |

## Unity MCP 사용

요청 파일:

`Temp/PrometheusSceneToolkit/request.json`

응답 파일:

`Temp/PrometheusSceneToolkit/response.json`

실행 메뉴:

`sragon000/AI Toolkit/Run Pending Command`

AI 작업 순서:

1. `snapshot.capture`
2. 변경 명령을 `dryRun: true`로 실행
3. 응답의 `changes` 확인
4. 같은 명령을 `dryRun: false`로 실행
5. `scene.doctor.scan`
6. 다시 `snapshot.capture`
7. `snapshot.compare`

## Unity Batch Mode 사용

```text
-executeMethod Narthex.Tools.PrometheusAiCommandRunner.RunBatch
-prometheusCommandFile <request.json>
-prometheusOutputFile <response.json>
```

Unity가 같은 프로젝트를 열고 있을 때는 Batch Mode를 동시에 실행하지 않는다. 그 경우 Unity MCP 메뉴 방식을 사용한다.

## 요청 형식

Unity `JsonUtility` 호환을 위해 인자는 Dictionary가 아니라 배열이다.

```json
{
  "version": "1",
  "requestId": "unique-request-id",
  "command": "marker.move",
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

## 주요 명령

### 씬과 검사

- `help`
- `scene.report`
- `scene.doctor.scan`
- `scene.doctor.repair-safe`

`repair-safe`는 현재 비어 있는 마커 ID만 생성한다. 보이지 않는 Collider나 씬 참조를 임의로 삭제하지 않는다.

### 마커

- `marker.list`
- `marker.create`
  - `markerId`, `kind`, `parentPath`, `x`, `y`, `z`, `width`, `height`
- `marker.move`
  - `markerId`, `x`, `y`, `z`, `rotationZ`
  - 선택적으로 `width`, `height`

### 타일맵 통행 여유 영역

- `tilemap.clearance.audit`
  - `x`, `y`, `width`, `height`
  - 지정 영역과 겹치는 고체 Collider의 경로·활성 상태·Bounds를 읽기 전용으로 보고한다.
- `tilemap.clearance.apply`
  - `markerId`, `zoneName`, `x`, `y`, `width`, `height`
  - 먼저 `dryRun: true`로 비활성화할 충돌체와 다시 만들 구역을 확인한다.
  - 적용 시 `TilemapClearance` 마커를 만들고, 영역 안의 레거시 충돌 프록시를 끄며 해당 구역 타일맵만 재구성한다.

`TilemapClearance`는 출입구, 사다리, 바람 통로, 적·장치가 차지하는 공간처럼
플랫폼 타일을 깔면 안 되는 영역을 나타낸다. 이후 플랫폼 재구성을 다시 실행해도
마커 Bounds와 교차하는 타일 셀은 자동으로 제외되므로 레벨 수정은 코드 좌표가 아니라
마커 이동과 크기 조절로 유지한다.

### 오브젝트와 컴포넌트

- `object.set-active`
  - 대상: `markerId`, `objectId`, `hierarchyPath` 중 하나
  - 값: `active`
- `object.transform`
  - 대상 + `x`, `y`, `z`, `rotationZ`
  - 선택적으로 `scaleX`, `scaleY`, `scaleZ`
- `component.inspect`
  - 대상 + `componentType`
- `component.set`
  - 대상 + `componentType`, `propertyPath`, `value`
  - 지원 값: 정수, 실수, bool, string, enum, Vector2, Vector3, `GlobalObjectId` 참조
  - Vector 값은 `1.5,2.0` 또는 `1.5,2.0,0`처럼 입력한다.

### 튜토리얼 배경판

- `background.backplate.apply`
  - `locationKey`: `A`~`H`
  - `spritePath`: `Assets/` 아래 PNG Sprite 경로
  - 선택값: `opacity`(기본 `1`), `sortingOrder`(기본 `-1000`),
    `cameraSpaceDepth`(기본 `20`)

이 명령은 수작업 레벨 도형을 수정하지 않는다. 씬 루트의
`AI_TutorialBackgroundRoot` 아래에 카메라 추적용 배경 SpriteRenderer를
생성하거나 갱신한다. 런타임에는 `TutorialLocationChanged` 이벤트에 따라
A~H 배경 슬롯을 전환한다.

- `background.zenith-approach.apply`
  - `spritePath`: 투명 배경의 단일 제니스 PNG Sprite 경로
  - 선택값: `playerPath`(기본 `TutorialRuntimeRoot/StageRoot/PlayerRoot`)
  - 월드 진행 범위: `startWorldX`(기본 `239`), `endWorldX`(기본 `867.87`)
  - 화면 위치: `farViewportX/Y`, `nearViewportX/Y`
  - 화면 너비 비율: `farScreenWidth`(기본 `0.14`), `nearScreenWidth`(기본 `0.56`)
  - 투명도: `farOpacity`(기본 `0.72`), `nearOpacity`(기본 `1`)
  - `sortingOrder`(기본 `-990`)

이 명령은 E 진입부터 H 보스 아레나까지 플레이어의 월드 X를 기준으로
제니스의 화면상 위치·크기·투명도를 매 프레임 연속 보간한다. E/F/G/H
위치 이벤트가 바뀌어도 진행도를 리셋하지 않으며, 하늘 배경판의 색감과
제니스 원근 연출을 서로 분리한다.

### 튜토리얼 적응형 음악

- `audio.music.apply`

`TutorialRuntimeRoot/StageRoot/TutorialAudioRoot` 아래에 음악용 AudioSource
6개를 생성하거나 갱신하고 `TutorialMusicDirector`의 런타임·보스·클립
참조를 연결한다. Adamas/외부 전투는 위치 이벤트로 크로스페이드하고,
G 스테이지와 Helte 보스 페이즈는 동기화된 레이어의 볼륨만 전환한다.
헬테 처치 후에는 `MUS_TUTO_HelteDefeat_Prototype_Loop.wav`를 생성·연결해
데모 비행정 엔딩 동안 전용 승리 음악을 재생한다.

일반 명령 파일과 충돌 없이 음악 배치만 실행하려면
`Temp/PrometheusSceneToolkit/tutorial-music-request.json`을 사용하고
`sragon000/AI Toolkit/Run Tutorial Music Command` 메뉴를 실행한다.

### 기존 원거리 훈련 더미 통합

- `tutorial.training-dummies.apply`

`훈련장-수정본/원거리공격훈련`에 레벨 디자이너가 배치한 `Enemy` 더미 3개에
`CombatActorHost`와 피격 Collider를 연결하고, 훈련 흐름의 표적 참조를 이
오브젝트들로 교체한다. 과거의 중복 `RangedTarget_01~03` 오브젝트는 제거한다.
더미 Transform과 자식 스프라이트 배치는 변경하지 않는다.

### 튜토리얼 효과음

- `audio.sfx.apply`

`TutorialAudioRoot` 아래 UI·플레이어·적·보스·월드 AudioSource를 구성하고
`TutorialSfxDirector`에 전투, Helte FSM, 진행 이벤트와 합성 SFX 클립을
연결한다. 재생 음량은 저장 데이터의 `MasterVolume × SfxVolume`을 적용한다.

일반 명령 파일과 충돌 없이 효과음 배치만 실행하려면
`Temp/PrometheusSceneToolkit/tutorial-sfx-request.json`을 사용하고
`sragon000/AI Toolkit/Run Tutorial SFX Command` 메뉴를 실행한다.

### 테우스 원거리 투사체 외형

- `tutorial.theus-projectile.apply`

기존 원거리 지원 로직과 풀링 구조는 유지하면서 투사체 풀 3개의 Sprite,
색상, 표시 크기를 현재 테우스 전용 아트로 갱신한다. 전체 월드 폴리싱을
재실행하지 않으므로 레벨 디자이너가 배치한 F/G 구역에는 영향을 주지 않는다.

### 원거리 적 투사체 외형

- `tutorial.enemy-projectile-art.apply`

F 구역 원거리 적 1명과 G 구역 원거리 적 2명의 투사체 풀 9개만 대상으로 한다.
`TUTO_VFX_RangedProjectile_v1`의 첫 번째 Sprite 서브 에셋을 연결하고,
어두운 구간에서도 사라지지 않도록 `Sprite-Unlit-Default` 재질, 불투명 흰색,
정렬 순서 180을 적용한다. 적·스폰 마커·투사체 Collider와 이동 설정은 변경하지 않는다.
PNG가 `Multiple Sprite`로 임포트되어 있어도 `LoadAllAssetsAtPath`로 서브 스프라이트를
읽으므로 단일 에셋 로드 실패로 Sprite가 다시 `null`이 되는 문제를 방지한다.

### 외곽 적 진격 연출

- `tutorial.exterior-march.apply`

`외부/외부_적진격연출`만 재구성하는 제한 명령이다. 적 10명을 화면 하단에
상반신만 보이는 높이로 배치하고 모두 왼쪽을 바라보며 진군하게 한다.
전투 Actor나 Collider는 생성하지 않으며 F/G 타일맵과 수작업 레벨 배치를 건드리지 않는다.

### 데모 비행정 엔딩

- `tutorial.demo-ending.apply`

헬테 처치 직후 결과창을 즉시 표시하지 않고 비행정 탑승과 제니스 항해 연출을 실행한다.
탑승 장면은 측면 비행정, 항해 장면은 엔진이 보이는 후면 3/4 비행정을 사용하며,
항해 중 `DEMO-END-FLIGHT-START`에서 `DEMO-END-FLIGHT-END` 마커로 이동하면서 축소된다.
두 마커를 이동하면 코드 수정 없이 화면상 비행 경로를 조정할 수 있다. 제니스는 별도 UI 복제본을
생성하지 않고 `AI_TutorialBackgroundRoot/Zenith_Continuous`를 그대로 재사용하며,
`DEMO-END-ZENITH-CENTER` 마커까지 접근한다. 탑승 뒤에는 배경 루트를 제외한 레벨·플레이어·적·지형
루트를 비활성화해 배경, 기존 제니스, 비행정만 남긴다. 항해가 끝난 뒤에만
항해 자막을 포함한 UI를 숨긴다. 프로메는 중력을 끄고 대각선으로 보간하지 않고
`DEMO-END-BOARDING-POINT`의 X 위치까지 선착장 바닥을 따라 이동한 뒤 탑승한다. 항해가 끝난 뒤에만
`DEMO VERSION · TO BE CONTINUED` 결과 화면이 표시되며 데모 빌드에서는 Chapter 1 전환을 막는다.

### 헬테 전용 PNG 모션 v2

- `boss.helte-animation-v2.apply`
- `boss.helte-animation-v2.pacing`
  - 전용 클립 FPS와 패턴 상태 유지 시간을 함께 낮춰 모션이 중간에 잘리지 않게 한다.
  - 위치, 충돌체, 피해량, 체력 및 패턴 선택 확률은 변경하지 않는다.

`AnimationBatch_v2/Sequences`의 `000.png` 연속 프레임을 상태별 클립으로 만들고
`HelteBoss_v2.controller`를 보스의 `AI_HelteAnimatedSprite`에 연결한다. 기본 좌/우 베기,
블링크 소멸·재등장, 대시와 예고, X자 베기와 예고, 칼 집중·발사, 카운터 예고·자세,
페이즈 전환, 피격, 회복, 전투 마무리를 서로 다른 클립으로 유지한다. 레벨 배치와 충돌체는
수정하지 않으며 보스 개발 씬에서 검증한 뒤 튜토리얼 메인 씬에 별도로 적용한다.

### 튜토리얼 바람·프로메 대화 화상

- `tutorial.wind-dialogue-art.apply`

모든 `TutorialWindHazardHost` 마커의 도형 막대 연출을 은은한 상승 기류
스프라이트 애니메이션으로 교체한다. 숨겨진 방에 남아 있던 구형
`WindStrip_*` 표현만 비활성화하며, 바람 충돌체·기능 마커와 바람 기계는
유지한다. 또한 프로메 대화 화상을 왼쪽 화자 슬롯에 연결하고 얼굴 영역이
보이도록 마스킹한다. 전체 월드/타일맵 재구성은 실행하지 않는다.

### 튜토리얼 UI 가독성 폴리싱

- `tutorial.ui-polish.apply`
- `tutorial.double-jump-platform-align`
  - 훈련장 더블점프 발판의 실제 착지 Collider를 장식 링 상단이 아닌 스프라이트의 보이는 발판 면에 맞춘다.
  - 다른 훈련 단계나 레벨 배치는 변경하지 않는다.

생성형 아트 기반 HUD를 용도별로 분리한다. 목표·상호작용·자막·보스 HUD에는
얇고 장식이 적은 정보 스트립, 인벤토리·모듈·도입 카드에는 중형 정보 카드,
보스 체력에는 전용 바 트랙을 적용한다. 전체 화면 결과 오버레이는 글자를 방해하는
프레임 없이 조용한 암전 배경만 사용한다. 대화창은 전용 가로형 원본 비율
프레임과 어두운 패널, 텍스트
외곽선을 사용하며, `TUTO_*` 식별자는 저장·진행 로직에는 유지하되 실제
플레이 화면에서는 표시하지 않는다. 하나의 정사각형 프레임을 모든 크기의 HUD에
재사용하지 않으며, 각 프레임 내부 텍스트에는 용도별 안전 여백을 둔다.
상단 중앙의 진행 HUD는 `튜토리얼 단계`, `현재 목표 수량`, `목표 문장`,
`조작 안내`를 각각 독립된 행으로 표시한다. 목표 문장은 고정된 두 줄 안전 영역에서
줄바꿈하고 글자를 자동 축소하지 않으므로 긴 한국어 문장도 프레임 밖으로 넘지 않는다.
세계관 설명 자막은 표시 후 1초 동안 내용을 읽게 한 뒤
`SPACE · 눌러서 닫기` 안내를 노출한다. 이후 스페이스바로 즉시 닫을 수 있으며,
입력하지 않은 경우 기존 표시 시간이 끝나면 자동으로 다음 자막으로 진행한다.
좌측 하단에는 원거리 공격 아이콘,
1.5초 방사형 쿨다운과 남은 시간을 표시한다. 원거리 훈련 표적 3기는 시작
마커의 오른쪽 사거리 안에 재정렬하므로 실제 투사체가 순서대로 관통한다.
더블점프 훈련 발판 3개의 고체 충돌면도 표시 스프라이트 상단에 맞추고,
최고 발판의 완료 마커를 실제 착지면 위로 이동한다.
대화창은 텍스트 길이에 맞춰 변형하지 않고 16:9 기준 `1760×660` 고정 창을
띄운 뒤, 내부 본문 영역에서 자동 줄바꿈한다. 좌우 화자 프로필은 각각
`280×280` 표시 영역을 확보한다. 정사각형 프레임을 9-slice로 억지로 늘리지
않으므로 모서리와 테두리 장식의 비율도 유지된다.
대화창 하단 중앙에는 프레임 장식보다 위에 `SPACE · 대화 진행`을 고정 표시하고,
마지막 대사에서는 `SPACE · 대화 닫기`로 바꿔 입력 결과를 명확히 안내한다.
본문은 OFL 라이선스의 `Gowun Dodum`, 화자명·지역명·버튼 표제는
`Do Hyeon`을 사용해 한글 가독성과 산업 판타지 분위기를 함께 유지한다.

대상 선택 우선순위는 `objectId → markerId → hierarchyPath`다. 마커 ID가 가장 팀 친화적이고, GlobalObjectId가 AI에게 가장 정확하다.

### 코드 영향

- `code.usage`
  - `typeName`: 타입의 짧은 이름 또는 전체 이름
  - 현재 씬에 붙은 해당 컴포넌트와 스크립트 경로를 반환한다.

### 스냅샷

- `snapshot.capture`
  - 선택 인자: `outputPath`
- `snapshot.compare`
  - `beforePath`, `afterPath`

스냅샷은 오브젝트 활성 상태, Transform, 컴포넌트 타입, 기능 마커, Collider를 기록한다.

### 구역 흐름

- `flow.validate`
  - `assetPath`

구역 흐름 에셋은 노드 ID, 조건, 마커 ID, 다음 노드를 보관한다. 흐름 데이터는 현재 런타임 로직을 자동 교체하지 않으며, 설계·검증의 기준 데이터다.

## 사람용 빠른 시작

`sragon000 > Prometheus Scene Toolkit`

1. 작업할 씬을 열고 `스냅샷` 탭에서 기준 스냅샷을 생성한다.
2. 위치와 범위는 `마커` 탭에서 편집한다.
3. 보이지 않는 Collider는 `Scene Doctor`에서 선택한 뒤 Scene View로 확인한다.
4. `구역 흐름` 탭에서 다음 구역과 조건 연결을 검사한다.
5. 검증과 플레이 테스트는 `기존 도구` 탭에서 실행한다.
6. 수정 후 스냅샷을 다시 생성하고 이전 결과와 비교한다.

`Legacy Migration`은 과거 씬 복구용이다. 실행 전 확인창이 표시되며, 수작업으로 배치한 씬에는 기존 추천 위치 적용 메뉴를 다시 실행하지 않는다.

## 타이틀 씬 생성·갱신

AI 명령 `title.scene.apply` 또는 `sragon000 > AI Toolkit > Create or Update Title Scene`을 사용한다.

- 대상: `Assets/Scenes/TitleScene.unity`
- 구성: 배경, 프로메 PNG 시퀀스, 중앙 대형 제니스, 구름 레이어, `PROME&THEUS` 로고, 메뉴, 설정, 애니메이션 로딩 화면, 타이틀 BGM
- 실행 전 빈 타이틀 씬 기준 스냅샷과 드라이런을 수행하고, 적용 후 Scene Doctor와 사후 스냅샷 비교를 수행한다.
- `TutorialScene`의 수작업 레벨 계층은 열거나 변경하지 않는다.
- 타이틀 레이어는 런타임 애니메이션이므로 최종 아트 교체 시 배경·프로메·제니스 슬롯을 독립적으로 바꾼다.
- 공통 UI 스프라이트는 `Assets/_Project/Resources/UI/Title`의 `TITLE_UI_LogoFrame_v1`, `TITLE_UI_ButtonPlate_v1`, `TITLE_UI_LoadingCompass_v1`, `TITLE_UI_ModalPanel_v1`을 사용한다. `ModalPanel`은 타이틀 설정뿐 아니라 튜토리얼 일시정지·설정 창에도 적용된다.

## 문서 연결

- 프로젝트 전체 실행·조작·문서 목록: [README](../../../README.md)
- 마커별 배치와 훈련장 규칙: [튜토리얼 마커 저작 가이드](TUTORIAL_MARKER_AUTHORING.md)
- 현재 씬과 시스템 인수인계: [PROJECT_HANDOFF](PROJECT_HANDOFF.md)

## 안전 정책

- AI 명령은 씬을 자동 저장하지 않는다. 검토 후 사람이 저장한다.
- `EditorOnly` 마커는 Player 빌드에서 제거되므로 런타임 기능의 유일한 필수 참조로 사용하지 않는다. 기능 오브젝트 내부에 런타임 앵커를 보존하거나 빌드 시 자동 복구한다.
- 삭제와 임의 Collider 비활성화는 자동 복구 범위에 포함하지 않는다.
- 자동 변경은 Unity Undo에 등록한다.
- 씬 경로가 명시되지 않으면 활성 씬을 사용한다.
- 중복 markerId는 오류이며 먼저 해결해야 한다.
- hierarchyPath는 이름 변경에 취약하므로 장기 연결에는 사용하지 않는다.
- 씬 열기·도메인 리로드·스크립트 컴파일은 Legacy Migration을 실행하지 않는다.
