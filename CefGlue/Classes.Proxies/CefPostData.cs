namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefPostData
    {
        public static CefPostData Create()
        {
            return CefPostData.FromNative(
                cef_post_data_t.create()
                );
        }

        public bool IsReadOnly
        {
            get { return cef_post_data_t.is_read_only(_self) != 0; }
        }

        public bool HasExcludedElements
        {
            get { return cef_post_data_t.has_excluded_elements(_self) != 0; }
        }

        public int Count
        {
            get { return (int)cef_post_data_t.get_element_count(_self); }
        }

        public CefPostDataElement[] GetElements()
        {
            var count = Count;
            if (count == 0) return new CefPostDataElement[0];

            UIntPtr n_elementsCount = (UIntPtr)count;
            var n_elements = new cef_post_data_element_t*[count];
            fixed (cef_post_data_element_t** n_elements_ptr = n_elements)
            {
                cef_post_data_t.get_elements(_self, &n_elementsCount, n_elements_ptr);
                if ((int)n_elementsCount > count) throw new InvalidOperationException();
            }

            count = (int)n_elementsCount;
            var elements = new CefPostDataElement[count];
            for (var i = 0; i < count; i++)
            {
                elements[i] = CefPostDataElement.FromNative(n_elements[i]);
            }

            return elements;
        }

        public bool Remove(CefPostDataElement element)
        {
            return cef_post_data_t.remove_element(_self, element.ToNative()) != 0;
        }

        public bool Add(CefPostDataElement element)
        {
            return cef_post_data_t.add_element(_self, element.ToNative()) != 0;
        }

        public void RemoveAll()
        {
            cef_post_data_t.remove_elements(_self);
        }
    }
}
