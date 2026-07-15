using UnityEngine;

namespace Narthex.Content
{
    public abstract class DefinitionBase : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayNameTextKey;

        public string StableId => stableId;
        public string DisplayNameTextKey => displayNameTextKey;
    

public void ConfigureIdentity(string id, string textKey = null)
        {
            stableId = id;
            displayNameTextKey = textKey;
        }
}
}
