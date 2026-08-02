# Prometheus Tutorial Background Generation Prompts

## Shared direction

Use case: stylized-concept  
Asset type: 16:9 side-scrolling 2D game environment concept background.

Use the supplied Prometheus character illustrations only as references for broad
hand-drawn anime line and color language, and the supplied title image only as a
world and atmosphere reference. Do not copy a character, costume, pose, logo,
text, object arrangement, or composition.

Create one original, empty environment in a hand-drawn 2D anime storybook
steampunk-fantasy style. Use soft painterly background rendering compatible with
clean charcoal-outlined chibi characters, simplified rounded forms, subtle
handcrafted irregularities, controlled cel-shaped value masses, and gentle
painterly gradients. Depict Victorian industrial Nadir with weathered brick,
dark iron, antique brass, pipes, gears, boilers, and restrained magical aether
technology.

Use a strict wide 16:9 orthographic side view with clear left-to-right traversal,
strong platform silhouettes, a quiet character-height gameplay lane, low visual
noise, and foreground, playable midground, and distant background layers that
can later be separated for parallax.

Palette: muted cream, parchment, walnut brown, dusty rose, charcoal, antique
brass, and weathered iron, with restrained cyan, ruby, or amber aether accents.

Constraints: one clean image, not a collage; no people, characters, enemies,
creatures, readable text, logos, UI, watermark, frame, photorealism, glossy 3D,
cyberpunk neon, modern machinery, excessive micro-detail, isometric view, or
top-down view.

## Location prompts

### A — Adamas meeting hall

A repurposed Nadir industrial-station interior containing a wide resistance
briefing room on the left, a short main hall, and a clear exit vestibule on the
right. Include modest wooden briefing furniture, abstract maps without readable
text, concealed workshop details, repaired pipes, and warm amber refuge lighting
with faint cool exterior light. Keep one continuous flat floor.

### B — Hidden chamber

A secret maintenance chamber beneath Adamas HQ, dominated by a tall vertical
updraft shaft for umbrella gliding. Include readable zigzag maintenance ledges,
an entry platform, a glowing airship-passkey pedestal on a safe platform,
broken ventilation machinery, old aether pipes, turquoise guide light, and
visible but non-horror depth.

### C — Reusable corridor

A straight 30–40-unit traversal-only industrial corridor with rhythmic iron
frames, brick service bays, horizontal pipes, brass lamps, sealed doors, clear
open passages at both ends, and one shallow central relief alcove. Show the
normal base-lighting state only; no combat, jumps, hazards, alarms, or rubble.

### D — Training hall

An enclosed resistance training hall with an elevated entry and descending
ramp, followed by a long flat floor. Suggest successive practice bays for dash
dodging, projectile jumping, double jumping, melee combos, and a piercing ranged
shot using visually distinct mechanical equipment without readable labels.

### E — HQ exterior overlook

A short, easy exterior route with the hidden HQ exit, a panoramic lookout, and
the entrance toward the combat district. Reveal smoke, restrained red warning
lights, the layered lower industrial city of Nadir, and the bright cloud sea.
Keep the playable route empty and hazard-free. Do not paint Zenith into this
backplate; it is rendered as a separate continuous camera-space sprite.

### F — Exterior combat I

A compact early-game industrial courtyard with a wide flat combat floor, one
modest raised platform, a clear left approach, and a thin locked mechanical gate
at the far-right exit. Use brick, iron supports, pipes, low machinery, moderate
alert lighting, and no hazards or enemies.

### G — Exterior combat II

An advanced industrial combat route split into two sections by a thin internal
gate. Include two readable height steps, two red steam-fire vents, two
blue-turquoise updraft vents, one molten-ore basin with safe ledges, and a final
industrial gate. Preserve unmistakable hazard color coding and a clean route.

### H — Nadir dock

A lower-city airship dock with a long horizontal floor: a tighter approach
corridor on the left leading into a broad, uncluttered boss arena on the right.
Include cargo cranes, hooks and chains away from the gameplay lane, ore
containers, docking machinery, mooring structures, clouds, and distant floating
industry. Keep the sky in the same bright daytime grade as E through G; use
amber and cyan only as local material accents.

## Zenith approach continuity

E through H form one continuous exterior approach toward the same floating
Zenith city. The E/F/G/H backplates must not contain Zenith. Use one transparent
Zenith sprite with a fixed silhouette, architecture, viewing angle, and crystal
placement, then change its apparent distance every frame from player world X.

- Progress start: E HQ-exit spawn, world X `239`.
- Progress end: H boss-arena centre, world X `867.87`.
- Far state: roughly 14% of screen width at viewport anchor `(0.80, 0.70)`.
- Near state: roughly 56% of screen width at viewport anchor `(0.70, 0.58)`.
- Interpolation: clamped continuous SmoothStep; never reset on an E/F/G/H
  location event or scene marker transition.

Keep the sky and overall exposure consistently pale and bright across E through
H. Do not use a late-afternoon, dusk, navy, or violet grade to communicate
progress. Distance is communicated only by Zenith scale, camera-space position,
and opacity in the same sprite.

### Shared E–H bright sky layer

Create a wide 16:9 side-scroller backplate containing only pale cyan-blue sky,
warm ivory daylight, soft layered clouds, and a distant cloud sea. Use polished
hand-drawn 2D anime background rendering with soft painterly cel shading,
restrained linework, and simplified detail density compatible with chibi
character sprites. No city, Zenith, characters, enemies, platforms, props,
text, frame, watermark, dark navy cast, storm, night, or vignette.

### Continuous Zenith cutout

Create one romantic lost sky-civilization grown around a broad floating rock
island. Use weathered ivory stone terraces, round towers, arched bridges,
observatory domes, garden levels, vines, shrubs, small trees, exposed roots,
layered stone strata, and a few narrow waterfalls. Keep stone and greenery at
roughly 65–70% of the design, then integrate 30–35% restrained steampunk/Aether
engineering: two elegant aged-brass lift rings embedded in the underside,
several compact turbine pods, thin copper conduits that follow architectural
curves, selected copper roof ribs, one clockwork observatory mechanism, small
pressure vents, and cyan energy lines connected to the crystal lift anchors.
Do not use a locomotive hull, factory ship, giant boiler, dense pipe clutter,
dark iron mass, or a skyline of smokestacks. Render it as simplified hand-drawn 2D anime game art with
clean slightly imperfect dark-brown linework, 2–3 cel-shaded values per
material, large readable forms, and no micro-detail or crushed shadows. Isolate
the complete silhouette with generous padding and no cast shadow, surrounding
clouds, character, robot, airship, text, logo, UI, frame, or watermark.
