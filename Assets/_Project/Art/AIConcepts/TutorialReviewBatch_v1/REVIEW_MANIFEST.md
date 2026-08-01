# Tutorial Art Review Batch v1

## Scope

- Backgrounds: existing A–H backplates remain unchanged.
- Platform tiles: six environment-specific visual families were generated.
- Enemy: one regular tutorial melee enemy only.
- Helte is excluded from this batch.
- Unity scenes, prefabs, Tile assets, Animator Controllers, and serialized references were not changed.

## Style-lock base prompt

Use this paragraph unchanged, then append only the asset-specific request.

> Hand-drawn 2D side-scroller game art matching the supplied Prometheus character references: soft chibi/anime proportions, clean readable silhouettes, slightly imperfect dark ink outlines, clean flat shapes with restrained soft painterly shading, warm muted steampunk materials, aged brass, walnut-brown metal, cream fabric, selective teal or violet accents, charming handmade feel. Preserve the designer's restrained contrast and avoid glossy 3D rendering, photorealism, generic mobile-game rendering, excessive micro-detail, hard vector edges, neon-heavy cyberpunk, text, labels, logos, UI, and watermarks.

Append one of:

- Background: `Create a layered side-view background for [LEVEL / LOCATION], with a clear gameplay lane, quiet character-reading zone, foreground/midground/background separation, and no characters.`
- Tile set: `Create an exact 4 columns × 3 rows sheet on solid #00FF00, containing [THEME] versions of isolated, left, middle, right; top-left, top, top-right, fill; wall-left, fill-alt, wall-right, support-pillar.`
- Enemy model: `Create one consistent full-body enemy design for [ENEMY ROLE], shown front, 3/4 right, and side-right on solid #00FF00.`
- Animation: `Using the approved model sheet as an exact identity reference, create [WALK / ATTACK / DIE] as exactly [FRAME COUNT] side-view frames facing right, fixed ground baseline and root anchor, on solid #00FF00.`

## Tile families

| Levels | Theme folder | Final review sheet |
|---|---|---|
| A / C | Warm brass + walnut interior | `../TutorialTileSets/ReviewBatch_v1/AC_WarmBrassWalnut/Generated/TUTO_AC_TileSheet_4x3_v1.png` |
| B | Cool teal hidden laboratory | `../TutorialTileSets/ReviewBatch_v1/B_CoolTealHiddenLab/Generated/TUTO_B_TileSheet_4x3_v1.png` |
| D | Tan training panels + blue-gray steel | `../TutorialTileSets/ReviewBatch_v1/D_Training/Generated/TUTO_D_TileSheet_4x3_v1.png` |
| E / H | Light air deck + restrained dock brass | `../TutorialTileSets/ReviewBatch_v1/EH_AirDeck/Generated/TUTO_EH_TileSheet_4x3_v1.png` |
| F | Rust cargo combat | `../TutorialTileSets/ReviewBatch_v1/F_RustCargo/Generated/TUTO_F_TileSheet_4x3_v1.png` |
| G | Charcoal + violet combat | `../TutorialTileSets/ReviewBatch_v1/G_CharcoalViolet/Generated/TUTO_G_TileSheet_4x3_v1.png` |

Each family contains:

- `Source/`: native chroma-key generation
- `Processed/`: transparent full source sheet
- `Generated/`: twelve individual 256 × 256 transparent PNG tiles and one 1024 × 768 sheet
- `Preview/`: five-tile horizontal seam test

Tile role order is identical for all families:

1. isolated
2. left cap
3. middle
4. right cap
5. top-left
6. top
7. top-right
8. fill
9. wall-left
10. fill-alt
11. wall-right
12. support-pillar

## Tutorial melee guard

- Model sheet: `../TutorialEnemies/ReviewBatch_v1/TutorialGuard/Processed/TutorialGuard_ModelSheet_v1_alpha.png`
- Walk v4: 8 transparent 512 × 512 source frames, played as 10 timing slots at 7 fps; each foot independently completes lift, reach, heel contact, and weight transfer, with heel-contact frames held twice
- Attack: 8 transparent 512 × 512 frames
- Die: 8 transparent 512 × 512 frames
- Animated review GIFs: `../TutorialEnemies/ReviewBatch_v1/TutorialGuard/Preview/`

## Validation

- 103 generated PNG files checked.
- All required files contain visible pixels and transparency.
- No retained bright green chroma spill was detected.
- Tile output cell size: 256 × 256.
- Enemy output frame size: 512 × 512.

## Approval gate

Do not integrate these assets into TutorialScene until the designer approves:

1. tile palette and material language per level,
2. platform seam/contact height,
3. enemy silhouette and perceived scale,
4. Walk loop cadence and foot contact,
5. Attack timing/readability,
6. Die pose and final footprint.
