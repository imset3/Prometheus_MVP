using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Narthex.Gameplay;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusAiCommandRunner
    {
        public const string PendingDirectory = "Temp/PrometheusSceneToolkit";
        public const string PendingRequestPath = PendingDirectory + "/request.json";
        public const string PendingResponsePath = PendingDirectory + "/response.json";

        private static readonly HashSet<string> SupportedCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "help",
            "scene.report",
            "scene.doctor.scan",
            "scene.doctor.repair-safe",
            "snapshot.capture",
            "snapshot.compare",
            "marker.list",
            "marker.create",
            "marker.move",
            "object.set-active",
            "object.transform",
            "background.backplate.apply",
            "background.zenith-approach.apply",
            "training.art.apply",
            "exterior.art.apply",
            "dock.art.apply",
            "hidden-room.art.apply",
            "meeting-room.art.apply",
            "corridor.art.apply",
            "audio.music.apply",
            "audio.sfx.apply",
            "tutorial.world-polish.apply",
            "tutorial.exterior-march.apply",
            "tutorial.theus-projectile.apply",
            "tutorial.enemy-projectile-art.apply",
            "tutorial.wind-dialogue-art.apply",
            "tutorial.ui-polish.apply",
            "tutorial.double-jump-platform-align",
            "tutorial.demo-ending.apply",
            "tutorial.training-dummies.apply",
            "tutorial.enemy-physics.apply",
            "tutorial.lava-art.apply",
            "tilemap.clearance.audit",
            "tilemap.clearance.apply",
            "title.scene.apply",
            "boss.polish.apply",
            "boss.helte-animation-v2.apply",
            "boss.helte-animation-v2.pacing",
            "art.prome-motion.apply",
            "component.inspect",
            "component.set",
            "code.usage",
            "flow.validate"
        };

        public static IReadOnlyCollection<string> Commands => SupportedCommands;

        [MenuItem(PrometheusToolMenuPaths.Ai + "Run Pending Command")]
        public static void RunPendingCommand()
        {
            var response = RunFile(PendingRequestPath, PendingResponsePath);
            Debug.Log($"[Prometheus AI Toolkit] {response.message}\n{PendingResponsePath}");
        }

        [MenuItem(PrometheusToolMenuPaths.Ai + "Write Command Help")]
        public static void WriteCommandHelp()
        {
            Directory.CreateDirectory(PendingDirectory);
            var request = new PrometheusAiCommandRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                command = "help"
            };
            File.WriteAllText(PendingRequestPath, JsonUtility.ToJson(request, true));
            RunPendingCommand();
        }

        // Unity batch mode:
        // -executeMethod Narthex.Tools.PrometheusAiCommandRunner.RunBatch
        // -prometheusCommandFile <path> -prometheusOutputFile <path>
        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs();
            var commandPath = ReadArg(args, "-prometheusCommandFile", PendingRequestPath);
            var outputPath = ReadArg(args, "-prometheusOutputFile", PendingResponsePath);
            var response = RunFile(commandPath, outputPath);
            if (!response.success) throw new InvalidOperationException(response.message);
        }

        public static PrometheusAiCommandResponse RunFile(string requestPath, string outputPath)
        {
            PrometheusAiCommandResponse response;
            try
            {
                if (!File.Exists(requestPath))
                    throw new FileNotFoundException("AI command request was not found.", requestPath);
                var request = JsonUtility.FromJson<PrometheusAiCommandRequest>(File.ReadAllText(requestPath));
                response = Execute(request);
            }
            catch (Exception exception)
            {
                response = new PrometheusAiCommandResponse
                {
                    success = false,
                    message = exception.ToString()
                };
            }
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, SerializeResponse(response));
            return response;
        }

        public static string SerializeResponse(PrometheusAiCommandResponse response) =>
            JsonConvert.SerializeObject(
                response,
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Ignore,
                    Converters = new List<JsonConverter>
                    {
                        new UnityVectorConverter()
                    }
                });

        public static PrometheusAiCommandResponse Execute(PrometheusAiCommandRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var response = new PrometheusAiCommandResponse
            {
                requestId = request.requestId,
                command = request.command,
                scenePath = request.scenePath
            };
            if (!SupportedCommands.Contains(request.command ?? string.Empty))
            {
                response.success = false;
                response.message = $"Unknown command '{request.command}'. Run 'help'.";
                return response;
            }
            if (string.Equals(request.command, "help", StringComparison.OrdinalIgnoreCase))
            {
                response.success = true;
                response.message = string.Join("\n", SupportedCommands.OrderBy(item => item));
                return response;
            }

            var scene = ResolveScene(request.scenePath);
            response.scenePath = scene.path;
            switch (request.command.ToLowerInvariant())
            {
                case "scene.report":
                    AddSceneReport(scene, response);
                    break;
                case "scene.doctor.scan":
                    response.issues = PrometheusSceneDoctor.Scan(scene);
                    response.message = $"Scene Doctor found {response.issues.Count} issue(s).";
                    break;
                case "scene.doctor.repair-safe":
                    response.changes = PrometheusSceneDoctor.RepairSafe(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = $"{(request.dryRun ? "Previewed" : "Applied")} {response.changes.Count} safe repair(s).";
                    break;
                case "snapshot.capture":
                    var snapshot = PrometheusSceneSnapshotService.Capture(scene);
                    response.artifactPath = PrometheusSceneSnapshotService.Save(snapshot, request.Get("outputPath"));
                    response.message = $"Captured {snapshot.objects.Count} scene object(s).";
                    break;
                case "snapshot.compare":
                    CompareSnapshots(request, response);
                    break;
                case "marker.list":
                    AddMarkers(scene, response);
                    break;
                case "marker.create":
                    CreateMarker(scene, request, response);
                    break;
                case "marker.move":
                    MoveMarker(scene, request, response);
                    break;
                case "object.set-active":
                    SetObjectActive(scene, request, response);
                    break;
                case "object.transform":
                    SetObjectTransform(scene, request, response);
                    break;
                case "background.backplate.apply":
                    ApplyBackgroundBackplate(scene, request, response);
                    break;
                case "background.zenith-approach.apply":
                    ApplyZenithApproach(scene, request, response);
                    break;
                case "training.art.apply":
                    response.changes = PrometheusTrainingArtAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = $"{(request.dryRun ? "Previewed" : "Applied")} D training hall art package ({response.changes.Count} change(s)).";
                    break;
                case "exterior.art.apply":
                    response.changes = PrometheusExteriorArtAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = $"{(request.dryRun ? "Previewed" : "Applied")} E exterior art package ({response.changes.Count} change(s)).";
                    break;
                case "dock.art.apply":
                    response.changes = PrometheusDockArtAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = $"{(request.dryRun ? "Previewed" : "Applied")} H Nadir dock art package ({response.changes.Count} change(s)).";
                    break;
                case "hidden-room.art.apply":
                    response.changes = PrometheusHiddenRoomArtAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = $"{(request.dryRun ? "Previewed" : "Applied")} B hidden-room functional art package ({response.changes.Count} change(s)).";
                    break;
                case "meeting-room.art.apply":
                    response.changes = PrometheusMeetingRoomArtAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " A meeting-room art package (" + response.changes.Count + " change(s)).";
                    break;
                case "corridor.art.apply":
                    response.changes = PrometheusCorridorArtAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " C corridor art package (" + response.changes.Count + " change(s)).";
                    break;
                case "audio.music.apply":
                    response.changes = PrometheusTutorialMusicAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " tutorial adaptive music package (" + response.changes.Count + " change(s)).";
                    break;
                case "audio.sfx.apply":
                    response.changes = PrometheusTutorialSfxAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " tutorial SFX package (" + response.changes.Count + " change(s)).";
                    break;
                case "tutorial.world-polish.apply":
                    response.changes = PrometheusTutorialWorldPolishAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " tutorial world polish (" + response.changes.Count + " change(s)).";
                    break;
                case "tutorial.exterior-march.apply":
                    response.changes = PrometheusTutorialWorldPolishAutomation.ApplyExteriorMarchOnly(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " exterior enemy march (" + response.changes.Count + " change(s)).";
                    break;
                case "tutorial.theus-projectile.apply":
                    response.changes = PrometheusTutorialWorldPolishAutomation.ApplyTheusProjectile(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " Theus projectile art (" + response.changes.Count + " change(s)).";
                    break;
                case "tutorial.enemy-projectile-art.apply":
                    response.changes = PrometheusRangedEnemyProjectileAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " ranged-enemy projectile art (" + response.changes.Count + " change(s)).";
                    break;
                case "tutorial.wind-dialogue-art.apply":
                    response.changes = PrometheusTutorialWorldPolishAutomation.ApplyWindAndDialogueArt(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " tutorial wind and dialogue art (" + response.changes.Count + " change(s)).";
                    break;
                case "tutorial.ui-polish.apply":
                    response.changes = PrometheusTutorialUiPolishAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " tutorial readable sprite UI (" + response.changes.Count + " change(s)).";
                    break;
                case "tutorial.double-jump-platform-align":
                    response.changes = PrometheusTutorialUiPolishAutomation.AlignDoubleJumpPlatforms(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " double-jump platform deck alignment (" + response.changes.Count + " change(s)).";
                    break;
                case "tutorial.demo-ending.apply":
                    response.changes = PrometheusTutorialDemoEndingAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " tutorial demo airship ending (" + response.changes.Count + " change(s)).";
                    break;
                case "tutorial.training-dummies.apply":
                    response.changes = PrometheusTrainingDummyIntegration.Apply(scene, request.dryRun)
                        .Select((description, index) => new PrometheusAiChange
                        {
                            action = "integrate-ranged-training-dummy",
                            hierarchyPath = "훈련장-수정본/원거리공격훈련",
                            before = index < 3 ? "visual-only authored dummy" : "duplicate runtime targets",
                            after = description
                        })
                        .ToList();
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " existing ranged training dummies (" + response.changes.Count + " change(s)).";
                    break;
                case "tutorial.enemy-physics.apply":
                    response.changes = PrometheusEnemyPhysicsAutomation.Apply(scene, request.dryRun).ToList();
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " grounded F/G enemy physics (" + response.changes.Count + " change(s)).";
                    break;
                case "tutorial.lava-art.apply":
                    response.changes = PrometheusTutorialLavaArtAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " animated G-stage lava art (" + response.changes.Count + " change(s)).";
                    break;
                case "tilemap.clearance.audit":
                    response.records = PrometheusTilemapSceneIntegrator.AuditSolidColliders(
                        scene,
                        new Vector2(request.GetFloat("x"), request.GetFloat("y")),
                        new Vector2(request.GetFloat("width", 0.5f), request.GetFloat("height", 1f)));
                    response.message = $"Found {response.records.Count} solid collider(s) in the requested clearance area.";
                    break;
                case "tilemap.clearance.apply":
                    response.changes = PrometheusTilemapSceneIntegrator.ApplyTilemapClearance(
                        scene,
                        request.Get("markerId"),
                        request.Get("zone"),
                        new Vector2(request.GetFloat("x"), request.GetFloat("y")),
                        new Vector2(request.GetFloat("width", 1f), request.GetFloat("height", 1f)),
                        request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = $"{(request.dryRun ? "Previewed" : "Applied")} tilemap clearance " +
                                       $"'{request.Get("markerId")}' ({response.changes.Count} change(s)).";
                    break;
                case "title.scene.apply":
                    response.changes = PrometheusTitleSceneAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " animated title and loading scene (" + response.changes.Count + " change(s)).";
                    break;
                case "boss.polish.apply":
                    response.changes = PrometheusBossPolishAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " Helte boss polish (" + response.changes.Count + " change(s)).";
                    break;
                case "boss.helte-animation-v2.apply":
                    response.changes = PrometheusHelteAnimationV2Automation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " dedicated Helte PNG animation v2 (" +
                                       response.changes.Count + " change(s)).";
                    break;
                case "boss.helte-animation-v2.pacing":
                    response.changes = PrometheusHelteAnimationV2Automation.ApplyReadableMotionPacing(
                        scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " readable Helte animation pacing (" +
                                       response.changes.Count + " change(s)).";
                    break;
                case "art.prome-motion.apply":
                    response.changes = PrometheusPromeArtAutomation.Apply(scene, request.dryRun);
                    response.changed = !request.dryRun && response.changes.Count > 0;
                    response.message = (request.dryRun ? "Previewed" : "Applied") +
                                       " Prome dash, jump, and dialogue expressions (" +
                                       response.changes.Count + " change(s)).";
                    break;
                case "component.inspect":
                    InspectComponent(scene, request, response);
                    break;
                case "component.set":
                    SetComponent(scene, request, response);
                    break;
                case "code.usage":
                    response.records = PrometheusComponentAutomation.FindCodeUsage(
                        scene, request.Get("typeName"));
                    response.message = $"Found {response.records.Count} scene usage(s).";
                    break;
                case "flow.validate":
                    ValidateFlow(scene, request, response);
                    break;
            }
            response.success = !response.issues.Any(issue => issue.severity == PrometheusIssueSeverity.Error);
            return response;
        }

        private static Scene ResolveScene(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                var active = SceneManager.GetActiveScene();
                if (!active.IsValid() || !active.isLoaded)
                    throw new InvalidOperationException("No active scene is loaded.");
                return active;
            }
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var loaded = SceneManager.GetSceneAt(index);
                if (string.Equals(loaded.path, scenePath, StringComparison.Ordinal)) return loaded;
            }
            if (!File.Exists(scenePath)) throw new FileNotFoundException("Scene was not found.", scenePath);
            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        private static void AddSceneReport(Scene scene, PrometheusAiCommandResponse response)
        {
            var all = PrometheusSceneQuery.All(scene).ToArray();
            response.records.Add(new PrometheusAiRecord
            {
                id = AssetDatabase.AssetPathToGUID(scene.path),
                kind = "scene",
                hierarchyPath = scene.path,
                active = scene.isLoaded
            });
            response.message =
                $"objects={all.Length}; roots={scene.rootCount}; markers={all.Count(item => item.GetComponent<TutorialFunctionMarkerHost>() != null)}";
        }

        private static void AddMarkers(Scene scene, PrometheusAiCommandResponse response)
        {
            foreach (var marker in PrometheusSceneQuery.All(scene)
                         .Select(item => item.GetComponent<TutorialFunctionMarkerHost>())
                         .Where(marker => marker != null)
                         .OrderBy(marker => marker.MarkerId, StringComparer.Ordinal))
                response.records.Add(new PrometheusAiRecord
                {
                    id = marker.MarkerId,
                    kind = marker.Kind.ToString(),
                    hierarchyPath = PrometheusSceneQuery.Path(marker.gameObject),
                    position = marker.transform.position,
                    rotation = marker.transform.eulerAngles,
                    scale = marker.transform.lossyScale,
                    active = marker.gameObject.activeInHierarchy
                });
            response.message = $"Found {response.records.Count} marker(s).";
        }

        private static void CreateMarker(
            Scene scene,
            PrometheusAiCommandRequest request,
            PrometheusAiCommandResponse response)
        {
            var markerId = request.Get("markerId");
            if (!Enum.TryParse(request.Get("kind", "Point"), true, out TutorialFunctionMarkerKind kind))
                throw new ArgumentException($"Unknown marker kind '{request.Get("kind")}'.");
            var position = Vector(request, "x", "y", "z");
            var size = new Vector2(request.GetFloat("width", 1f), request.GetFloat("height", 1f));
            response.changes.Add(new PrometheusAiChange
            {
                action = "create-marker",
                hierarchyPath = markerId,
                after = $"kind={kind}; position={position}; size={size}"
            });
            if (request.dryRun)
            {
                response.message = "Marker creation previewed.";
                return;
            }
            var parent = ResolveParent(scene, request.Get("parentPath"));
            var created = PrometheusMarkerAuthoring.Create(scene, parent, kind, markerId, position, size);
            response.changed = true;
            response.changes[0].objectId = PrometheusSceneQuery.ObjectId(created);
            response.changes[0].hierarchyPath = PrometheusSceneQuery.Path(created);
            response.message = $"Created marker '{markerId}'.";
        }

        private static void MoveMarker(
            Scene scene,
            PrometheusAiCommandRequest request,
            PrometheusAiCommandResponse response)
        {
            var markerId = request.Get("markerId");
            var size = request.Get("width") == "" || request.Get("height") == ""
                ? (Vector2?)null
                : new Vector2(request.GetFloat("width"), request.GetFloat("height"));
            if (!PrometheusMarkerAuthoring.Move(
                    scene,
                    markerId,
                    Vector(request, "x", "y", "z"),
                    request.GetFloat("rotationZ"),
                    size,
                    request.dryRun,
                    out var change))
                throw new InvalidOperationException($"Marker '{markerId}' was not found.");
            response.changes.Add(change);
            response.changed = !request.dryRun;
            response.message = $"{(request.dryRun ? "Previewed" : "Moved")} marker '{markerId}'.";
        }

        private static void CompareSnapshots(
            PrometheusAiCommandRequest request,
            PrometheusAiCommandResponse response)
        {
            var beforePath = request.Get("beforePath");
            var afterPath = request.Get("afterPath");
            var before = PrometheusSceneSnapshotService.Load(beforePath);
            var after = PrometheusSceneSnapshotService.Load(afterPath);
            var diff = PrometheusSceneSnapshotService.Compare(before, after, beforePath, afterPath);
            response.changes.AddRange(diff.added);
            response.changes.AddRange(diff.removed);
            response.changes.AddRange(diff.modified);
            response.message =
                $"added={diff.added.Count}; removed={diff.removed.Count}; modified={diff.modified.Count}";
        }

        private static void SetObjectActive(
            Scene scene,
            PrometheusAiCommandRequest request,
            PrometheusAiCommandResponse response)
        {
            if (!bool.TryParse(request.Get("active"), out var active))
                throw new FormatException("Argument 'active' must be true or false.");
            response.changes.Add(PrometheusComponentAutomation.SetActive(
                scene,
                request.Get("markerId"),
                request.Get("hierarchyPath"),
                request.Get("objectId"),
                active,
                request.dryRun));
            response.changed = !request.dryRun;
            response.message = $"{(request.dryRun ? "Previewed" : "Applied")} active state.";
        }

        private static void InspectComponent(
            Scene scene,
            PrometheusAiCommandRequest request,
            PrometheusAiCommandResponse response)
        {
            response.records = PrometheusComponentAutomation.Inspect(
                scene,
                request.Get("markerId"),
                request.Get("hierarchyPath"),
                request.Get("objectId"),
                request.Get("componentType"));
            response.message = $"Found {response.records.Count} serialized property record(s).";
        }

        private static void SetObjectTransform(
            Scene scene,
            PrometheusAiCommandRequest request,
            PrometheusAiCommandResponse response)
        {
            var hasScale = request.Get("scaleX") != "" &&
                           request.Get("scaleY") != "" &&
                           request.Get("scaleZ") != "";
            var scale = hasScale
                ? new Vector3(
                    request.GetFloat("scaleX"),
                    request.GetFloat("scaleY"),
                    request.GetFloat("scaleZ"))
                : (Vector3?)null;
            response.changes.Add(PrometheusComponentAutomation.SetTransform(
                scene,
                request.Get("markerId"),
                request.Get("hierarchyPath"),
                request.Get("objectId"),
                Vector(request, "x", "y", "z"),
                request.GetFloat("rotationZ"),
                scale,
                request.dryRun));
            response.changed = !request.dryRun;
            response.message = $"{(request.dryRun ? "Previewed" : "Applied")} Transform change.";
        }

        private static void SetComponent(
            Scene scene,
            PrometheusAiCommandRequest request,
            PrometheusAiCommandResponse response)
        {
            response.changes.Add(PrometheusComponentAutomation.Set(
                scene,
                request.Get("markerId"),
                request.Get("hierarchyPath"),
                request.Get("objectId"),
                request.Get("componentType"),
                request.Get("propertyPath"),
                request.Get("value"),
                request.dryRun));
            response.changed = !request.dryRun;
            response.message = $"{(request.dryRun ? "Previewed" : "Applied")} serialized property change.";
        }

        private static void ApplyBackgroundBackplate(
            Scene scene,
            PrometheusAiCommandRequest request,
            PrometheusAiCommandResponse response)
        {
            response.changes.Add(PrometheusBackgroundAutomation.Apply(
                scene,
                request.Get("locationKey"),
                request.Get("spritePath"),
                request.GetFloat("opacity", 1f),
                (int)request.GetFloat("sortingOrder", -1000f),
                request.GetFloat("cameraSpaceDepth", 20f),
                request.dryRun));
            response.changed = !request.dryRun;
            response.message =
                $"{(request.dryRun ? "Previewed" : "Applied")} tutorial background backplate.";
        }

        private static void ApplyZenithApproach(
            Scene scene,
            PrometheusAiCommandRequest request,
            PrometheusAiCommandResponse response)
        {
            response.changes.Add(PrometheusZenithApproachAutomation.Apply(
                scene,
                request.Get("spritePath"),
                request.Get("playerPath", "TutorialRuntimeRoot/StageRoot/PlayerRoot"),
                request.GetFloat("startWorldX", 239f),
                request.GetFloat("endWorldX", 867.87f),
                new Vector2(
                    request.GetFloat("farViewportX", 0.80f),
                    request.GetFloat("farViewportY", 0.70f)),
                new Vector2(
                    request.GetFloat("nearViewportX", 0.70f),
                    request.GetFloat("nearViewportY", 0.58f)),
                request.GetFloat("farScreenWidth", 0.14f),
                request.GetFloat("nearScreenWidth", 0.56f),
                request.GetFloat("farOpacity", 0.72f),
                request.GetFloat("nearOpacity", 1f),
                (int)request.GetFloat("sortingOrder", -990f),
                request.dryRun));
            response.changed = !request.dryRun;
            response.message =
                $"{(request.dryRun ? "Previewed" : "Applied")} continuous Zenith approach.";
        }

        private static void ValidateFlow(
            Scene scene,
            PrometheusAiCommandRequest request,
            PrometheusAiCommandResponse response)
        {
            var assetPath = request.Get("assetPath");
            var asset = AssetDatabase.LoadAssetAtPath<PrometheusZoneFlowAsset>(assetPath);
            if (asset == null) throw new FileNotFoundException("Flow asset was not found.", assetPath);
            response.issues = asset.Validate(scene);
            response.message = $"Flow validation found {response.issues.Count} issue(s).";
        }

        private static Transform ResolveParent(Scene scene, string hierarchyPath)
        {
            if (string.IsNullOrWhiteSpace(hierarchyPath)) return null;
            return PrometheusSceneQuery.All(scene)
                .FirstOrDefault(item =>
                    string.Equals(PrometheusSceneQuery.Path(item), hierarchyPath, StringComparison.Ordinal))
                ?.transform;
        }

        private static Vector3 Vector(
            PrometheusAiCommandRequest request,
            string x,
            string y,
            string z) =>
            new(request.GetFloat(x), request.GetFloat(y), request.GetFloat(z));

        private static string ReadArg(IReadOnlyList<string> args, string name, string fallback)
        {
            for (var index = 0; index < args.Count - 1; index++)
                if (string.Equals(args[index], name, StringComparison.Ordinal))
                    return args[index + 1];
            return fallback;
        }

        private sealed class UnityVectorConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override bool CanConvert(Type objectType) =>
                objectType == typeof(Vector2) || objectType == typeof(Vector3);

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                if (value is Vector2 vector2)
                {
                    writer.WritePropertyName("x");
                    writer.WriteValue(vector2.x);
                    writer.WritePropertyName("y");
                    writer.WriteValue(vector2.y);
                }
                else if (value is Vector3 vector3)
                {
                    writer.WritePropertyName("x");
                    writer.WriteValue(vector3.x);
                    writer.WritePropertyName("y");
                    writer.WriteValue(vector3.y);
                    writer.WritePropertyName("z");
                    writer.WriteValue(vector3.z);
                }
                writer.WriteEndObject();
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer) =>
                throw new NotSupportedException();
        }
    }
}
