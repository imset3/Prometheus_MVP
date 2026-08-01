Operate on the active Prometheus Unity scene through the project command contract.

1. Read `Assets/_Project/Docs/AI_SCENE_TOOLKIT.md`.
2. Translate the request into a `PrometheusAiCommandRequest`.
3. Write it to `Temp/PrometheusSceneToolkit/request.json` with `dryRun: true`.
4. Ask Unity MCP to execute `sragon000/AI Toolkit/Run Pending Command`.
5. Read `Temp/PrometheusSceneToolkit/response.json`.
6. Show the planned changes and only then repeat with `dryRun: false`.
7. Run `scene.doctor.scan` and snapshot comparison after mutation.

User request: $ARGUMENTS
