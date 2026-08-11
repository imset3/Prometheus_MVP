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
        private static GameLaunchMode mode = GameLaunchMode.DirectDevelopment;

        public static GameLaunchMode Mode => mode;
        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static SaveData LoadSave() => new SaveFileStore(SavePath).Load();

        public static bool CanContinue(SaveData data)
        {
            if (data == null || data.Permanent == null || data.Run == null || data.Permanent.TutorialCompleted)
                return false;

            return !string.IsNullOrWhiteSpace(data.Run.CurrentStageId) ||
                   !string.IsNullOrWhiteSpace(data.Run.TutorialIntroStageId) ||
                   (data.Run.QuestIds != null && data.Run.QuestIds.Count > 0);
        }

        public static void PrepareNewGame()
        {
            var current = LoadSave();
            new SaveFileStore(SavePath).Save(new SaveData
            {
                Settings = current?.Settings ?? new SettingsSaveData()
            });
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
            mode = GameLaunchMode.DirectDevelopment;
            return skipDevelopmentReset;
        }

        public static void SaveSettings(SettingsSaveData settings)
        {
            var data = LoadSave();
            data.Settings = settings ?? new SettingsSaveData();
            new SaveFileStore(SavePath).Save(data);
        }

        public static void MarkDemoFinished()
        {
            var data = LoadSave();
            data.Permanent.TutorialCompleted = true;
            data.Run = new RunSaveData();
            new SaveFileStore(SavePath).Save(data);
            mode = GameLaunchMode.DirectDevelopment;
        }
    }
}
