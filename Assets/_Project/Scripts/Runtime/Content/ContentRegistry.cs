using System.Collections.Generic;
using UnityEngine;

namespace Narthex.Content
{
    [CreateAssetMenu(menuName = "Narthex/Content/Content Registry")]
    public sealed class ContentRegistry : ScriptableObject
    {
        [SerializeField] private DefinitionBase[] definitions;

        private Dictionary<string, DefinitionBase> lookup;

        public IReadOnlyList<DefinitionBase> Definitions => definitions;

        public void BuildLookup()
        {
            lookup = new Dictionary<string, DefinitionBase>();
            if (definitions == null) return;

            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.StableId)) continue;
                if (lookup.ContainsKey(definition.StableId))
                    throw new ContentRegistryException($"Duplicate stableId: {definition.StableId}");
                lookup.Add(definition.StableId, definition);
            }
        }

        public bool TryGet<T>(string stableId, out T definition) where T : DefinitionBase
        {
            if (lookup == null) BuildLookup();
            if (lookup.TryGetValue(stableId, out var value) && value is T typed)
            {
                definition = typed;
                return true;
            }

            definition = null;
            return false;
        }
    }

    public sealed class ContentRegistryException : System.Exception
    {
        public ContentRegistryException(string message) : base(message) { }
    }
}
