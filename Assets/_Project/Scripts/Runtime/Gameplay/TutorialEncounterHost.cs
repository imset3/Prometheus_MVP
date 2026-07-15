using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class TutorialEncounterHost : MonoBehaviour
    {
        [SerializeField] private CombatSystemHost combatSystemHost;
        [SerializeField] private CombatActorHost tutorialEnemy;
        [SerializeField] private GameObject goalMarker;
        [SerializeField] private bool showGoalAfterEnemyKilled;

        private void Awake()
        {
            if (combatSystemHost == null || tutorialEnemy == null || goalMarker == null)
            {
                Debug.LogError("TutorialEncounterHost requires pre-placed combat, enemy, and goal references.", this);
                enabled = false;
                return;
            }

            combatSystemHost.Initialize();
            goalMarker.SetActive(false);
        }

        private void OnEnable()
        {
            if (combatSystemHost != null) combatSystemHost.Events?.Subscribe<EnemyKilled>(HandleEnemyKilled);
        }

        private void OnDisable()
        {
            if (combatSystemHost != null) combatSystemHost.Events?.Unsubscribe<EnemyKilled>(HandleEnemyKilled);
        }

        private void HandleEnemyKilled(EnemyKilled message)
        {
            if (message.EnemyId != tutorialEnemy.ActorId) return;
            tutorialEnemy.gameObject.SetActive(false);
            if (showGoalAfterEnemyKilled) goalMarker.SetActive(true);
        }
    }
}
