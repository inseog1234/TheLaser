using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Core
{
    public enum KeyBindingAction
    {
        MoveUp,
        MoveDown,
        MoveLeft,
        MoveRight,
        FireLaser,
        RotateClockwise,
        RotateCounterClockwise,
        Reset,
        Undo,
        Redo,
        Interact,
        Pause
    }

    public static class KeyBindingManager
    {
        private const string PrefPrefix = "TheLaser_KeyBinding_";

        private static readonly KeyBindingAction[] actions =
        {
            KeyBindingAction.MoveUp,
            KeyBindingAction.MoveDown,
            KeyBindingAction.MoveLeft,
            KeyBindingAction.MoveRight,
            KeyBindingAction.FireLaser,
            KeyBindingAction.Reset,
            KeyBindingAction.Undo,
            KeyBindingAction.Redo,
            KeyBindingAction.Interact,
            KeyBindingAction.Pause
        };

        public static KeyBindingAction[] Actions => actions;

        public static Key GetKey(KeyBindingAction action)
        {
            string keyName = PrefPrefix + action;
            Key key = (Key)PlayerPrefs.GetInt(keyName, (int)GetDefaultKey(action));
            if (action == KeyBindingAction.Interact && key == Key.Space)
            {
                key = Key.F;
                PlayerPrefs.SetInt(keyName, (int)key);
                PlayerPrefs.Save();
            }

            return key;
        }

        public static bool TrySetKey(KeyBindingAction action, Key key, out string message)
        {
            message = string.Empty;

            if (!IsValidKeyboardKey(key))
            {
                message = "키보드 키만 설정할 수 있습니다.";
                return false;
            }

            for (int i = 0; i < actions.Length; i++)
            {
                KeyBindingAction other = actions[i];
                if (other == action)
                    continue;

                if (GetKey(other) == key)
                {
                    message = $"{GetActionName(other)}에 이미 사용 중인 키입니다.";
                    return false;
                }
            }

            PlayerPrefs.SetInt(PrefPrefix + action, (int)key);
            PlayerPrefs.Save();
            message = $"{GetActionName(action)}: {GetKeyName(key)}";
            return true;
        }

        public static bool WasPressedThisFrame(KeyBindingAction action)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            Key key = GetKey(action);
            return IsValidKeyboardKey(key) && keyboard[key].wasPressedThisFrame;
        }

        public static bool IsPressed(KeyBindingAction action)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            Key key = GetKey(action);
            return IsValidKeyboardKey(key) && keyboard[key].isPressed;
        }

        public static bool TryReadPressedKeyThisFrame(out Key key)
        {
            key = Key.None;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            foreach (KeyControl keyControl in keyboard.allKeys)
            {
                if (keyControl == null || !keyControl.wasPressedThisFrame)
                    continue;

                Key candidate = keyControl.keyCode;
                if (!IsValidKeyboardKey(candidate))
                    continue;

                key = candidate;
                return true;
            }

            return false;
        }

        public static string GetActionName(KeyBindingAction action)
        {
            switch (action)
            {
                case KeyBindingAction.MoveUp: return "위 이동";
                case KeyBindingAction.MoveDown: return "아래 이동";
                case KeyBindingAction.MoveLeft: return "왼쪽 이동";
                case KeyBindingAction.MoveRight: return "오른쪽 이동";
                case KeyBindingAction.FireLaser: return "레이저 발사";
                case KeyBindingAction.RotateClockwise: return "시계 회전";
                case KeyBindingAction.RotateCounterClockwise: return "반시계 회전";
                case KeyBindingAction.Reset: return "리셋";
                case KeyBindingAction.Undo: return "되돌리기";
                case KeyBindingAction.Redo: return "다시실행";
                case KeyBindingAction.Interact: return "상호작용";
                case KeyBindingAction.Pause: return "일시정지";
                default: return action.ToString();
            }
        }

        public static string GetKeyName(Key key)
        {
            switch (key)
            {
                case Key.UpArrow: return "Up Arrow";
                case Key.DownArrow: return "Down Arrow";
                case Key.LeftArrow: return "Left Arrow";
                case Key.RightArrow: return "Right Arrow";
                case Key.Space: return "Space";
                case Key.Enter: return "Enter";
                case Key.Escape: return "Escape";
                case Key.Backspace: return "Backspace";
                case Key.Tab: return "Tab";
                case Key.LeftShift: return "Left Shift";
                case Key.RightShift: return "Right Shift";
                case Key.LeftCtrl: return "Left Ctrl";
                case Key.RightCtrl: return "Right Ctrl";
                case Key.LeftAlt: return "Left Alt";
                case Key.RightAlt: return "Right Alt";
                default: return key.ToString();
            }
        }

        private static Key GetDefaultKey(KeyBindingAction action)
        {
            switch (action)
            {
                case KeyBindingAction.MoveUp: return Key.UpArrow;
                case KeyBindingAction.MoveDown: return Key.DownArrow;
                case KeyBindingAction.MoveLeft: return Key.LeftArrow;
                case KeyBindingAction.MoveRight: return Key.RightArrow;
                case KeyBindingAction.FireLaser: return Key.X;
                case KeyBindingAction.RotateClockwise: return Key.C;
                case KeyBindingAction.RotateCounterClockwise: return Key.V;
                case KeyBindingAction.Reset: return Key.R;
                case KeyBindingAction.Undo: return Key.Z;
                case KeyBindingAction.Redo: return Key.Y;
                case KeyBindingAction.Interact: return Key.F;
                case KeyBindingAction.Pause: return Key.Escape;
                default: return Key.None;
            }
        }

        private static bool IsValidKeyboardKey(Key key)
        {
            return key != Key.None && Enum.IsDefined(typeof(Key), key);
        }
    }
}
