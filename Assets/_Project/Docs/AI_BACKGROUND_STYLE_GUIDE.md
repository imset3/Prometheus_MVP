# Prome & Zeus AI 배경 제작 규격

## 목적

AI 배경은 디자이너가 그린 캐릭터와 경쟁하지 않는 후면 배경판으로 사용한다.
플레이 가능한 바닥, 플랫폼, 충돌 지형은 기존 Unity 레벨 오브젝트가 담당한다.

## 공통 베이스 프롬프트

```text
Prome & Zeus game background backplate for [LOCATION].
Match the supplied human-drawn character artwork: simplified hand-drawn anime
game illustration, thin slightly imperfect dark outlines, broad flat pastel
color shapes, restrained soft cel shading, gentle paper-like texture, muted
warm steampunk palette, large readable forms, low contrast and low visual
density in the central gameplay area. Background layer only, straight-on
camera, 16:9. Keep all important decoration near the outer edges and upper
third so the playable character remains the focal point.

No characters, no enemies, no silhouettes of people, no text, no logo, no UI,
no foreground props, no floor, no walkway, no playable platform, no collision
geometry, no photorealism, no 3D render, no sharp high-frequency detail, no
heavy bloom, no dramatic depth of field.
```

`[LOCATION]`만 다음과 같이 바꾼다.

| 키 | 구역 | 이미지 |
|---|---|---|
| A | 아다마스 회의장 | `TUTO_A_AdamasMeeting_Backplate_v2.png` |
| B | 숨겨진 방 | `TUTO_B_HiddenRoom_Backplate_v2.png` |
| C | 공중 기관차 복도 | `TUTO_C_Corridor_Backplate_v2.png` |
| D | 훈련장 | `TUTO_D_Training_Backplate_v2.png` |
| E | 본부 외부 | `TUTO_EH_BrightSky_Continuous_v4.png` + `TUTO_Zenith_Continuous_Cutout_v6.png` |
| F | 전투 스테이지 1 | E와 같은 연속 하늘·제니스 레이어 |
| G | 전투 스테이지 2 / 선착장 진입로 | E와 같은 연속 하늘·제니스 레이어 |
| H | 나디르 선착장 | E와 같은 연속 하늘·제니스 레이어 |

이미지는 `Assets/_Project/Art/AIConcepts/TutorialBackgrounds/`에 둔다.

## Unity 적용 원칙

- `background.backplate.apply` 명령을 사용한다.
- 첫 적용 전 스냅샷을 만들고 `dryRun: true`로 변경 목록을 확인한다.
- `sortingOrder`는 `-1000`, `cameraSpaceDepth`는 `20`을 기본값으로 한다.
- 캐릭터와 기존 지형이 묻히면 이미지를 다시 생성하기 전에 `opacity`를
  `0.75`~`0.9` 범위에서 먼저 조절한다.
- 적용 후 `scene.doctor.scan`과 두 번째 스냅샷 비교를 실행한다.
- 배경판은 카메라를 따라가며 `TutorialLocationChanged` 이벤트에 맞춰 A~H
  슬롯을 전환한다. 기존 수작업 지형과 충돌체는 수정하지 않는다.
- E~H에서는 구역 전환으로 제니스 크기를 바꾸지 않는다. 밝은 하늘을
  유지하고 `ZenithApproachPresenter`가 플레이어 월드 X를 기준으로 같은
  v6 제니스 스프라이트의 크기·위치·불투명도를 연속 보간한다.

예시 요청:

```json
{
  "version": "1",
  "requestId": "tutorial-background-a",
  "command": "background.backplate.apply",
  "scenePath": "Assets/Scenes/TutorialScene.unity",
  "dryRun": true,
  "arguments": [
    { "key": "locationKey", "value": "A" },
    {
      "key": "spritePath",
      "value": "Assets/_Project/Art/AIConcepts/TutorialBackgrounds/TUTO_A_AdamasMeeting_Backplate_v2.png"
    },
    { "key": "opacity", "value": "0.85" },
    { "key": "sortingOrder", "value": "-1000" },
    { "key": "cameraSpaceDepth", "value": "20" }
  ]
}
```
