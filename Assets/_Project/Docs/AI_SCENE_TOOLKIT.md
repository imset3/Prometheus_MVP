# Prometheus AI Scene Toolkit

## 목적

이 도구는 Codex, Claude Code, Unity MCP와 사람이 같은 씬 작업 API를 사용하게 한다.

- AI: JSON 명령과 JSON 응답을 사용한다.
- 사람: `sragon000 > Prometheus Scene Toolkit` 창에서 시각 배치와 검토를 한다.
- 기존 Setup 메뉴: 통합창의 `기존 도구` 탭에서 호환용으로 호출한다.
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

## 사람용 작업

`sragon000 > Prometheus Scene Toolkit`

- 위치와 범위는 `마커` 탭에서 편집한다.
- 보이지 않는 Collider는 `Scene Doctor`에서 선택한 뒤 Scene View로 확인한다.
- 기존 훈련장/F/G Setup과 플레이 테스트는 `기존 도구` 탭에서 실행한다.
- 수작업 배치 후 기존 추천 위치 적용 메뉴를 다시 실행하지 않는다.

## 안전 정책

- AI 명령은 씬을 자동 저장하지 않는다. 검토 후 사람이 저장한다.
- 삭제와 임의 Collider 비활성화는 자동 복구 범위에 포함하지 않는다.
- 자동 변경은 Unity Undo에 등록한다.
- 씬 경로가 명시되지 않으면 활성 씬을 사용한다.
- 중복 markerId는 오류이며 먼저 해결해야 한다.
- hierarchyPath는 이름 변경에 취약하므로 장기 연결에는 사용하지 않는다.
