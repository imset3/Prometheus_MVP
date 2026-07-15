using Narthex.Core;
using Narthex.Save;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class TutorialGoalHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private Collider2D goalTrigger;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private string goalId = "TUTORIAL-GOAL-001";

        private bool completed;

        private void Awake()
        {
            if (serviceRoot == null || saveSystemHost == null || playerInputHost == null || goalTrigger == null || playerCollider == null)
            {
                Debug.LogError("TutorialGoalHost requires pre-placed service, save, input, and collider references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            if (!saveSystemHost.Initialize()) enabled = false;
        }

        private void OnEnable()
        {
            if (playerInputHost != null) playerInputHost.InteractRequested += TryComplete;
        }

        private void OnDisable()
        {
            if (playerInputHost != null) playerInputHost.InteractRequested -= TryComplete;
        }

        private void TryComplete()
        {
            if (completed || !goalTrigger.Distance(playerCollider).isOverlapped) return;

            completed = true;
            saveSystemHost.System.Current.Permanent.TutorialCompleted = true;
            saveSystemHost.System.Save("TutorialCompleted");
            serviceRoot.Events.Publish(new TutorialCompleted(goalId));
        }
    }
}
