namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefDomNode
    {
        public CefDomNodeType NodeType
        {
            get { return cef_domnode_t.get_type(_self); }
        }

        public bool IsText
        {
            get { return cef_domnode_t.is_text(_self) != 0; }
        }

        public bool IsElement
        {
            get { return cef_domnode_t.is_element(_self) != 0; }
        }

        public bool IsEditable
        {
            get { return cef_domnode_t.is_editable(_self) != 0; }
        }

        public bool IsFormControlElement
        {
            get { return cef_domnode_t.is_form_control_element(_self) != 0; }
        }

        public CefDomFormControlType FormControlElementType
        {
            get { return cef_domnode_t.get_form_control_element_type(_self); }
        }

        public bool IsSame(CefDomNode that)
        {
            return cef_domnode_t.is_same(_self, that.ToNative()) != 0;
        }

        public string Name
        {
            get
            {
                var n_result = cef_domnode_t.get_name(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string Value
        {
            get
            {
                var n_result = cef_domnode_t.get_value(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public bool SetValue(string value)
        {
            fixed (char* value_str = value)
            {
                var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);

                return cef_domnode_t.set_value(_self, &n_value) != 0;
            }
        }

        public string GetAsMarkup()
        {
            var n_result = cef_domnode_t.get_as_markup(_self);
            return cef_string_userfree.ToString(n_result);
        }

        public CefDomDocument Document
        {
            get
            {
                return CefDomDocument.FromNative(
                    cef_domnode_t.get_document(_self)
                    );
            }
        }

        public CefDomNode Parent
        {
            get
            {
                return CefDomNode.FromNativeOrNull(
                    cef_domnode_t.get_parent(_self)
                    );
            }
        }

        public CefDomNode PreviousSibling
        {
            get
            {
                return CefDomNode.FromNativeOrNull(
                    cef_domnode_t.get_previous_sibling(_self)
                    );
            }
        }

        public CefDomNode NextSibling
        {
            get
            {
                return CefDomNode.FromNativeOrNull(
                    cef_domnode_t.get_next_sibling(_self)
                    );
            }
        }

        public bool HasChildren
        {
            get { return cef_domnode_t.has_children(_self) != 0; }
        }

        public CefDomNode FirstChild
        {
            get
            {
                return CefDomNode.FromNativeOrNull(
                    cef_domnode_t.get_first_child(_self)
                    );
            }
        }

        public CefDomNode LastChild
        {
            get
            {
                return CefDomNode.FromNativeOrNull(
                    cef_domnode_t.get_last_child(_self)
                    );
            }
        }

        public string ElementTagName
        {
            get
            {
                var n_result = cef_domnode_t.get_element_tag_name(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public bool HasAttributes
        {
            get { return cef_domnode_t.has_element_attributes(_self) != 0; }
        }

        public bool HasAttribute(string attrName)
        {
            fixed (char* attrName_str = attrName)
            {
                var n_attrName = new cef_string_t(attrName_str, attrName.Length);
                return cef_domnode_t.has_element_attribute(_self, &n_attrName) != 0;
            }
        }

        public string GetAttribute(string attrName)
        {
            fixed (char* attrName_str = attrName)
            {
                var n_attrName = new cef_string_t(attrName_str, attrName.Length);
                var n_result = cef_domnode_t.get_element_attribute(_self, &n_attrName);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public IDictionary<string, string> GetAttributes()
        {
            var attrMap = libcef.string_map_alloc();
            cef_domnode_t.get_element_attributes(_self, attrMap);
            var result = cef_string_map.ToDictionary(attrMap);
            libcef.string_map_free(attrMap);
            return result;
        }

        public bool SetAttribute(string attrName, string value)
        {
            fixed (char* attrName_str = attrName)
            fixed (char* value_str = value)
            {
                var n_attrName = new cef_string_t(attrName_str, attrName.Length);
                var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);
                return cef_domnode_t.set_element_attribute(_self, &n_attrName, &n_value) != 0;
            }
        }

        public string InnerText
        {
            get
            {
                var n_result = cef_domnode_t.get_element_inner_text(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public CefRectangle GetElementBounds()
        {
            var n_result = cef_domnode_t.get_element_bounds(_self);
            return new CefRectangle(
                n_result.x,
                n_result.y,
                n_result.width,
                n_result.height
                );
        }
    }
}
