# Helte Animation Batch v2

헬테 FSM 전용 PNG 시퀀스다. 모든 런타임 프레임은 `512 × 512 RGBA`, 파일 번호는
`000`부터 연속이며 원본은 왼쪽을 바라본다. 오른쪽 대상은
`CharacterPngAnimationBridge`의 `SpriteRenderer.flipX`로 처리한다.

## 동작 구성

| FSM 클립 | 프레임 | FPS | 용도 |
| --- | ---: | ---: | --- |
| Idle | 8 | 10 | 기본 대기, 반복 |
| BasicWindup | 3 | 8 | 기본 베기 예고 |
| BasicLeftSlash | 8 | 16 | 왼손 1타 |
| BasicAdvance | 3 | 12 | 1타와 2타 사이 한 걸음 |
| BasicRightSlash | 8 | 16 | 오른손 2타 |
| BlinkVanish | 4 | 18 | 블링크 소멸 |
| BlinkReappear | 4 | 18 | 블링크 재등장 |
| DashTelegraph | 2 | 8 | 대시 직전 압축 자세 |
| DashApproach | 8 | 18 | 피해 없는 고속 접근 |
| CrossSlashTelegraph | 3 | 10 | X자 베기 예고 |
| CrossSlash | 8 | 16 | 이도류 X자 베기 |
| SwordFocus | 4 | 6 | 칼 3개 소환 집중 |
| SwordVolley | 4 | 12 | 왼쪽→오른쪽→가운데 발사 |
| CounterTelegraph | 4 | 8 | 장난스러운 반격 예고 |
| CounterStance | 4 | 10 | 교차 방어·튕겨내기 |
| PhaseTransition | 8 | 6 | 2페이즈 전환 |
| Recover | 3 | 8 | 패턴 종료 복귀 |
| Hit | 8 | 16 | 짧은 비치명 피격 반응 |
| Death | 8 | 6 | 사망이 아닌 무릎 꿇기·우호적 승복 |

## Unity 적용

AI Scene Toolkit 명령 `boss.helte-animation-v2.apply`를 사용한다. 명령은
`UnityGenerated/Clips`와 `HelteBoss_v2.controller`를 만들고 현재 씬의
`AI_HelteAnimatedSprite`에만 연결한다. 지형, 보스 위치, 히트박스, FSM 수치는 변경하지 않는다.

적용 순서는 스냅샷 → dry run → apply → Scene Doctor → 스냅샷 비교다.

## 원본 생성 규격

- 캐릭터 기준: `ReviewBatch_v1/Generated/HELTE_Body_Base.png`
- 포즈 시트: 4열 × 2행, 완전 불투명 마젠타 크로마 배경
- 스타일: 손그림풍 치비 애니메이션, 짙은 유기적 선, 평면 셀 셰이딩
- 팔레트: 숲색 코트, 차콜 망토, 아이보리·앤티크 골드 효과
- 마무리: 헬테의 비적대적 서사에 맞춰 죽음이 아닌 항복/인정으로 연출
