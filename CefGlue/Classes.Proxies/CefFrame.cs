namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefFrame
    {
        public bool IsValid
        {
            get { return cef_frame_t.is_valid(_self) != 0; }
        }

        public void Undo()
        {
            cef_frame_t.undo(_self);
        }

        public void Redo()
        {
            cef_frame_t.redo(_self);
        }

        public void Cut()
        {
            cef_frame_t.cut(_self);
        }

        public void Copy()
        {
            cef_frame_t.copy(_self);
        }

        public void Paste()
        {
            cef_frame_t.paste(_self);
        }

        public void PasteAndMatchStyle()
        {
            cef_frame_t.paste_and_match_style(_self);
        }

        public void Delete()
        {
            cef_frame_t.del(_self);
        }

        public void SelectAll()
        {
            cef_frame_t.select_all(_self);
        }

        public void ViewSource()
        {
            cef_frame_t.view_source(_self);
        }

        public void GetSource(CefStringVisitor visitor)
        {
            if (visitor == null) throw new ArgumentNullException("visitor");

            cef_frame_t.get_source(_self, visitor.ToNative());
        }

        public void GetText(CefStringVisitor visitor)
        {
            if (visitor == null) throw new ArgumentNullException("visitor");

            cef_frame_t.get_text(_self, visitor.ToNative());
        }

        public void LoadRequest(CefRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");

            cef_frame_t.load_request(_self, request.ToNative());
        }

        public void LoadUrl(string url)
        {
            fixed (char* url_str = url)
            {
                var n_url = new cef_string_t(url_str, url != null ? url.Length : 0);
                cef_frame_t.load_url(_self, &n_url);
            }
        }

        public void ExecuteJavaScript(string code, string url, int line)
        {
            fixed (char* code_str = code)
            fixed (char* url_str = url)
            {
                var n_code = new cef_string_t(code_str, code != null ? code.Length : 0);
                var n_url = new cef_string_t(url_str, url != null ? url.Length : 0);
                cef_frame_t.execute_java_script(_self, &n_code, &n_url, line);
            }
        }

        public bool IsMain
        {
            get { return cef_frame_t.is_main(_self) != 0; }
        }

        public bool IsFocused
        {
            get { return cef_frame_t.is_focused(_self) != 0; }
        }

        public string Name
        {
            get
            {
                var n_result = cef_frame_t.get_name(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string Identifier => cef_string_userfree.ToString(cef_frame_t.get_identifier(_self));

        public CefFrame Parent
        {
            get
            {
                return CefFrame.FromNativeOrNull(
                    cef_frame_t.get_parent(_self)
                    );
            }
        }

        public string Url
        {
            get
            {
                var n_result = cef_frame_t.get_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public CefBrowser Browser
        {
            get
            {
                return CefBrowser.FromNative(
                    cef_frame_t.get_browser(_self)
                    );
            }
        }

        public CefV8Context V8Context
        {
            get
            {
                return CefV8Context.FromNative(
                    cef_frame_t.get_v8_context(_self)
                    );
            }
        }

        public void VisitDom(CefDomVisitor visitor)
        {
            if (visitor == null) throw new ArgumentNullException("visitor");

            cef_frame_t.visit_dom(_self, visitor.ToNative());
        }

        public CefUrlRequest CreateUrlRequest(CefRequest request, CefUrlRequestClient client = null)
        {
            var n_request = request.ToNative();
            var n_client = client != null ? client.ToNative() : null;

            var n_result = cef_frame_t.create_urlrequest(_self, n_request, n_client);

            return CefUrlRequest.FromNativeOrNull(n_result);
        }

        public void SendProcessMessage(CefProcessId targetProcess, CefProcessMessage message)
        {
            if (message == null) throw new ArgumentNullException("message");

            cef_frame_t.send_process_message(_self, targetProcess, message.ToNative());
        }
    }
}
