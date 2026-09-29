namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefDomDocument
    {
        public CefDomDocumentType DocumentType
        {
            get { return cef_domdocument_t.get_type(_self); }
        }

        public CefDomNode Root
        {
            get
            {
                return CefDomNode.FromNative(
                    cef_domdocument_t.get_document(_self)
                    );
            }
        }

        public CefDomNode Body
        {
            get
            {
                return CefDomNode.FromNative(
                    cef_domdocument_t.get_body(_self)
                    );
            }
        }

        public CefDomNode Head
        {
            get
            {
                return CefDomNode.FromNative(
                    cef_domdocument_t.get_head(_self)
                    );
            }
        }

        public string Title
        {
            get
            {
                var n_result = cef_domdocument_t.get_title(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public CefDomNode GetElementById(string id)
        {
            fixed (char* id_str = id)
            {
                var n_id = new cef_string_t(id_str, id.Length);
                return CefDomNode.FromNativeOrNull(
                    cef_domdocument_t.get_element_by_id(_self, &n_id)
                    );
            }
        }

        public CefDomNode FocusedNode
        {
            get
            {
                return CefDomNode.FromNativeOrNull(
                    cef_domdocument_t.get_focused_node(_self)
                    );
            }
        }

        public bool HasSelection
        {
            get { return cef_domdocument_t.has_selection(_self) != 0; }
        }

        public int SelectionStartOffset
        {
            get { return cef_domdocument_t.get_selection_start_offset(_self); }
        }

        public int GetSelectionEndOffset
        {
            get { return cef_domdocument_t.get_selection_end_offset(_self); }
        }

        public string SelectionAsMarkup
        {
            get
            {
                var n_result = cef_domdocument_t.get_selection_as_markup(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string GetSelectionAsText
        {
            get
            {
                var n_result = cef_domdocument_t.get_selection_as_text(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string BaseUrl
        {
            get
            {
                var n_result = cef_domdocument_t.get_base_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string GetCompleteUrl(string partialUrl)
        {
            fixed (char* partialUrl_str = partialUrl)
            {
                var n_partialUrl = new cef_string_t(partialUrl_str, partialUrl.Length);
                var n_result = cef_domdocument_t.get_complete_url(_self, &n_partialUrl);
                return cef_string_userfree.ToString(n_result);
            }
        }
    }
}
