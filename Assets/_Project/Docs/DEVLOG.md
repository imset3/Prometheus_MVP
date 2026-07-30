# Prometheus 개발일지

프로젝트의 주요 기능 변경, 검증 결과와 협업 기준을 날짜순으로 기록한다. 세부 사용법은 각 항목에 연결된 문서를 따른다.

## 2026-07-31 — 헬테 우호적 패턴 프로토타입

- `HelteBossFsmDev.unity`에서만 페이크 블링크, 카운터 자세와 자비 행동을 활성화했다.
- 기존 기본 공격·블링크 X 베기·칼 소환은 교체하지 않고 신규 패턴 사이에 유지한다.
- 카운터 자세를 공격하면 피해 없이 밀려나며, 기다리면 1.2초의 근접 반격 기회가 열린다.
- 프로메 체력이 25% 이하가 되면 헬테가 한 번 후퇴하고 체력을 35%까지 복구한다.
- 자비 행동 전에 같은 연계의 후속타로 사망하지 않도록 최초 1회는 체력 25%를 보장한다.
- 최종 시험은 기존 패턴과 신규 패턴을 섞은 6박 순환으로 확장했다.
- 튜토리얼 씬은 검증 전까지 기존 패턴 설정을 유지한다.

## 2026-07-30 — 헬테 FSM 1차 폴리싱

### 개선

- 재도전 시 패턴 플래너, 2페이즈 연출 상태와 헬테 최초 위치를 초기화했다.
- 헬테 최대 체력을 `100 → 240 → 6000`으로 조정하고 2페이즈 진입점을 `55%`로 설정해 5분 전투의 1차 기준을 마련했다.
- 단순 체력 소모를 줄이기 위해 최대 체력을 `6000 → 5000`으로 낮추고, `55%` 2페이즈와 `20%` 최종 러시로 전투 리듬을 세분화했다.
- 2페이즈부터 회복·이동 시간을 단계적으로 줄이고 투사체 속도와 피해량을 올리되, 공격 예고 시간은 유지했다.
- 최종 러시는 `블링크 → 기본 → 칼 소환 → 기본` 순환으로 구성해 읽을 수 있는 클라이맥스를 만든다.
- `HelteCombatTelemetryHost`가 전투 시간과 기본 콤보·블링크·칼 소환 횟수를 측정한다.
- 헬테 개발 씬에만 계측 오버레이를 표시하고, 처치 시 목표 `300초`와의 차이를 Console에 기록한다.
- `HeltePatternVfxHost`를 추가해 FSM 상태별 VFX 오브젝트를 Inspector에서 연결할 수 있게 했다.
- 하나의 상태에 ParticleSystem, 트레일, 검광 등 여러 VFX 슬롯을 겹쳐 연결할 수 있다.
- 블링크 돌진을 `경로 예고 → 무피해 이동 → X 베기 예고 → 판정` 상태로 분리했다.
- 돌진 경로 예고 시간을 `0.3초`로 추가했다.
- 전투 도중 플레이어나 보스가 사망하면 남은 이동·공격 연출이 이어지지 않게 했다.
- 헬테 전용 개발 씬의 HUD가 구간 스킵 이후 이전 튜토리얼 목표에 남는 문제를 수정했다.

### 검증

- Unity 스크립트 컴파일 오류 `0`
- 개발 씬 진입 시 `튜토리얼 10/10 · TUTO_H_01`과 `나디르 선착장` 표시 확인
- EditMode 테스트 총 `47개` 발견 확인
- 패턴 플래너 재시작 테스트와 개발 씬 HUD PlayMode 검증 추가

세부 상태 흐름은 [헬테 FSM 개발 문서](HELTE_FSM_DEV.md)를 따른다.

## 2026-07-30 — AI Scene Toolkit과 Editor 도구 정리

### 목표

- Codex, Claude Code, Unity MCP와 팀원이 동일한 씬 작업 인터페이스를 사용하도록 한다.
- 레벨 배치 변경을 코드 수정 대신 마커 이동으로 처리할 수 있게 한다.
- 기존 일회성 Setup 도구가 팀원의 씬을 의도치 않게 변경하지 않도록 정리한다.

### 구현

- `sragon000 > Prometheus Scene Toolkit` 통합 창 추가
  - 씬 개요
  - 기능 마커 생성·이동·조회
  - Scene Doctor 검사와 안전 복구
  - 구역 흐름 검증
  - 씬 스냅샷 생성·비교
  - 기존 검증·테스트·분석·아트 도구 실행
- AI용 JSON 명령과 응답 흐름 추가
  - 모든 변경 명령은 `dryRun: true`로 사전 확인 가능
  - 마커, 오브젝트 Transform·활성 상태와 컴포넌트 값을 공통 명령으로 처리
  - Unity Batch Mode와 Unity MCP 메뉴 실행 지원
- 공통 `PrometheusToolRegistry` 추가
  - Validation, Tests, Analysis, Art, Legacy Migration 메뉴 분류
  - 통합 창과 Unity 메뉴가 같은 도구 경로를 사용

### 기존 도구 정리

- 일회성 씬 Setup 9개를 `Assets/_Project/Scripts/Editor/Legacy/`로 격리했다.
- 에디터 시작·씬 열기·도메인 리로드 시 실행되던 자동 적용 경로를 제거했다.
- Legacy Migration은 명시적으로 선택하고 경고를 확인해야만 실행된다.
- 훈련장과 환경 위험처럼 계속 사용하는 도구는 일상 도구로 유지했다.
- 기존 `sragon000/튜토리얼`, `sragon000/Tutorial`, `Prometheus/Level Design` 중복 메뉴를 현재 분류 체계로 통합했다.

### 문서

- [AI Scene Toolkit 사용 설명서](AI_SCENE_TOOLKIT.md)
- [마커 기반 레벨 저작 가이드](TUTORIAL_MARKER_AUTHORING.md)
- [프로젝트 인수인계](PROJECT_HANDOFF.md)
- [저장소 README](../../../README.md)

README에는 사람용 빠른 시작, 최신 검증 메뉴, 현재 훈련 순서와 위 문서 연결을 반영했다.

### 검증 결과

- Unity EditMode 테스트: `46/46` 통과
- Scene Toolkit 관련 테스트: `9/9` 통과
- 메뉴 구조와 통합 창 스크롤·버튼 배치를 Unity Editor에서 확인
- 정리 과정에서 `.unity`, `.prefab` 변경 없음
- Legacy 도구가 자동으로 씬을 변경하지 않는 것을 확인

### Git

작업 브랜치: `codex/ai-scene-toolkit`

- `459cfc5 feat(tools): add AI-first Unity scene toolkit`
- `34b138e refactor(tools): isolate legacy Unity migrations`
- `783d5bc docs: link scene toolkit usage guide`

원격 `origin/codex/ai-scene-toolkit`에 업로드했다.

### 다음 작업

- 팀 작업에 Toolkit을 실제 적용하며 누락된 명령과 검사 규칙 보강
- 팀원이 만든 레벨 변경은 마커·스냅샷 기반으로 검토
- 헬테 전용 개발 씬에서 보스 FSM 고도화 진행
