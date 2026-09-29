namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefKeyboardHandler
    {
        private int on_pre_key_event(cef_keyboard_handler_t* self, cef_browser_t* browser, cef_key_event_t* @event, IntPtr os_event, int* is_keyboard_shortcut)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_event = CefKeyEvent.FromNative(@event);
            IntPtr m_os_event = IntPtr.Zero;
            if (os_event != IntPtr.Zero)
            {
            }

            var m_is_keyboard_shortcut = *is_keyboard_shortcut != 0;

            var result = OnPreKeyEvent(m_browser, m_event, m_os_event, out m_is_keyboard_shortcut);
            *is_keyboard_shortcut = m_is_keyboard_shortcut ? 1 : 0;

            return result ? 1 : 0;
        }

        protected virtual bool OnPreKeyEvent(CefBrowser browser, CefKeyEvent keyEvent, IntPtr os_event, out bool isKeyboardShortcut)
        {
            isKeyboardShortcut = false;
            return false;
        }

        private int on_key_event(cef_keyboard_handler_t* self, cef_browser_t* browser, cef_key_event_t* @event, IntPtr os_event)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_event = CefKeyEvent.FromNative(@event);
            IntPtr m_os_event = IntPtr.Zero;
            if (os_event != IntPtr.Zero)
            {
            }

            var result = OnKeyEvent(m_browser, m_event, m_os_event);

            return result ? 1 : 0;
        }

        protected virtual bool OnKeyEvent(CefBrowser browser, CefKeyEvent keyEvent, IntPtr osEvent)
        {
            return false;
        }
    }
}
