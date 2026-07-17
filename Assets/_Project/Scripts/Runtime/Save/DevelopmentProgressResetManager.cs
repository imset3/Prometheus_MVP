using UnityEngine;

namespace Narthex.Save
{
    /// <summary>
    /// Temporary scene-start reset for tutorial iteration. Disable or remove this
    /// pre-placed component when persistent progression becomes part of the game flow.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class DevelopmentProgressResetManager : MonoBehaviour
    {
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private bool resetProgressOnSceneStart = true;

        public bool HasValidSetup => saveSystemHost != null;
        public bool ResetProgressOnSceneStart => resetProgressOnSceneStart;

        private void Awake()
        {
            if (!resetProgressOnSceneStart) return;
            if (saveSystemHost == null || !saveSystemHost.Initialize())
            {
                Debug.LogError("DevelopmentProgressResetManager requires a pre-placed SaveSystemHost reference.", this);
                enabled = false;
                return;
            }

            saveSystemHost.System.ResetProgressForSceneStart();
        }
    }
}
