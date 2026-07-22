using System;
using System.Collections.Generic;

namespace Narthex.Save
{
    [Serializable]
    public sealed class SaveData
    {
        public PermanentSaveData Permanent = new PermanentSaveData();
        public RunSaveData Run = new RunSaveData();
        public SettingsSaveData Settings = new SettingsSaveData();
    }

    [Serializable]
    public sealed class PermanentSaveData
    {
        public int SaveVersion = 1;
        public List<string> UnlockedTreeIds = new List<string>();
        public List<string> BossKillRecords = new List<string>();
        public bool TutorialCompleted;
        public bool DoubleJumpUnlocked;
        public int TotalDeaths;
    }

    [Serializable]
    public sealed class RunSaveData
    {
        public int RunNumber;
        public string CurrentStageId;
        public int Level = 1;
        public int Experience;
        public int ModulePoints;
        public List<string> QuestIds = new List<string>();
        public List<string> UnlockedModuleIds = new List<string>();
        public List<string> EquippedModuleIds = new List<string>();
        public List<EquippedModuleSlotSaveData> EquippedModuleSlots = new List<EquippedModuleSlotSaveData>();
        public List<string> ActivatedTowerIds = new List<string>();
        public List<string> CollectedItemIds = new List<string>();
        public string TutorialIntroStageId;
    }

    [Serializable]
    public sealed class EquippedModuleSlotSaveData
    {
        public string ModuleId;
        public int SlotIndex;
    }

    [Serializable]
    public sealed class SettingsSaveData
    {
        public float MasterVolume = 1f;
        public float MusicVolume = 1f;
        public float SfxVolume = 1f;
        public string InputBindingJson;
    }
}
