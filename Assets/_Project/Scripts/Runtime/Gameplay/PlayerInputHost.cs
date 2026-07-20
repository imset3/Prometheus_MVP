using UnityEngine;
using UnityEngine.InputSystem;

namespace Narthex.Gameplay
{
    public static class TutorialAimPolicy
    {
        public static float ResolveNonPointerAttackDirection(
            bool usesStickAim,
            float lookX,
            float movementX,
            float currentDirection)
        {
            if (usesStickAim && Mathf.Abs(lookX) >= 0.2f) return Mathf.Sign(lookX);
            if (Mathf.Abs(movementX) >= 0.2f) return Mathf.Sign(movementX);
            return Mathf.Approximately(currentDirection, 0f) ? 1f : Mathf.Sign(currentDirection);
        }
    }

    public sealed class PlayerInputHost : MonoBehaviour
    {
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private PlayerMotorHost motor;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string glideActionName = "Glide";
        [SerializeField] private string dashActionName = "Sprint";
        [SerializeField] private string attackActionName = "Attack";
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField] private string moduleActionName = "Next";
        [SerializeField] private string moduleTreeActionName = "OpenModuleTree";
        [SerializeField] private string inventoryActionName = "OpenInventory";

        public event System.Action AttackRequested;
        public event System.Action InteractRequested;
        public event System.Action ModuleRequested;
        public event System.Action ModuleTreeRequested;
        public event System.Action InventoryRequested;
        public event System.Action DialogueAdvanceRequested;
        public event System.Action<float> AimDirectionChanged;
        public event System.Action BindingDisplayChanged;
        public bool UsesCSharpEvents => playerInput != null && playerInput.notificationBehavior == PlayerNotifications.InvokeCSharpEvents;
        public bool HasAimCamera => aimCamera != null;
        public bool HasLookAction => playerInput != null && playerInput.actions != null &&
                                     playerInput.actions.FindAction(lookActionName, false) != null;
        public bool IsDialogueInputClaimed => dialogueInputClaimed;
        public float AimDirectionX { get; private set; } = 1f;
        private bool dialogueInputClaimed;
        private Vector2 latestMovementInput;
        private Vector2 latestLookInput;

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
            playerInput.onControlsChanged += HandleControlsChanged;
        }

        private void OnDisable()
        {
            if (playerInput == null) return;
            playerInput.onActionTriggered -= OnActionTriggered;
            playerInput.onControlsChanged -= HandleControlsChanged;
        }

        private void OnActionTriggered(InputAction.CallbackContext context)
        {
            if (motor == null) return;
            if (dialogueInputClaimed)
            {
                if (context.action.name == jumpActionName && context.performed)
                    DialogueAdvanceRequested?.Invoke();
                if (context.action.name == glideActionName)
                    motor.SetGlideHeld(false);
                return;
            }

            if (context.action.name == moveActionName)
            {
                latestMovementInput = context.ReadValue<Vector2>();
                motor.SetMovementInput(latestMovementInput);
            }
            else if (context.action.name == lookActionName)
            {
                latestLookInput = context.ReadValue<Vector2>();
                if (context.control != null && (context.control.device is Gamepad || context.control.device is Joystick))
                    UpdateAimDirection(latestLookInput.x);
            }
            else if (context.action.name == jumpActionName && context.performed) motor.RequestJump();
            else if (context.action.name == glideActionName)
                motor.SetGlideHeld(context.phase == InputActionPhase.Started || context.phase == InputActionPhase.Performed);
            else if (context.action.name == dashActionName && context.performed) motor.RequestDash();
            else if (context.action.name == attackActionName && context.performed)
            {
                if (context.control != null && context.control.device is Mouse)
                    UpdateAimDirectionFromPointer();
                else
                {
                    var usesStickAim = context.control != null &&
                                       (context.control.device is Gamepad || context.control.device is Joystick);
                    UpdateAimDirection(TutorialAimPolicy.ResolveNonPointerAttackDirection(
                        usesStickAim,
                        latestLookInput.x,
                        latestMovementInput.x,
                        AimDirectionX));
                }
                AttackRequested?.Invoke();
            }
            else if (context.action.name == interactActionName && context.performed)
                InteractRequested?.Invoke();
            else if (context.action.name == moduleActionName && context.performed) ModuleRequested?.Invoke();
            else if (context.action.name == moduleTreeActionName && context.performed) ModuleTreeRequested?.Invoke();
            else if (context.action.name == inventoryActionName && context.performed) InventoryRequested?.Invoke();
        }

        public void SetDialogueInputClaimed(bool claimed)
        {
            dialogueInputClaimed = claimed;
            if (claimed) motor?.SetGlideHeld(false);
        }

        public string GetBindingDisplayName(string actionName, string fallback)
        {
            if (playerInput == null || playerInput.actions == null) return fallback;
            var action = playerInput.actions.FindAction(actionName, false);
            if (action == null) return fallback;
            var display = action.GetBindingDisplayString(
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames,
                playerInput.currentControlScheme);
            return string.IsNullOrWhiteSpace(display) ? fallback : display.ToUpperInvariant();
        }

        private void UpdateAimDirectionFromPointer()
        {
            if (Mouse.current == null) return;
            var cameraToUse = aimCamera != null ? aimCamera : Camera.main;
            if (cameraToUse == null) return;

            var pointerWorld = cameraToUse.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            var nextDirection = pointerWorld.x < transform.position.x ? -1f : 1f;
            if (Mathf.Approximately(nextDirection, AimDirectionX)) return;
            AimDirectionX = nextDirection;
            AimDirectionChanged?.Invoke(AimDirectionX);
        }

        private void UpdateAimDirection(float horizontalInput)
        {
            if (Mathf.Abs(horizontalInput) < 0.2f) return;
            var nextDirection = horizontalInput < 0f ? -1f : 1f;
            if (Mathf.Approximately(nextDirection, AimDirectionX)) return;
            AimDirectionX = nextDirection;
            AimDirectionChanged?.Invoke(AimDirectionX);
        }

        private void HandleControlsChanged(PlayerInput input) => BindingDisplayChanged?.Invoke();
    }
}
