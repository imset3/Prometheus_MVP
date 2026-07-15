using System;
using System.Collections.Generic;
using Narthex.Content;
using Narthex.Core;
using Narthex.Save;

namespace Narthex.Gameplay
{
    public sealed class ModuleTreeManager
    {
        private readonly GameEventBus events;
        private readonly ModuleSystem moduleSystem;
        private readonly PermanentSaveData permanentData;
        private readonly RunSaveData runData;
        private readonly Dictionary<string, ModuleTreeDefinition> trees = new Dictionary<string, ModuleTreeDefinition>();
        private readonly Dictionary<string, ModuleNodeDefinition> nodes = new Dictionary<string, ModuleNodeDefinition>();

        public int ModulePoints => runData.ModulePoints;

        public ModuleTreeManager(GameEventBus events, ModuleSystem moduleSystem, PermanentSaveData permanentData, RunSaveData runData)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.moduleSystem = moduleSystem ?? throw new ArgumentNullException(nameof(moduleSystem));
            this.permanentData = permanentData ?? throw new ArgumentNullException(nameof(permanentData));
            this.runData = runData ?? throw new ArgumentNullException(nameof(runData));
        }

        public void Register(ModuleTreeDefinition tree)
        {
            if (tree == null || string.IsNullOrWhiteSpace(tree.StableId)) throw new ArgumentException("Module tree must have a stableId.");
            if (trees.ContainsKey(tree.StableId)) return;

            trees.Add(tree.StableId, tree);
            foreach (var node in tree.Nodes)
            {
                if (node == null || node.Module == null || string.IsNullOrWhiteSpace(node.Module.StableId))
                    throw new ArgumentException("Every module tree node requires a module definition.");
                if (node.Module.TreeId != tree.StableId)
                    throw new ArgumentException("A module tree node must reference a module from the same tree.");
                if (nodes.ContainsKey(node.Module.StableId))
                    throw new ArgumentException("A module may only belong to one registered tree.");

                nodes.Add(node.Module.StableId, node);
                moduleSystem.Register(node.Module);
            }

            RestoreTreeState(tree);
        }

        public bool HasTreeAccess(string treeId)
        {
            return trees.TryGetValue(treeId, out var tree) &&
                   (tree.AvailableAtRunStart || permanentData.UnlockedTreeIds.Contains(treeId));
        }

        public bool GrantTreeAccess(string treeId)
        {
            if (!trees.ContainsKey(treeId) || permanentData.UnlockedTreeIds.Contains(treeId)) return false;
            permanentData.UnlockedTreeIds.Add(treeId);
            events.Publish(new ModuleTreeAccessGranted(treeId));
            events.Publish(new SaveRequested("ModuleTreeAccessGranted"));
            return true;
        }

        public bool TryUnlockModule(string moduleId)
        {
            if (!nodes.TryGetValue(moduleId, out var node) || node.Module == null) return false;
            if (!HasTreeAccess(node.Module.TreeId)) return false;
            if (runData.UnlockedModuleIds.Contains(moduleId)) return false;
            if (!HasRequiredModules(node.RequiredModuleIds)) return false;
            if (runData.ModulePoints < node.Module.UnlockCost) return false;

            if (!moduleSystem.Unlock(moduleId)) return false;
            runData.ModulePoints -= node.Module.UnlockCost;
            runData.UnlockedModuleIds.Add(moduleId);
            events.Publish(new SaveRequested("ModuleUnlocked"));
            return true;
        }

        public bool TryEquipModule(string moduleId, int slotIndex)
        {
            if (!moduleSystem.TryGetState(moduleId, out var state) || !state.Unlocked) return false;
            if (!runData.UnlockedModuleIds.Contains(moduleId)) runData.UnlockedModuleIds.Add(moduleId);
            if (!moduleSystem.Equip(moduleId, slotIndex)) return false;

            var displacedModuleIds = new List<string>();
            foreach (var entry in runData.EquippedModuleSlots)
            {
                if (entry.SlotIndex == slotIndex && entry.ModuleId != moduleId) displacedModuleIds.Add(entry.ModuleId);
            }

            foreach (var displacedModuleId in displacedModuleIds)
                runData.EquippedModuleIds.Remove(displacedModuleId);

            runData.EquippedModuleIds.Remove(moduleId);
            runData.EquippedModuleIds.Add(moduleId);
            runData.EquippedModuleSlots.RemoveAll(entry => entry.SlotIndex == slotIndex || entry.ModuleId == moduleId);
            runData.EquippedModuleSlots.Add(new EquippedModuleSlotSaveData { ModuleId = moduleId, SlotIndex = slotIndex });
            events.Publish(new SaveRequested("ModuleEquipped"));
            return true;
        }

        public void NotifyTreeOpened(string treeId)
        {
            if (!HasTreeAccess(treeId)) return;
            events.Publish(new ModuleTreeOpened(treeId));
            events.Publish(new GameplaySignal(QuestSignalType.ModuleTreeOpened, treeId));
        }

        public bool TryGetTree(string treeId, out ModuleTreeDefinition tree) => trees.TryGetValue(treeId, out tree);

        public bool TryGetModuleState(string moduleId, out ModuleRuntimeState state) => moduleSystem.TryGetState(moduleId, out state);

        public float GetModuleCooldownRemaining(string moduleId) => moduleSystem.GetCooldownRemaining(moduleId);

        private bool HasRequiredModules(string[] requiredModuleIds)
        {
            if (requiredModuleIds == null) return true;
            foreach (var requiredModuleId in requiredModuleIds)
            {
                if (!runData.UnlockedModuleIds.Contains(requiredModuleId)) return false;
            }

            return true;
        }

        private void RestoreTreeState(ModuleTreeDefinition tree)
        {
            foreach (var node in tree.Nodes)
            {
                if (!runData.UnlockedModuleIds.Contains(node.Module.StableId)) continue;
                moduleSystem.Unlock(node.Module.StableId);
            }

            foreach (var entry in runData.EquippedModuleSlots)
            {
                if (nodes.ContainsKey(entry.ModuleId)) moduleSystem.Equip(entry.ModuleId, entry.SlotIndex);
            }
        }
    }
}
