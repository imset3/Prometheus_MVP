using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Presentation;
using Narthex.Save;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusTutorialMusicAutomation
    {
        public const string PendingRequestPath =
            "Temp/PrometheusSceneToolkit/tutorial-music-request.json";
        public const string PendingResponsePath =
            "Temp/PrometheusSceneToolkit/tutorial-music-response.json";
        private const string AudioRootPath = "TutorialRuntimeRoot/StageRoot/TutorialAudioRoot";
        private const string PrototypeRoot = "Assets/_Project/Audio/Music/Tutorial/Prototypes/";

        private static readonly string[] SourceNames =
        {
            "MusicSourceA",
            "MusicSourceB",
            "OuterCombatIntensitySource",
            "HelteMusicBaseSource",
            "HelteMusicPhase2Source",
            "HelteMusicFinalSource",
            "HelteVictorySource"
        };

        [MenuItem(PrometheusToolMenuPaths.Ai + "Run Tutorial Music Command")]
        public static void RunPendingMusicCommand()
        {
            var response = PrometheusAiCommandRunner.RunFile(PendingRequestPath, PendingResponsePath);
            Debug.Log($"[Prometheus Tutorial Music] {response.message}\n{PendingResponsePath}");
        }

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (!dryRun && EditorApplication.isPlaying)
                throw new InvalidOperationException("Tutorial music scene integration requires Edit Mode.");

            var root = PrometheusSceneQuery.Resolve(scene, string.Empty, AudioRootPath, string.Empty);
            if (root == null)
                throw new InvalidOperationException($"Tutorial audio root was not found: {AudioRootPath}");

            var all = PrometheusSceneQuery.All(scene).ToArray();
            var serviceRoot = FindRequired<ServiceRoot>(all);
            var saveSystemHost = FindRequired<SaveSystemHost>(all);
            var bossArenaHost = FindRequired<TutorialBossArenaHost>(all);
            var heltePatternHost = FindRequired<HelteBossPatternHost>(all);
            var changes = DescribeChanges(root);
            if (dryRun) return changes;

            PrometheusVictoryMusicGenerator.Generate(false);
            var clips = LoadClips();

            var sources = new Dictionary<string, AudioSource>(StringComparer.Ordinal);
            foreach (var sourceName in SourceNames)
                sources[sourceName] = EnsureSource(root.transform, sourceName);

            var director = root.GetComponent<TutorialMusicDirector>();
            if (director == null) director = Undo.AddComponent<TutorialMusicDirector>(root);
            Undo.RecordObject(director, "Configure tutorial music director");
            var serialized = new SerializedObject(director);
            Assign(serialized, "serviceRoot", serviceRoot);
            Assign(serialized, "saveSystemHost", saveSystemHost);
            Assign(serialized, "bossArenaHost", bossArenaHost);
            Assign(serialized, "heltePatternHost", heltePatternHost);
            Assign(serialized, "musicSourceA", sources["MusicSourceA"]);
            Assign(serialized, "musicSourceB", sources["MusicSourceB"]);
            Assign(serialized, "outerIntensitySource", sources["OuterCombatIntensitySource"]);
            Assign(serialized, "helteBaseSource", sources["HelteMusicBaseSource"]);
            Assign(serialized, "heltePhaseTwoSource", sources["HelteMusicPhase2Source"]);
            Assign(serialized, "helteFinalSource", sources["HelteMusicFinalSource"]);
            Assign(serialized, "victorySource", sources["HelteVictorySource"]);
            Assign(serialized, "adamasLoop", clips.adamas);
            Assign(serialized, "outerCombatBaseLoop", clips.outerBase);
            Assign(serialized, "outerCombatIntensityLoop", clips.outerIntensity);
            Assign(serialized, "helteBaseLoop", clips.helteBase);
            Assign(serialized, "heltePhaseTwoLoop", clips.heltePhaseTwo);
            Assign(serialized, "helteFinalLoop", clips.helteFinal);
            Assign(serialized, "victoryLoop", clips.victory);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(scene);

            foreach (var change in changes)
            {
                var target = change.hierarchyPath == AudioRootPath
                    ? root
                    : root.transform.Find(change.hierarchyPath.Substring(AudioRootPath.Length + 1))?.gameObject;
                if (target == null) continue;
                change.objectId = PrometheusSceneQuery.ObjectId(target);
                change.hierarchyPath = PrometheusSceneQuery.Path(target);
            }
            return changes;
        }

        private static List<PrometheusAiChange> DescribeChanges(GameObject root)
        {
            var changes = new List<PrometheusAiChange>();
            foreach (var sourceName in SourceNames)
            {
                var child = root.transform.Find(sourceName);
                changes.Add(new PrometheusAiChange
                {
                    action = child == null ? "create-music-source" : "configure-music-source",
                    hierarchyPath = $"{AudioRootPath}/{sourceName}",
                    before = child == null ? "missing" : "existing",
                    after = "AudioSource; loop=true; playOnAwake=false; spatialBlend=0; volume=0"
                });
            }
            changes.Add(new PrometheusAiChange
            {
                action = root.GetComponent<TutorialMusicDirector>() == null
                    ? "add-music-director"
                    : "configure-music-director",
                hierarchyPath = AudioRootPath,
                before = root.GetComponent<TutorialMusicDirector>() == null ? "missing" : "existing",
                after = "location crossfade + outer intensity + Helte synchronized phase layers"
            });
            return changes;
        }

        private static AudioSource EnsureSource(Transform parent, string name)
        {
            var child = parent.Find(name)?.gameObject;
            if (child == null)
            {
                child = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(child, "Create tutorial music source");
                child.transform.SetParent(parent, false);
            }
            var source = child.GetComponent<AudioSource>();
            if (source == null) source = Undo.AddComponent<AudioSource>(child);
            Undo.RecordObject(source, "Configure tutorial music source");
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = 64;
            EditorUtility.SetDirty(source);
            return source;
        }

        private static T FindRequired<T>(IEnumerable<GameObject> objects) where T : Component
        {
            var component = objects.Select(item => item.GetComponent<T>()).FirstOrDefault(item => item != null);
            if (component == null) throw new InvalidOperationException($"Scene requires {typeof(T).Name}.");
            return component;
        }

        private static void Assign(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new MissingFieldException(typeof(TutorialMusicDirector).Name, propertyName);
            property.objectReferenceValue = value;
        }

        private static MusicClips LoadClips() => new()
        {
            adamas = Load("MUS_TUTO_Adamas_Prototype_Loop.wav"),
            outerBase = Load("MUS_TUTO_OuterCombat_Base_Prototype_Loop.wav"),
            outerIntensity = Load("MUS_TUTO_OuterCombat_Intensity_Prototype_Loop.wav"),
            helteBase = Load("MUS_BOSS_Helte_Base_Prototype_Loop.wav"),
            heltePhaseTwo = Load("MUS_BOSS_Helte_Phase2_Prototype_Loop.wav"),
            helteFinal = Load("MUS_BOSS_Helte_Final_Prototype_Loop.wav"),
            victory = Load("MUS_TUTO_HelteDefeat_Prototype_Loop.wav")
        };

        private static AudioClip Load(string fileName)
        {
            var path = PrototypeRoot + fileName;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException($"Music clip was not imported: {path}");
            return clip;
        }

        private sealed class MusicClips
        {
            public AudioClip adamas;
            public AudioClip outerBase;
            public AudioClip outerIntensity;
            public AudioClip helteBase;
            public AudioClip heltePhaseTwo;
            public AudioClip helteFinal;
            public AudioClip victory;
        }
    }
}
