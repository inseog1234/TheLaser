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

        [Header("Option")]
        [SerializeField] private bool inputEnabled = true;

        public event Action<GridDirection> MovePressed;
        public event Action LaserPressed;
        public event Action RotateClockwisePressed;
        public event Action RotateCounterClockwisePressed;
        public event Action ResetPressed;
        public event Action UndoPressed;
        public event Action RedoPressed;

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
    }
}