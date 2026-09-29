using System.Drawing;

namespace Xilium.CefGlue
{
    using System;
    using Xilium.CefGlue.Interop;

    public sealed class CefKeyEvent
    {
        private CefKeyEventType _type;
        private CefEventFlags _modifiers;
        private int _windowsKeyCode;
        private int _nativeKeyCode;
        private bool _isSystemKey;
        private char _character;
        private char _unmodifiedCharacter;
        private bool _focusOnEditableField;

        public CefKeyEvent()
        {
        }

        public CefKeyEventType EventType
        {
            get { return _type; }
            set { _type = value; }
        }

        public CefEventFlags Modifiers
        {
            get { return _modifiers; }
            set { _modifiers = value; }
        }

        public int WindowsKeyCode
        {
            get { return _windowsKeyCode; }
            set { _windowsKeyCode = value; }
        }

        public int NativeKeyCode
        {
            get { return _nativeKeyCode; }
            set { _nativeKeyCode = value; }
        }

        public bool IsSystemKey
        {
            get { return _isSystemKey; }
            set { _isSystemKey = value; }
        }

        public char Character
        {
            get { return _character; }
            set { _character = value; }
        }

        public char UnmodifiedCharacter
        {
            get { return _unmodifiedCharacter; }
            set { _unmodifiedCharacter = value; }
        }

        public bool FocusOnEditableField
        {
            get { return _focusOnEditableField; }
            set { _focusOnEditableField = value; }
        }

        #region Interop

        internal static unsafe CefKeyEvent FromNative(cef_key_event_t* ptr)
        {
            if (ptr == null) throw new ArgumentNullException("ptr");

            return new CefKeyEvent
            {
                EventType = ptr->type,
                Modifiers = ptr->modifiers,
                WindowsKeyCode = ptr->windows_key_code,
                NativeKeyCode = ptr->native_key_code,
                IsSystemKey = ptr->is_system_key != 0,
                Character = (char)ptr->character,
                UnmodifiedCharacter = (char)ptr->unmodified_character,
                FocusOnEditableField = ptr->focus_on_editable_field != 0,
            };
        }

        internal unsafe void ToNative(cef_key_event_t* ptr)
        {
            if (ptr == null) throw new ArgumentNullException("ptr");

            ptr->size = (UIntPtr)cef_key_event_t.Size;
            ptr->type = EventType;
            ptr->modifiers = Modifiers;
            ptr->windows_key_code = WindowsKeyCode;
            ptr->native_key_code = NativeKeyCode;
            ptr->is_system_key = IsSystemKey ? 1 : 0;
            ptr->character = Character;
            ptr->unmodified_character = UnmodifiedCharacter;
            ptr->focus_on_editable_field = FocusOnEditableField ? 1 : 0;
        }

        #endregion
    }
}
