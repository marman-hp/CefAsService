namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefResourceBundleHandler
    {
        private int get_localized_string(cef_resource_bundle_handler_t* self, int string_id, cef_string_t* @string)
        {
            CheckSelf(self);

            string mValue;
            var result = GetLocalizedString(string_id, out mValue);

            if (result)
            {
                cef_string_t.Copy(mValue, @string);
                return 1;
            }
            else return 0;
        }

        protected virtual bool GetLocalizedString(int stringId, out string value)
        {
            value = null;
            return false;
        }

        private int get_data_resource(cef_resource_bundle_handler_t* self, int resource_id, void** data, UIntPtr* data_size)
        {
            CheckSelf(self);

            void* mData;
            UIntPtr mDataSize;
            var result = GetDataResource(resource_id, out mData, out mDataSize);

            if (result)
            {
                *data = mData;
                *data_size = mDataSize;
                return 1;
            }
            else return 0;
        }

        protected virtual bool GetDataResource(int resourceId, out void* data, out UIntPtr dataSize)
        {
            data = null;
            dataSize = UIntPtr.Zero;
            return false;
        }

        private int get_data_resource_for_scale(cef_resource_bundle_handler_t* self, int resource_id, CefScaleFactor scale_factor, void** data, UIntPtr* data_size)
        {
            CheckSelf(self);

            void* mData;
            UIntPtr mDataSize;
            var result = GetDataResourceForScale(resource_id, scale_factor, out mData, out mDataSize);

            if (result)
            {
                *data = mData;
                *data_size = mDataSize;
                return 1;
            }
            else return 0;
        }

        protected virtual bool GetDataResourceForScale(int resourceId, CefScaleFactor scaleFactor, out void* data, out UIntPtr dataSize)
        {
            data = null;
            dataSize = UIntPtr.Zero;
            return false;
        }
    }
}
