using UnityEngine;

namespace Narthex.Content
{
    public enum StageType { Tutorial, Normal, Boss, Result }
    public enum RoomType { Tutorial, Traverse, Combat, BossArena }

    [CreateAssetMenu(menuName = "Narthex/Content/Stage Definition")]
    public sealed class StageDefinition : DefinitionBase
    {
        public StageType StageType;
        public string ChapterId;
        public string SceneAddress;
        public string DefaultSpawnPointId;
        public string DefaultCameraProfileId;
        public string[] RoomIds;
        public string[] PortalIds;
        public string[] TowerIds;
        public string BalanceProfileId;
    }

    [CreateAssetMenu(menuName = "Narthex/Content/Room Definition")]
    public sealed class RoomDefinition : DefinitionBase
    {
        public RoomType RoomType;
        public string CameraBoundsId;
        public string[] EntryGateIds;
        public string[] ExitPortalIds;
        public string[] EnemySpawnerIds;
        public string BossId;
        public string LockPolicy;
        public string[] ClearConditionIds;
    }

    [CreateAssetMenu(menuName = "Narthex/Content/Portal Definition")]
    public sealed class PortalDefinition : DefinitionBase
    {
        public string SourceStageId;
        public string TargetStageId;
        public string TargetSpawnPointId;
        public string RequiredQuestId;
    }

    [CreateAssetMenu(menuName = "Narthex/Content/Tower Definition")]
    public sealed class TowerDefinition : DefinitionBase
    {
        public string StageId;
        public string[] ActivationConditionIds;
        public string[] EffectIds;
        public bool SavesRunProgress;
        public bool IsRevivePoint;
    }

    [CreateAssetMenu(menuName = "Narthex/Content/Camera Bounds Definition")]
    public sealed class CameraBoundsDefinition : DefinitionBase
    {
        public Rect Bounds;
    }

    [CreateAssetMenu(menuName = "Narthex/Content/Balance Profile Definition")]
    public sealed class BalanceProfileDefinition : DefinitionBase
    {
        public float PlayerDamageMultiplier = 1f;
        public float EnemyHealthMultiplier = 1f;
        public float RewardMultiplier = 1f;
    }
}
