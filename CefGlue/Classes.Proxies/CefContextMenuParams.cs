namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefContextMenuParams
    {
        public int X
        {
            get { return cef_context_menu_params_t.get_xcoord(_self); }
        }

        public int Y
        {
            get { return cef_context_menu_params_t.get_ycoord(_self); }
        }

        public CefContextMenuTypeFlags ContextMenuType
        {
            get { return cef_context_menu_params_t.get_type_flags(_self); }
        }

        public string LinkUrl
        {
            get
            {
                var n_result = cef_context_menu_params_t.get_link_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string UnfilteredLinkUrl
        {
            get
            {
                var n_result = cef_context_menu_params_t.get_unfiltered_link_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string SourceUrl
        {
            get
            {
                var n_result = cef_context_menu_params_t.get_source_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public bool HasImageContents
        {
            get { return cef_context_menu_params_t.has_image_contents(_self) != 0; }
        }

        public string TitleText
        {
            get
            {
                var n_result = cef_context_menu_params_t.get_title_text(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string PageUrl
        {
            get
            {
                var n_result = cef_context_menu_params_t.get_page_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string FrameUrl
        {
            get
            {
                var n_result = cef_context_menu_params_t.get_frame_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string FrameCharset
        {
            get
            {
                var n_result = cef_context_menu_params_t.get_frame_charset(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public CefContextMenuMediaType MediaType
        {
            get { return cef_context_menu_params_t.get_media_type(_self); }
        }

        public CefContextMenuMediaStateFlags MediaState
        {
            get { return cef_context_menu_params_t.get_media_state_flags(_self); }
        }

        public string SelectionText
        {
            get
            {
                var n_result = cef_context_menu_params_t.get_selection_text(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string GetMisspelledWord()
        {
            var n_result = cef_context_menu_params_t.get_misspelled_word(_self);
            return cef_string_userfree.ToString(n_result);
        }

        public string[] GetDictionarySuggestions()
        {
            var n_suggestions = libcef.string_list_alloc();
            cef_context_menu_params_t.get_dictionary_suggestions(_self, n_suggestions);
            var suggestions = cef_string_list.ToArray(n_suggestions);
            libcef.string_list_free(n_suggestions);
            return suggestions;
        }

        public bool IsEditable
        {
            get { return cef_context_menu_params_t.is_editable(_self) != 0; }
        }

        public bool IsSpellCheckEnabled
        {
            get { return cef_context_menu_params_t.is_spell_check_enabled(_self) != 0; }
        }

        public CefContextMenuEditStateFlags EditState
        {
            get { return cef_context_menu_params_t.get_edit_state_flags(_self); }
        }

        public bool IsCustomMenu
        {
            get { return cef_context_menu_params_t.is_custom_menu(_self) != 0; }
        }
    }
}
