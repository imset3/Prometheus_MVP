# 튜토리얼 BGM 연결 계획

현재 저장소에는 `wav/mp3/ogg` 음원이 없어 코드와 씬에 음원을 임의로 포함하지 않는다. 대신 `TutorialBgmCueHost`가 다음 큐를 미리 준비한다.

| 큐 | 적용 구간 | Resources 경로 | 성격 |
| --- | --- | --- | --- |
| `Intro` | 회의장·복도 | `TutorialBgm/Intro` | 짧은 브리핑과 이동 |
| `HiddenRoom` | 숨겨진 방 | `TutorialBgm/HiddenRoom` | 어두운 공간, 빛과 활공 |
| `Training` | 훈련장 | `TutorialBgm/Training` | 반복 학습, 박자 명확 |
| `ExteriorCombat` | 외부·F·G | `TutorialBgm/ExteriorCombat` | 습격과 전투 |
| `Boss` | H·헬테 | `TutorialBgm/Boss` | 초반 적대, 후반 친화적 여운 |

음원을 받으면 `Assets/Resources/TutorialBgm/`에 위 파일명으로 넣거나 `TutorialBgmCueHost`의 AudioClip 슬롯에 드래그한다. 위치/퀘스트 이벤트는 이미 큐 전환을 호출하므로 레벨 코드 수정은 필요하지 않다.
