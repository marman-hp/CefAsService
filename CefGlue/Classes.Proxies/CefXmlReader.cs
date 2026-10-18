namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefXmlReader
    {
        public static CefXmlReader Create(CefStreamReader stream, CefXmlEncoding encodingType, string uri)
        {
            if (stream == null) throw new ArgumentNullException("stream");

            fixed (char* uri_str = uri)
            {
                var n_uri = new cef_string_t(uri_str, uri != null ? uri.Length : 0);
                return CefXmlReader.FromNative(
                    cef_xml_reader_t.create(stream.ToNative(), encodingType, &n_uri)
                    );
            }
        }

        public bool MoveToNextNode()
        {
            return cef_xml_reader_t.move_to_next_node(_self) != 0;
        }

        public bool Close()
        {
            return cef_xml_reader_t.close(_self) != 0;
        }

        public bool HasError
        {
            get { return cef_xml_reader_t.has_error(_self) != 0; }
        }

        public string Error
        {
            get
            {
                var n_result = cef_xml_reader_t.get_error(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public CefXmlNodeType NodeType
        {
            get { return cef_xml_reader_t.get_type(_self); }
        }

        public int Depth
        {
            get { return cef_xml_reader_t.get_depth(_self); }
        }

        public string LocalName
        {
            get
            {
                var n_result = cef_xml_reader_t.get_local_name(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string Prefix
        {
            get
            {
                var n_result = cef_xml_reader_t.get_prefix(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string QualifiedName
        {
            get
            {
                var n_result = cef_xml_reader_t.get_qualified_name(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string NamespaceUri
        {
            get
            {
                var n_result = cef_xml_reader_t.get_namespace_uri(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string BaseUri
        {
            get
            {
                var n_result = cef_xml_reader_t.get_base_uri(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string XmlLang
        {
            get
            {
                var n_result = cef_xml_reader_t.get_xml_lang(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public bool IsEmptyElement
        {
            get { return cef_xml_reader_t.is_empty_element(_self) != 0; }
        }

        public bool HasValue
        {
            get { return cef_xml_reader_t.has_value(_self) != 0; }
        }

        public string Value
        {
            get
            {
                var n_result = cef_xml_reader_t.get_value(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public bool HasAttributes
        {
            get { return cef_xml_reader_t.has_attributes(_self) != 0; }
        }

        public int AttributeCount
        {
            get { return (int)cef_xml_reader_t.get_attribute_count(_self); }
        }

        public string GetAttribute(int index)
        {
            var n_result = cef_xml_reader_t.get_attribute_byindex(_self, index);
            return cef_string_userfree.ToString(n_result);
        }

        public string GetAttribute(string qualifiedName)
        {
            fixed (char* qualifiedName_str = qualifiedName)
            {
                var n_qualifiedName = new cef_string_t(qualifiedName_str, qualifiedName != null ? qualifiedName.Length : 0);
                var n_result = cef_xml_reader_t.get_attribute_byqname(_self, &n_qualifiedName);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string GetAttribute(string localName, string namespaceUri)
        {
            fixed (char* localName_str = localName)
            fixed (char* namespaceUri_str = namespaceUri)
            {
                var n_localName = new cef_string_t(localName_str, localName != null ? localName.Length : 0);
                var n_namespaceUri = new cef_string_t(namespaceUri_str, namespaceUri != null ? namespaceUri.Length : 0);

                var n_result = cef_xml_reader_t.get_attribute_bylname(_self, &n_localName, &n_namespaceUri);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string GetInnerXml()
        {
            var n_result = cef_xml_reader_t.get_inner_xml(_self);
            return cef_string_userfree.ToString(n_result);
        }

        public string GetOuterXml()
        {
            var n_result = cef_xml_reader_t.get_outer_xml(_self);
            return cef_string_userfree.ToString(n_result);
        }

        public int LineNumber
        {
            get { return cef_xml_reader_t.get_line_number(_self); }
        }

        public bool MoveToAttribute(int index)
        {
            return cef_xml_reader_t.move_to_attribute_byindex(_self, index) != 0;
        }

        public bool MoveToAttribute(string qualifiedName)
        {
            fixed (char* qualifiedName_str = qualifiedName)
            {
                var n_qualifiedName = new cef_string_t(qualifiedName_str, qualifiedName != null ? qualifiedName.Length : 0);

                return cef_xml_reader_t.move_to_attribute_byqname(_self, &n_qualifiedName) != 0;
            }
        }

        public bool MoveToAttribute(string localName, string namespaceUri)
        {
            fixed (char* localName_str = localName)
            fixed (char* namespaceUri_str = namespaceUri)
            {
                var n_localName = new cef_string_t(localName_str, localName != null ? localName.Length : 0);
                var n_namespaceUri = new cef_string_t(namespaceUri_str, namespaceUri != null ? namespaceUri.Length : 0);

                return cef_xml_reader_t.move_to_attribute_bylname(_self, &n_localName, &n_namespaceUri) != 0;
            }
        }

        public bool MoveToFirstAttribute()
        {
            return cef_xml_reader_t.move_to_first_attribute(_self) != 0;
        }

        public bool MoveToNextAttribute()
        {
            return cef_xml_reader_t.move_to_next_attribute(_self) != 0;
        }

        public bool MoveToCarryingElement()
        {
            return cef_xml_reader_t.move_to_carrying_element(_self) != 0;
        }
    }
}
