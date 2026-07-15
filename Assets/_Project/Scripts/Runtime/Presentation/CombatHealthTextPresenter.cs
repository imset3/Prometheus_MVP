using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    public sealed class CombatHealthTextPresenter : MonoBehaviour
    {
        [SerializeField] private CombatActorHost actorHost;
        [SerializeField] private Text healthText;
        [SerializeField] private string label = "체력";
        [SerializeField] private string defeatedText = "처치됨";

        private void Awake()
        {
            if (actorHost == null || healthText == null)
            {
                Debug.LogError("CombatHealthTextPresenter requires pre-placed CombatActorHost and UI Text references.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (actorHost == null || healthText == null || actorHost.Runtime == null) return;
            healthText.text = actorHost.Runtime.IsAlive
                ? $"{label} {actorHost.Runtime.CurrentHealth}/{actorHost.Runtime.MaxHealth}"
                : defeatedText;
        }
    }
}
