using System.Collections;
using System.IO;
using System.Linq;
using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Presentation
{
    /// <summary>
    /// Opt-in player-build verification. It never runs during normal play and exists so release
    /// binaries can prove that stripped/runtime activation paths still show training and F enemies.
    /// </summary>
    public sealed class TutorialBuildQaProbe : MonoBehaviour
    {
        private const string Argument = "-prometheus-build-qa";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!System.Environment.GetCommandLineArgs().Contains(Argument)) return;
            if (FindFirstObjectByType<TutorialBuildQaProbe>() != null) return;
            DontDestroyOnLoad(new GameObject(nameof(TutorialBuildQaProbe))
                .AddComponent<TutorialBuildQaProbe>().gameObject);
        }

        private IEnumerator Start()
        {
            var operation = SceneManager.LoadSceneAsync("TutorialScene", LoadSceneMode.Single);
            while (operation != null && !operation.isDone) yield return null;
            yield return new WaitForSecondsRealtime(0.75f);

            var sequence = FindSceneComponent<TutorialQuestSequenceHost>();
            var phase = FindSceneComponent<TutorialTrainingPhaseControllerHost>();
            var skip = FindSceneComponent<TutorialDebugSectionSkipHost>();
            var background = FindSceneComponent<TutorialBackgroundPresenter>();
            var player = FindSceneComponent<PlayerMotorHost>();
            if (sequence == null || phase == null || skip == null || background == null || player == null)
            {
                Finish(false, "required runtime hosts missing");
                yield break;
            }

            if (!sequence.TryDebugJumpToQuest("QST-TUTO-002"))
            {
                Finish(false, "failed to enter jump quest");
                yield break;
            }
            phase.RefreshCurrentQuest();
            var jumpTraining = FindSceneComponent<TutorialJumpTrainingHost>();
            var maxConcurrentProjectiles = 0;
            var jumpSampleEnd = Time.realtimeSinceStartup + 6.25f;
            while (Time.realtimeSinceStartup < jumpSampleEnd)
            {
                maxConcurrentProjectiles = Mathf.Max(
                    maxConcurrentProjectiles,
                    jumpTraining != null ? jumpTraining.ActiveProjectileCount : 0);
                yield return new WaitForSecondsRealtime(0.1f);
            }
            var projectile = FindActive("JumpProjectileVisual_ART");
            Capture("01_jump_projectile.png");
            LogState("jump-projectile", projectile);
            Debug.Log($"[BUILD-QA] jump-spacing maxConcurrent={maxConcurrentProjectiles} " +
                      $"safeInterval={jumpTraining?.SafeLaunchInterval:0.00}");

            if (!sequence.TryDebugJumpToQuest("QST-TUTO-003"))
            {
                Finish(false, "failed to enter melee quest");
                yield break;
            }
            phase.RefreshCurrentQuest();
            yield return new WaitForSecondsRealtime(1.25f);
            var dummy = FindActive("TrainingDummyVisual_ART");
            Capture("02_melee_dummy.png");
            LogState("melee-dummy", dummy);

            if (!sequence.TryDebugJumpToQuest("QST-TUTO-005"))
            {
                Finish(false, "failed to enter ranged quest");
                yield break;
            }
            phase.RefreshCurrentQuest();
            yield return new WaitForSecondsRealtime(0.75f);
            var rangedFlow = FindSceneComponent<TutorialImportedTrainingFlowHost>();
            var visibleRangedTargets = rangedFlow?.VisibleRangedTargetCount ?? 0;
            Debug.Log($"[BUILD-QA] ranged-dummies visible={visibleRangedTargets}");

            if (!skip.JumpToFSection())
            {
                Finish(false, "failed to enter F encounter");
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.25f);
            var encounter = FindSceneComponent<TutorialSimultaneousEncounterHost>();
            var activeEnemies = encounter?.ActiveEnemyCount ?? 0;
            Capture("03_f_enemies.png");
            Debug.Log($"[BUILD-QA] f-enemies active={activeEnemies} background={background.CurrentKey}");

            if (!skip.JumpToNextSection())
            {
                Finish(false, "failed to enter G encounter");
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.75f);
            var wave = FindSceneComponent<TutorialWaveEncounterHost>();
            var activeWaveEnemies = wave?.ActiveEnemyCount ?? 0;
            var lavaVisual = FindActive("LavaAnimatedVisual_ART");
            Capture("04_g_enemies.png");
            Debug.Log($"[BUILD-QA] g-enemies active={activeWaveEnemies} background={background.CurrentKey} " +
                      $"lavaVisible={lavaVisual != null}");

            Finish(projectile != null && maxConcurrentProjectiles == 1 && dummy != null &&
                   visibleRangedTargets == 3 && activeEnemies > 0 && background.CurrentKey == "G" &&
                   activeWaveEnemies > 0 && lavaVisual != null,
                $"projectile={projectile != null}, maxConcurrent={maxConcurrentProjectiles}, " +
                $"dummy={dummy != null}, ranged={visibleRangedTargets}, fEnemies={activeEnemies}, " +
                $"gEnemies={activeWaveEnemies}, lava={lavaVisual != null}, background={background.CurrentKey}");
        }

        private static T FindSceneComponent<T>() where T : Component =>
            Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(candidate =>
                candidate != null && candidate.gameObject.scene.name == "TutorialScene");

        private static GameObject FindActive(string objectName) =>
            Resources.FindObjectsOfTypeAll<Transform>()
                .FirstOrDefault(candidate => candidate != null && candidate.name == objectName &&
                                             candidate.gameObject.scene.name == "TutorialScene" &&
                                             candidate.gameObject.activeInHierarchy &&
                                             candidate.GetComponent<SpriteRenderer>() is { enabled: true, sprite: not null })
                ?.gameObject;

        private static void LogState(string label, GameObject target) =>
            Debug.Log($"[BUILD-QA] {label} visible={target != null} path={GetPath(target?.transform)}");

        private static string GetPath(Transform target)
        {
            if (target == null) return "missing";
            var path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }
            return path;
        }

        private static void Capture(string fileName)
        {
            var directory = Path.Combine(Application.persistentDataPath, "BuildQa");
            Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, fileName));
        }

        private static void Finish(bool passed, string detail)
        {
            Debug.Log($"[BUILD-QA] RESULT={(passed ? "PASS" : "FAIL")} {detail}");
            Application.Quit(passed ? 0 : 2);
        }
    }
}
