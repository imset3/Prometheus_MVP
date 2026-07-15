using System.Collections.Generic;
using UnityEngine;

namespace Narthex.Content
{
    [CreateAssetMenu(menuName = "Narthex/Content/Ability Definition")]
    public sealed class AbilityDefinition : DefinitionBase
    {
        public List<ActionDefinition> Actions = new List<ActionDefinition>();
        public float Cooldown;
    }
}
