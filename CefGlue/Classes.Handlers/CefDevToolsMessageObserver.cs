namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefDevToolsMessageObserver
    {
        private int on_dev_tools_message(cef_dev_tools_message_observer_t* self, cef_browser_t* browser, void* message, UIntPtr message_size)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            var m_result = OnDevToolsMessage(m_browser, (IntPtr)message, checked((int)message_size));

            return m_result ? 1 : 0;
        }

        protected abstract bool OnDevToolsMessage(CefBrowser browser, IntPtr message, int messageSize);

        private void on_dev_tools_method_result(cef_dev_tools_message_observer_t* self, cef_browser_t* browser, int message_id, int success, void* result, UIntPtr result_size)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            OnDevToolsMethodResult(m_browser, message_id, success != 0, (IntPtr)result, checked((int)result_size));
        }

        protected abstract void OnDevToolsMethodResult(CefBrowser browser, int messageId, bool success, IntPtr result, int resultSize);

        private void on_dev_tools_event(cef_dev_tools_message_observer_t* self, cef_browser_t* browser, cef_string_t* method, void* @params, UIntPtr params_size)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_method = cef_string_t.ToString(method);

            OnDevToolsEvent(m_browser, m_method, (IntPtr)@params, checked((int)params_size));
        }

        protected abstract void OnDevToolsEvent(CefBrowser browser, string method, IntPtr parameters, int parametersSize);

        private void on_dev_tools_agent_attached(cef_dev_tools_message_observer_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            OnDevToolsAgentAttached(m_browser);
        }

        protected abstract void OnDevToolsAgentAttached(CefBrowser browser);

        private void on_dev_tools_agent_detached(cef_dev_tools_message_observer_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            OnDevToolsAgentDetached(m_browser);
        }

        protected abstract void OnDevToolsAgentDetached(CefBrowser browser);
    }
}
