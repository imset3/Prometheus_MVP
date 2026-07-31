# PZ 범용 플랫폼 타일

디자이너 캐릭터와 A~H 배경의 선·색·명암에 맞춘 2D 플랫폼 타일셋이다.

- 개별 타일: `Generated/PZ_*.png`
- 통합 시트: `Generated/PZ_PlatformTiles_Universal_4x3_v1.png`
- 셀 크기: `256×256 px`
- Unity PPU: `256`
- Unity Grid Cell Size: `1×1`
- 투명 배경, Bilinear, Mipmap Off, Compression None

Unity에서 `sragon000/Art/Build AI Platform Tile Palette`를 실행하면 개별 PNG의
임포트 설정을 맞추고 Tile 에셋과
`Assets/TileMap/PZ_PlatformTilesPalette.prefab`을 생성한다.

긴 발판은 `Platform_Left → Platform_Middle 반복 → Platform_Right` 순서로
배치한다. 한 칸짜리 발판에는 `Platform_Isolated`를 사용한다.
