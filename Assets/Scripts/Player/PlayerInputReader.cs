using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Core;

namespace Player
{
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference laserAction;
        [SerializeField] private InputActionReference rotateClockwiseAction;
        [SerializeField] private InputActionReference rotateCounterClockwiseAction;
        [SerializeField] private InputActionReference resetAction;
        [SerializeField] private InputActionReference undoAction;
        [SerializeField] private InputActionReference redoAction;
        [SerializeField] private InputActionReference interactAction;
        [SerializeField] private InputActionReference pauseAction;

        [Header("Option")]
        [SerializeField] private bool inputEnabled = true;
        [SerializeField] private float heldMoveInitialDelay = 0.18f;
        [SerializeField] private float heldMoveRepeatInterval = 0.11f;

        private GridDirection heldMoveDirection;
        private bool hasHeldMoveDirection;
        private float nextHeldMoveTime;

        public event Action<GridDirection> MovePressed;
        public event Action LaserPressed;
        public event Action RotateClockwisePressed;
        public event Action RotateCounterClockwisePressed;
        public event Action ResetPressed;
        public event Action UndoPressed;
        public event Action RedoPressed;
        public event Action InteractPressed;
        public event Action PausePressed;

        public bool InputEnabled
        {
            get => inputEnabled;
            set => inputEnabled = value;
        }

        private void OnEnable()
        {
            SubscribeInputActions();
            SetInputActionsEnabled(true);
        }

        private void OnDisable()
        {
            SetInputActionsEnabled(false);
            UnsubscribeInputActions();
        }

        private void Update()
        {
            if (!inputEnabled)
            {
                hasHeldMoveDirection = false;
                return;
            }

            HandleHeldMoveInput();
        }

        private void HandleHeldMoveInput()
        {
            if (moveAction == null || moveAction.action == null)
                return;

            Vector2 input = moveAction.action.ReadValue<Vector2>();
            if (input == Vector2.zero)
            {
                hasHeldMoveDirection = false;
                return;
            }

            GridDirection direction = ConvertVectorToGridDirection(input);
            if (!hasHeldMoveDirection || direction != heldMoveDirection)
            {
                heldMoveDirection = direction;
                hasHeldMoveDirection = true;
                nextHeldMoveTime = Time.unscaledTime + Mathf.Max(0f, heldMoveInitialDelay);
                return;
            }

            if (Time.unscaledTime < nextHeldMoveTime)
                return;

            nextHeldMoveTime = Time.unscaledTime + Mathf.Max(0.02f, heldMoveRepeatInterval);
            MovePressed?.Invoke(direction);
        }

        private void SubscribeInputActions()
        {
            if (moveAction != null)
                moveAction.action.performed += OnMovePerformed;

            if (laserAction != null)
                laserAction.action.performed += OnLaserPerformed;

            if (rotateClockwiseAction != null)
                rotateClockwiseAction.action.performed += OnRotateClockwisePerformed;

            if (rotateCounterClockwiseAction != null)
                rotateCounterClockwiseAction.action.performed += OnRotateCounterClockwisePerformed;

            if (resetAction != null)
                resetAction.action.performed += OnResetPerformed;

            if (undoAction != null)
                undoAction.action.performed += OnUndoPerformed;

            if (redoAction != null)
                redoAction.action.performed += OnRedoPerformed;

            if (interactAction != null)
                interactAction.action.performed += OnInteractPerformed;

            if (pauseAction != null)
                pauseAction.action.performed += OnPausePerformed;
        }

        private void UnsubscribeInputActions()
        {
            if (moveAction != null)
                moveAction.action.performed -= OnMovePerformed;

            if (laserAction != null)
                laserAction.action.performed -= OnLaserPerformed;

            if (rotateClockwiseAction != null)
                rotateClockwiseAction.action.performed -= OnRotateClockwisePerformed;

            if (rotateCounterClockwiseAction != null)
                rotateCounterClockwiseAction.action.performed -= OnRotateCounterClockwisePerformed;

            if (resetAction != null)
                resetAction.action.performed -= OnResetPerformed;

            if (undoAction != null)
                undoAction.action.performed -= OnUndoPerformed;

            if (redoAction != null)
                redoAction.action.performed -= OnRedoPerformed;

            if (interactAction != null)
                interactAction.action.performed -= OnInteractPerformed;

            if (pauseAction != null)
                pauseAction.action.performed -= OnPausePerformed;
        }

        private void SetInputActionsEnabled(bool enabled)
        {
            SetActionEnabled(moveAction, enabled);
            SetActionEnabled(laserAction, enabled);
            SetActionEnabled(rotateClockwiseAction, enabled);
            SetActionEnabled(rotateCounterClockwiseAction, enabled);
            SetActionEnabled(resetAction, enabled);
            SetActionEnabled(undoAction, enabled);
            SetActionEnabled(redoAction, enabled);
            SetActionEnabled(interactAction, enabled);
            SetActionEnabled(pauseAction, enabled);
        }

        private void SetActionEnabled(InputActionReference actionReference, bool enabled)
        {
            if (actionReference == null || actionReference.action == null)
                return;

            if (enabled)
                actionReference.action.Enable();
            else
                actionReference.action.Disable();
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            if (!inputEnabled)
                return;

            Vector2 input = context.ReadValue<Vector2>();

            if (input == Vector2.zero)
                return;

            GridDirection direction = ConvertVectorToGridDirection(input);
            heldMoveDirection = direction;
            hasHeldMoveDirection = true;
            nextHeldMoveTime = Time.unscaledTime + Mathf.Max(0f, heldMoveInitialDelay);
            MovePressed?.Invoke(direction);
        }

        private GridDirection ConvertVectorToGridDirection(Vector2 input)
        {
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
                return input.x > 0f ? GridDirection.Right : GridDirection.Left;

            return input.y > 0f ? GridDirection.Up : GridDirection.Down;
        }

        private void OnLaserPerformed(InputAction.CallbackContext context)
        {
            if (!inputEnabled)
                return;

            LaserPressed?.Invoke();
        }

        private void OnRotateClockwisePerformed(InputAction.CallbackContext context)
        {
            if (!inputEnabled)
                return;

            RotateClockwisePressed?.Invoke();
        }

        private void OnRotateCounterClockwisePerformed(InputAction.CallbackContext context)
        {
            if (!inputEnabled)
                return;

            RotateCounterClockwisePressed?.Invoke();
        }

        private void OnResetPerformed(InputAction.CallbackContext context)
        {
            if (!inputEnabled)
                return;

            ResetPressed?.Invoke();
        }

        private void OnUndoPerformed(InputAction.CallbackContext context)
        {
            if (!inputEnabled)
                return;

            UndoPressed?.Invoke();
        }

        private void OnRedoPerformed(InputAction.CallbackContext context)
        {
            if (!inputEnabled)
                return;

            RedoPressed?.Invoke();
        }


        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            if (!inputEnabled)
                return;

            InteractPressed?.Invoke();
        }

        private void OnPausePerformed(InputAction.CallbackContext context)
        {
            if (!inputEnabled)
                return;

            PausePressed?.Invoke();
        }
    }
}
