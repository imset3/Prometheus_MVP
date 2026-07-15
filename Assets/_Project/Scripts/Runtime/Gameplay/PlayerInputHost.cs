using UnityEngine;
using UnityEngine.InputSystem;

namespace Narthex.Gameplay
{
    public sealed class PlayerInputHost : MonoBehaviour
    {
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private PlayerMotorHost motor;
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string glideActionName = "Glide";
        [SerializeField] private string dashActionName = "Sprint";
        [SerializeField] private string attackActionName = "Attack";
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField] private string moduleActionName = "Next";
        [SerializeField] private string moduleTreeActionName = "OpenModuleTree";

        public event System.Action AttackRequested;
        public event System.Action InteractRequested;
        public event System.Action ModuleRequested;
        public event System.Action ModuleTreeRequested;
        public bool UsesCSharpEvents => playerInput != null && playerInput.notificationBehavior == PlayerNotifications.InvokeCSharpEvents;

        private void Awake()
        {
            if (playerInput == null || motor == null)
                Debug.LogError("PlayerInputHost requires pre-placed PlayerInput and PlayerMotorHost references.", this);
            else if (!UsesCSharpEvents)
            {
                Debug.LogWarning("PlayerInputHost switched PlayerInput to Invoke C# Events so action callbacks can reach gameplay.", this);
                playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            }
        }

        private void OnEnable()
        {
            if (playerInput == null) return;
            if (!UsesCSharpEvents) playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            if (!playerInput.inputIsActive) playerInput.ActivateInput();
            if (playerInput.currentActionMap == null && !string.IsNullOrWhiteSpace(playerInput.defaultActionMap))
                playerInput.SwitchCurrentActionMap(playerInput.defaultActionMap);
            playerInput.onActionTriggered += OnActionTriggered;
        }

        private void OnDisable()
        {
            if (playerInput != null) playerInput.onActionTriggered -= OnActionTriggered;
        }

        private void OnActionTriggered(InputAction.CallbackContext context)
        {
            if (motor == null) return;
            if (context.action.name == moveActionName) motor.SetMovementInput(context.ReadValue<Vector2>());
            else if (context.action.name == jumpActionName && context.performed) motor.RequestJump();
            else if (context.action.name == glideActionName)
                motor.SetGlideHeld(context.phase == InputActionPhase.Started || context.phase == InputActionPhase.Performed);
            else if (context.action.name == dashActionName && context.performed) motor.RequestDash();
            else if (context.action.name == attackActionName && context.performed) AttackRequested?.Invoke();
            else if (context.action.name == interactActionName && context.performed) InteractRequested?.Invoke();
            else if (context.action.name == moduleActionName && context.performed) ModuleRequested?.Invoke();
            else if (context.action.name == moduleTreeActionName && context.performed) ModuleTreeRequested?.Invoke();
        }
    }
}
