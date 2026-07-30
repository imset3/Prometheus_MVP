# Prometheus AI scene workflow

When changing Unity scenes, markers, serialized component values, or scene activation state:

1. Read `Assets/_Project/Docs/AI_SCENE_TOOLKIT.md`.
2. Prefer `PrometheusAiCommandRunner` commands over editing `.unity` YAML.
3. Run mutating commands with `"dryRun": true` first.
4. Capture a snapshot before the first mutation.
5. After mutation, run `scene.doctor.scan`, capture another snapshot, and compare them.
6. Do not run legacy one-click setup commands on a scene that a level designer has manually arranged unless the user explicitly requests a reset or migration.

Visual placement remains a human-reviewed task. Use stable marker IDs for AI automation and hierarchy paths only as a fallback.
