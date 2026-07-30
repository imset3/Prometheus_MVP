using System;
using System.IO;
using System.Linq;
using Narthex.Gameplay;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools.EditorTests
{
    public sealed class PrometheusSceneToolkitTests
    {
        private Scene scene;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void MarkerAuthoring_CreatesStableInspectableMarker()
        {
            var created = PrometheusMarkerAuthoring.Create(
                scene,
                null,
                TutorialFunctionMarkerKind.Wind,
                "TEST-WIND-001",
                new Vector3(4f, 7f, 0f),
                new Vector2(2f, 8f),
                false);

            var marker = created.GetComponent<TutorialFunctionMarkerHost>();
            Assert.That(marker.MarkerId, Is.EqualTo("TEST-WIND-001"));
            Assert.That(marker.Kind, Is.EqualTo(TutorialFunctionMarkerKind.Wind));
            Assert.That(created.GetComponent<BoxCollider2D>().isTrigger, Is.True);
            Assert.That(PrometheusMarkerAuthoring.FindById(scene, "TEST-WIND-001"), Is.SameAs(created));
        }

        [Test]
        public void SceneDoctor_ReportsDuplicateMarkerIds()
        {
            PrometheusMarkerAuthoring.Create(
                scene, null, TutorialFunctionMarkerKind.Point, "DUPLICATE", Vector3.zero, Vector2.one, false);
            var second = PrometheusMarkerAuthoring.Create(
                scene, null, TutorialFunctionMarkerKind.Point, "UNIQUE", Vector3.one, Vector2.one, false);
            var marker = second.GetComponent<TutorialFunctionMarkerHost>();
            var serialized = new UnityEditor.SerializedObject(marker);
            serialized.FindProperty("markerId").stringValue = "DUPLICATE";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var issues = PrometheusSceneDoctor.Scan(scene);

            Assert.That(issues.Count(issue => issue.rule == "marker-id-duplicate"), Is.EqualTo(2));
        }

        [Test]
        public void SnapshotCompare_ReportsTransformModification()
        {
            var target = new GameObject("Target");
            SceneManager.MoveGameObjectToScene(target, scene);
            var before = PrometheusSceneSnapshotService.Capture(scene);
            target.transform.position = new Vector3(5f, 2f, 0f);
            var after = PrometheusSceneSnapshotService.Capture(scene);

            var diff = PrometheusSceneSnapshotService.Compare(before, after);

            Assert.That(diff.modified.Any(change => change.hierarchyPath == "Target"), Is.True);
        }

        [Test]
        public void AiCommands_DefaultToDryRunAndDoNotMoveMarker()
        {
            var marker = PrometheusMarkerAuthoring.Create(
                scene, null, TutorialFunctionMarkerKind.Point, "MOVE-ME", Vector3.zero, Vector2.one, false);
            var request = new PrometheusAiCommandRequest
            {
                requestId = "test",
                command = "marker.move",
                dryRun = true,
                arguments =
                {
                    new PrometheusAiArgument { key = "markerId", value = "MOVE-ME" },
                    new PrometheusAiArgument { key = "x", value = "10" },
                    new PrometheusAiArgument { key = "y", value = "20" }
                }
            };

            var response = PrometheusAiCommandRunner.Execute(request);

            Assert.That(response.success, Is.True);
            Assert.That(response.changed, Is.False);
            Assert.That(marker.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(response.changes, Has.Count.EqualTo(1));
        }

        [Test]
        public void ZoneFlowValidation_ReportsBrokenEdgeAndMissingMarker()
        {
            var flow = ScriptableObject.CreateInstance<PrometheusZoneFlowAsset>();
            flow.Configure(
                "",
                "entry",
                new[]
                {
                    new PrometheusZoneFlowNode
                    {
                        id = "entry",
                        markerId = "NOT-IN-SCENE",
                        nextNodeIds = { "missing" }
                    }
                });

            var issues = flow.Validate(scene);

            Assert.That(issues.Any(issue => issue.rule == "flow-edge-broken"), Is.True);
            Assert.That(issues.Any(issue => issue.rule == "flow-marker-missing"), Is.True);
            UnityEngine.Object.DestroyImmediate(flow);
        }

        [Test]
        public void ComponentAutomation_DryRunDoesNotApplyAndCommitDoes()
        {
            var target = new GameObject("Editable");
            SceneManager.MoveGameObjectToScene(target, scene);
            var collider = target.AddComponent<BoxCollider2D>();

            var preview = PrometheusComponentAutomation.Set(
                scene, "", "Editable", "", "BoxCollider2D", "m_IsTrigger", "true", true);
            Assert.That(preview.action, Is.EqualTo("set-component-property"));
            Assert.That(collider.isTrigger, Is.False);

            PrometheusComponentAutomation.Set(
                scene, "", "Editable", "", "BoxCollider2D", "m_IsTrigger", "true", false);
            Assert.That(collider.isTrigger, Is.True);

            PrometheusComponentAutomation.Set(
                scene, "", "Editable", "", "BoxCollider2D", "m_Size", "3.5,7.25", false);
            Assert.That(collider.size, Is.EqualTo(new Vector2(3.5f, 7.25f)));
        }

        [Test]
        public void RunFile_WritesMachineReadableResponse()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"prometheus-toolkit-{Guid.NewGuid():N}");
            var requestPath = Path.Combine(directory, "request.json");
            var responsePath = Path.Combine(directory, "response.json");
            Directory.CreateDirectory(directory);
            try
            {
                var request = new PrometheusAiCommandRequest
                {
                    requestId = "file-test",
                    command = "scene.report",
                    dryRun = true
                };
                File.WriteAllText(requestPath, JsonUtility.ToJson(request, true));

                var response = PrometheusAiCommandRunner.RunFile(requestPath, responsePath);

                Assert.That(response.success, Is.True);
                Assert.That(File.Exists(responsePath), Is.True);
                Assert.That(File.ReadAllText(responsePath), Does.Contain("\"requestId\": \"file-test\""));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SerializeResponse_UsesCompactUnityVectorShape()
        {
            var response = new PrometheusAiCommandResponse
            {
                success = true,
                records =
                {
                    new PrometheusAiRecord
                    {
                        id = "MARKER",
                        kind = "Wind",
                        position = new Vector3(1f, 2f, 0f)
                    }
                }
            };

            var json = PrometheusAiCommandRunner.SerializeResponse(response);

            Assert.That(json, Does.Contain("\"x\": 1.0"));
            Assert.That(json, Does.Contain("\"y\": 2.0"));
            Assert.That(json, Does.Not.Contain("magnitude"));
            Assert.That(json.Length, Is.LessThan(1000));
        }
    }
}
