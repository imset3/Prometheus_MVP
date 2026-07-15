using UnityEngine;

namespace Narthex.Content
{
    public enum ActionType { Wait, SpawnHitbox, SpawnProjectile, Move, ApplyEffect, PlaySfx }
    public enum QuestSignalType { MovementPerformed, JumpPerformed, DashPerformed, AttackPerformed, ModuleUsed, ModuleTreeOpened, TowerActivated, BossKilled, PortalUsed }
    public enum RewardType { None, ModulePoint, TreeUnlock, BossModuleTreeUnlock, StageUnlock }
    public enum ModuleTreeType { Basic, Boss }

    [System.Serializable]
    public sealed class ModuleNodeDefinition
    {
        public ModuleDefinition Module;
        public string[] RequiredModuleIds = new string[0];
        public Vector2 UiPosition;
    }

    public enum ModuleCastPolicyData { DeusFront, PlayerCenter, TargetPosition, CustomAnchor }

}
