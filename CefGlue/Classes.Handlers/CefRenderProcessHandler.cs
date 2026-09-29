namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefRenderProcessHandler
    {
        private void on_web_kit_initialized(cef_render_process_handler_t* self)
        {
            CheckSelf(self);

            OnWebKitInitialized();
        }

        protected virtual void OnWebKitInitialized()
        {
        }

        private void on_browser_created(cef_render_process_handler_t* self, cef_browser_t* browser, cef_dictionary_value_t* extra_info)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_extraInfo = CefDictionaryValue.FromNativeOrNull(extra_info);

            OnBrowserCreated(m_browser, m_extraInfo);
        }

        protected virtual void OnBrowserCreated(CefBrowser browser, CefDictionaryValue? extraInfo)
        {
        }

        private void on_browser_destroyed(cef_render_process_handler_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            OnBrowserDestroyed(m_browser);
        }

        protected virtual void OnBrowserDestroyed(CefBrowser browser)
        {
        }

        private cef_load_handler_t* get_load_handler(cef_render_process_handler_t* self)
        {
            CheckSelf(self);

            var result = GetLoadHandler();

            return result != null ? result.ToNative() : null;
        }

        protected virtual CefLoadHandler GetLoadHandler()
        {
            return null;
        }

        private void on_context_created(cef_render_process_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_v8_context_t* context)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_frame = CefFrame.FromNative(frame);
            var m_context = CefV8Context.FromNative(context);

            OnContextCreated(m_browser, m_frame, m_context);
        }

        protected virtual void OnContextCreated(CefBrowser browser, CefFrame frame, CefV8Context context)
        {
        }

        private void on_context_released(cef_render_process_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_v8_context_t* context)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_frame = CefFrame.FromNative(frame);
            var m_context = CefV8Context.FromNative(context);

            OnContextReleased(m_browser, m_frame, m_context);
        }

        protected virtual void OnContextReleased(CefBrowser browser, CefFrame frame, CefV8Context context)
        {
        }

        private void on_uncaught_exception(cef_render_process_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_v8_context_t* context, cef_v8_exception_t* exception, cef_v8_stack_trace_t* stackTrace)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);
            var mContext = CefV8Context.FromNative(context);
            var mException = CefV8Exception.FromNative(exception);
            var mStackTrace = CefV8StackTrace.FromNative(stackTrace);

            OnUncaughtException(mBrowser, mFrame, mContext, mException, mStackTrace);
        }

        protected virtual void OnUncaughtException(CefBrowser browser, CefFrame frame, CefV8Context context, CefV8Exception exception, CefV8StackTrace stackTrace)
        {
        }

        private void on_focused_node_changed(cef_render_process_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_domnode_t* node)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_frame = CefFrame.FromNative(frame);
            var m_node = CefDomNode.FromNativeOrNull(node);

            OnFocusedNodeChanged(m_browser, m_frame, m_node);

            if (m_node != null) m_node.Dispose();
        }

        protected virtual void OnFocusedNodeChanged(CefBrowser browser, CefFrame frame, CefDomNode node)
        {
        }

        private int on_process_message_received(cef_render_process_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, CefProcessId source_process, cef_process_message_t* message)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_frame = CefFrame.FromNative(frame);

            var m_message = CefProcessMessage.FromNative(message);

            var result = OnProcessMessageReceived(m_browser, m_frame, source_process, m_message);
            return result ? 1 : 0;
        }

        protected virtual bool OnProcessMessageReceived(CefBrowser browser, CefFrame frame, CefProcessId sourceProcess, CefProcessMessage message)
        {
            return false;
        }
    }
}
