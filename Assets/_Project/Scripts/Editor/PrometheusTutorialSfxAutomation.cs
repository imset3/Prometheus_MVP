using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Gameplay;
using Narthex.Presentation;
using Narthex.Save;
using Narthex.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusTutorialSfxAutomation
    {
        public const string PendingRequestPath = "Temp/PrometheusSceneToolkit/tutorial-sfx-request.json";
        public const string PendingResponsePath = "Temp/PrometheusSceneToolkit/tutorial-sfx-response.json";
        private const string AudioRootPath = "TutorialRuntimeRoot/StageRoot/TutorialAudioRoot";
        private const string PrototypeRoot = "Assets/_Project/Audio/Sfx/Tutorial/Prototypes/";

        private static readonly Dictionary<string, string> ClipFields = new(StringComparer.Ordinal)
        {
            ["playerMeleeSwingA"] = "SFX_Player_Melee_Swing_A.wav",
            ["playerMeleeSwingB"] = "SFX_Player_Melee_Swing_B.wav",
            ["playerRangedFire"] = "SFX_Player_Ranged_Fire.wav",
            ["impactLightA"] = "SFX_Impact_Light_A.wav",
            ["impactLightB"] = "SFX_Impact_Light_B.wav",
            ["impactHeavy"] = "SFX_Impact_Heavy.wav",
            ["playerHit"] = "SFX_Player_Hit.wav",
            ["playerDeath"] = "SFX_Player_Death.wav",
            ["enemyDeath"] = "SFX_Enemy_Death.wav",
            ["playerJump"] = "SFX_Player_Jump.wav",
            ["playerDash"] = "SFX_Player_Dash.wav",
            ["focusedVolleyStart"] = "SFX_Skill_FocusedVolley_Start.wav",
            ["focusedVolleyShot"] = "SFX_Skill_FocusedVolley_Shot.wav",
            ["fourSlashStart"] = "SFX_Skill_FourSlash_Start.wav",
            ["fourSlashHit"] = "SFX_Skill_FourSlash_Hit.wav",
            ["enemyMeleeTelegraph"] = "SFX_Enemy_Melee_Telegraph.wav",
            ["enemyMeleeAttack"] = "SFX_Enemy_Melee_Attack.wav",
            ["enemyRangedTelegraph"] = "SFX_Enemy_Ranged_Telegraph.wav",
            ["enemyRangedFire"] = "SFX_Enemy_Ranged_Fire.wav",
            ["helteIntroWarning"] = "SFX_Helte_Intro_Warning.wav",
            ["heltePhaseTwo"] = "SFX_Helte_Phase2.wav",
            ["helteFinalRush"] = "SFX_Helte_FinalRush.wav",
            ["helteBasicWindup"] = "SFX_Helte_Basic_Windup.wav",
            ["helteSlash"] = "SFX_Helte_Slash.wav",
            ["helteBlinkOut"] = "SFX_Helte_Blink_Out.wav",
            ["helteBlinkIn"] = "SFX_Helte_Blink_In.wav",
            ["helteDashTelegraph"] = "SFX_Helte_Dash_Telegraph.wav",
            ["helteDash"] = "SFX_Helte_Dash.wav",
            ["helteCrossTelegraph"] = "SFX_Helte_Cross_Telegraph.wav",
            ["helteCrossSlash"] = "SFX_Helte_Cross_Slash.wav",
            ["helteSwordFocus"] = "SFX_Helte_Sword_Focus.wav",
            ["helteSwordFire"] = "SFX_Helte_Sword_Fire.wav",
            ["helteCounterTelegraph"] = "SFX_Helte_Counter_Telegraph.wav",
            ["helteCounter"] = "SFX_Helte_Counter.wav",
            ["helteMercy"] = "SFX_Helte_Mercy.wav",
            ["helteVictory"] = "SFX_Helte_Victory.wav",
            ["dialogueAdvance"] = "SFX_UI_Dialogue_Advance.wav",
            ["objectiveUpdated"] = "SFX_UI_Objective_Update.wav",
            ["tutorialCompleted"] = "SFX_UI_Tutorial_Complete.wav",
            ["panelOpen"] = "SFX_UI_Panel_Open.wav",
            ["itemPickup"] = "SFX_World_Item_Pickup.wav",
            ["relayActivate"] = "SFX_World_Relay_Activate.wav",
            ["gateOpen"] = "SFX_World_Gate_Open.wav",
            ["encounterStart"] = "SFX_World_Encounter_Start.wav",
            ["encounterClear"] = "SFX_World_Encounter_Clear.wav"
        };

        [MenuItem(PrometheusToolMenuPaths.Ai + "Run Tutorial SFX Command")]
        public static void RunPendingSfxCommand()
        {
            var response = PrometheusAiCommandRunner.RunFile(PendingRequestPath, PendingResponsePath);
            Debug.Log($"[Prometheus Tutorial SFX] {response.message}\n{PendingResponsePath}");
        }

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (!dryRun && EditorApplication.isPlaying)
                throw new InvalidOperationException("Tutorial SFX scene integration requires Edit Mode.");

            var root = PrometheusSceneQuery.Resolve(scene, string.Empty, AudioRootPath, string.Empty);
            if (root == null) throw new InvalidOperationException($"Tutorial audio root was not found: {AudioRootPath}");
            var all = PrometheusSceneQuery.All(scene).ToArray();
            var changes = DescribeChanges(root);
            ValidateClips();
            if (dryRun) return changes;

            var serviceRoot = FindRequired<ServiceRoot>(all);
            var saveSystemHost = FindRequired<SaveSystemHost>(all);
            var playerInput = FindRequired<PlayerInputHost>(all);
            var playerActor = all.Select(item => item.GetComponent<CombatActorHost>())
                .FirstOrDefault(item => item != null && item.Kind == CombatActorKind.Player);
            var bossActor = all.Select(item => item.GetComponent<CombatActorHost>())
                .FirstOrDefault(item => item != null && item.Kind == CombatActorKind.Boss);
            if (playerActor == null || bossActor == null)
                throw new InvalidOperationException("Tutorial SFX requires player and boss CombatActorHost references.");

            var uiSource = EnsureSource(root.transform, "UIAudioSource", 96);
            var worldSource = EnsureSource(root.transform, "WorldAudioSource", 104);
            var playerSource = EnsureSource(root.transform, "PlayerSfxSource", 80);
            var enemySource = EnsureSource(root.transform, "EnemySfxSource", 112);
            var bossSource = EnsureSource(root.transform, "BossSfxSource", 72);

            var director = root.GetComponent<TutorialSfxDirector>();
            if (director == null) director = Undo.AddComponent<TutorialSfxDirector>(root);
            Undo.RecordObject(director, "Configure tutorial SFX director");
            var serialized = new SerializedObject(director);
            Assign(serialized, "serviceRoot", serviceRoot);
            Assign(serialized, "saveSystemHost", saveSystemHost);
            Assign(serialized, "playerInputHost", playerInput);
            Assign(serialized, "playerActor", playerActor);
            Assign(serialized, "bossActor", bossActor);
            Assign(serialized, "playerMeleeAttack", FindRequired<MeleeAttackHost>(all));
            Assign(serialized, "playerRangedAttack", FindRequired<PlayerRangedAttackHost>(all));
            Assign(serialized, "bossArenaHost", FindRequired<TutorialBossArenaHost>(all));
            Assign(serialized, "heltePatternHost", FindRequired<HelteBossPatternHost>(all));
            Assign(serialized, "promeBossSkillHost", all.Select(item => item.GetComponent<PromeBossSkillHost>())
                .FirstOrDefault(item => item != null));
            Assign(serialized, "theusRangedSupportHost", all.Select(item => item.GetComponent<TutorialTheusRangedSupportHost>())
                .FirstOrDefault(item => item != null));
            Assign(serialized, "uiSource", uiSource);
            Assign(serialized, "playerSource", playerSource);
            Assign(serialized, "enemySource", enemySource);
            Assign(serialized, "bossSource", bossSource);
            Assign(serialized, "worldSource", worldSource);
            AssignArray(serialized, "enemyMeleeAttacks", all.Select(item => item.GetComponent<EnemyAttackHost>())
                .Where(item => item != null && item.GetComponent<CombatActorHost>()?.Kind != CombatActorKind.Boss)
                .Distinct().ToArray());
            AssignArray(serialized, "enemyRangedAttacks", all.Select(item => item.GetComponent<TutorialRangedEnemyHost>())
                .Where(item => item != null).Distinct().ToArray());
            foreach (var pair in ClipFields) Assign(serialized, pair.Key, Load(pair.Value));
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
            foreach (var name in new[] { "UIAudioSource", "WorldAudioSource", "PlayerSfxSource", "EnemySfxSource", "BossSfxSource" })
            {
                var child = root.transform.Find(name);
                changes.Add(new PrometheusAiChange
                {
                    action = child == null ? "create-sfx-source" : "configure-sfx-source",
                    hierarchyPath = $"{AudioRootPath}/{name}",
                    before = child == null ? "missing" : "existing",
                    after = "AudioSource; loop=false; playOnAwake=false; spatialBlend=0; volume=1"
                });
            }
            changes.Add(new PrometheusAiChange
            {
                action = root.GetComponent<TutorialSfxDirector>() == null ? "add-sfx-director" : "configure-sfx-director",
                hierarchyPath = AudioRootPath,
                before = root.GetComponent<TutorialSfxDirector>() == null ? "missing" : "existing",
                after = "event-driven combat, boss, UI, flow SFX + saved SfxVolume"
            });
            return changes;
        }

        private static AudioSource EnsureSource(Transform parent, string name, int priority)
        {
            var child = parent.Find(name)?.gameObject;
            if (child == null)
            {
                child = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(child, "Create tutorial SFX source");
                child.transform.SetParent(parent, false);
            }
            var source = child.GetComponent<AudioSource>();
            if (source == null) source = Undo.AddComponent<AudioSource>(child);
            Undo.RecordObject(source, "Configure tutorial SFX source");
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.priority = priority;
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
            if (property == null) throw new MissingFieldException(typeof(TutorialSfxDirector).Name, propertyName);
            property.objectReferenceValue = value;
        }

        private static void AssignArray<T>(SerializedObject serialized, string propertyName, T[] values) where T : UnityEngine.Object
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new MissingFieldException(typeof(TutorialSfxDirector).Name, propertyName);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void ValidateClips()
        {
            foreach (var fileName in ClipFields.Values) Load(fileName);
        }

        private static AudioClip Load(string fileName)
        {
            var path = PrototypeRoot + fileName;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException($"SFX clip was not imported: {path}");
            return clip;
        }
    }
}
