namespace Xilium.CefGlue
{
    using System;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public readonly struct CefTouchHandleState
    {
        private readonly cef_touch_handle_state_t _value;

        internal unsafe CefTouchHandleState(cef_touch_handle_state_t* value)
        {
            _value = *value;
        }

        public int TouchHandleId => _value.touch_handle_id;

        public CefTouchHandleStateFlags Flags => _value.flags;

        public bool Enabled => _value.enabled != 0;

        public CefHorizontalAlignment Orientation => _value.orientation;
        public bool MirrorVertical => _value.mirror_vertical != 0;
        public bool MirrorHorizontal => _value.mirror_horizontal != 0;

        public CefPoint Origin => new CefPoint(_value.origin.x, _value.origin.y);

        public float Alpha => _value.alpha;
    }
}
