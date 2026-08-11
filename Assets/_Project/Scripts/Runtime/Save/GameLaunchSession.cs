using System.IO;
using UnityEngine;

namespace Narthex.Save
{
    public enum GameLaunchMode
    {
        DirectDevelopment,
        NewGame,
        Continue,
        BossDevelopment
    }

    /// <summary>
    /// Carries the player's title-menu intent across the next scene load and provides
    /// save-file access to scenes that do not own a ServiceRoot yet (Title/Loading).
    /// </summary>
    public static class GameLaunchSession
    {
        private const string SaveFileName = "narthex_save.json";
        private const string LegacyTutorialSaveFileName = "narthex_tutorial_save.json";
        private static GameLaunchMode mode = GameLaunchMode.DirectDevelopment;
        private static GameLaunchMode pendingTutorialMode = GameLaunchMode.DirectDevelopment;

        public static GameLaunchMode Mode => mode;
        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
        public static string LegacyTutorialSavePath =>
            Path.Combine(Application.persistentDataPath, LegacyTutorialSaveFileName);

        public static SaveData LoadSave()
        {
            MigrateLegacyTutorialSave();
            return new SaveFileStore(SavePath).Load();
        }

        public static string ResolveSavePath(string requestedFileName)
        {
            if (string.IsNullOrWhiteSpace(requestedFileName) ||
                requestedFileName == SaveFileName ||
                requestedFileName == LegacyTutorialSaveFileName)
                return SavePath;
            return Path.Combine(Application.persistentDataPath, requestedFileName);
        }

        public static bool CanContinue(SaveData data)
        {
            if (data == null || data.Permanent == null || data.Run == null || data.Permanent.TutorialCompleted)
                return false;

            return !string.IsNullOrWhiteSpace(data.Run.CurrentStageId) ||
                   !string.IsNullOrWhiteSpace(data.Run.TutorialIntroStageId) ||
                   !string.IsNullOrWhiteSpace(data.Run.SavedQuestId) ||
                   data.Run.HasSavedPlayerPosition ||
                   (data.Run.QuestIds != null && data.Run.QuestIds.Count > 0);
        }

        public static void PrepareNewGame()
        {
            var current = LoadSave();
            ResetSaveStore(LegacyTutorialSavePath);
            new SaveFileStore(SavePath).Save(CreateFreshSavePreservingSettings(current));
            pendingTutorialMode = GameLaunchMode.DirectDevelopment;
            mode = GameLaunchMode.NewGame;
        }

        public static bool PrepareContinue()
        {
            if (!CanContinue(LoadSave())) return false;
            mode = GameLaunchMode.Continue;
            return true;
        }

        public static void PrepareBossDevelopment() => mode = GameLaunchMode.BossDevelopment;

        public static bool ConsumeTutorialLaunchOverride()
        {
            var skipDevelopmentReset = mode == GameLaunchMode.NewGame || mode == GameLaunchMode.Continue;
            pendingTutorialMode = skipDevelopmentReset ? mode : GameLaunchMode.DirectDevelopment;
            mode = GameLaunchMode.DirectDevelopment;
            return skipDevelopmentReset;
        }

        public static bool TryConsumeContinuePosition(RunSaveData run, out Vector2 position)
        {
            position = default;
            var isContinue = pendingTutorialMode == GameLaunchMode.Continue;
            pendingTutorialMode = GameLaunchMode.DirectDevelopment;
            return isContinue && TryResolveSavedPlayerPosition(run, out position);
        }

        public static bool TryResolveSavedPlayerPosition(RunSaveData run, out Vector2 position)
        {
            position = default;
            if (run == null || !run.HasSavedPlayerPosition) return false;
            if (!float.IsFinite(run.SavedPlayerPositionX) || !float.IsFinite(run.SavedPlayerPositionY)) return false;
            position = new Vector2(run.SavedPlayerPositionX, run.SavedPlayerPositionY);
            return true;
        }

        public static SaveData CreateFreshSavePreservingSettings(SaveData current) => new()
        {
            Settings = current?.Settings ?? new SettingsSaveData()
        };

        public static void CompleteTutorialLaunch() => pendingTutorialMode = GameLaunchMode.DirectDevelopment;

        public static void SaveSettings(SettingsSaveData settings)
        {
            var data = LoadSave();
            data.Settings = settings ?? new SettingsSaveData();
            new SaveFileStore(SavePath).Save(data);
        }

        public static void ResetAllLocalData()
        {
            ResetSaveStore(SavePath);
            ResetSaveStore(LegacyTutorialSavePath);
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Caching.ClearCache();
            mode = GameLaunchMode.DirectDevelopment;
            pendingTutorialMode = GameLaunchMode.DirectDevelopment;
        }

        private static void MigrateLegacyTutorialSave()
        {
            if (File.Exists(SavePath) || !File.Exists(LegacyTutorialSavePath)) return;
            var legacyData = new SaveFileStore(LegacyTutorialSavePath).Load();
            new SaveFileStore(SavePath).Save(legacyData);
            ResetSaveStore(LegacyTutorialSavePath);
        }

        public static void ResetSaveStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (File.Exists(path)) File.Delete(path);
            var temporaryPath = path + ".tmp";
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }

        public static void SaveTutorialContinuePoint(SaveData data, string questId, Vector2 position)
        {
            data ??= LoadSave();
            data.Run ??= new RunSaveData();
            data.Permanent ??= new PermanentSaveData();
            data.Run.SavedQuestId = questId ?? string.Empty;
            data.Run.HasSavedPlayerPosition = true;
            data.Run.SavedPlayerPositionX = position.x;
            data.Run.SavedPlayerPositionY = position.y;
            // Title and tutorial must share this canonical path even if an editor test or
            // scene-local SaveSystemHost is configured with a different file name.
            new SaveFileStore(SavePath).Save(data);
        }

        public static void MarkDemoFinished()
        {
            var data = LoadSave();
            data.Permanent.TutorialCompleted = true;
            data.Run = new RunSaveData();
            new SaveFileStore(SavePath).Save(data);
            mode = GameLaunchMode.DirectDevelopment;
            pendingTutorialMode = GameLaunchMode.DirectDevelopment;
        }
    }
}
