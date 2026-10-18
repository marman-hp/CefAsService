namespace Xilium.CefGlue
{
    using System;
    using Xilium.CefGlue.Interop;

    public sealed unsafe class CefScreenInfo
    {
        private cef_screen_info_t* _self;

        internal CefScreenInfo(cef_screen_info_t* self)
        {
            _self = self;
        }

        internal void Dispose()
        {
            _self = null;
        }

        public float DeviceScaleFactor
        {
            get { return _self->device_scale_factor; }
            set { _self->device_scale_factor = value; }
        }

        public int Depth
        {
            get { return _self->depth; }
            set { _self->depth = value; }
        }

        public int DepthPerComponent
        {
            get { return _self->depth_per_component; }
            set { _self->depth_per_component = value; }
        }

        public bool IsMonochrome
        {
            get { return _self->is_monochrome != 0; }
            set { _self->is_monochrome = value ? 1 : 0; }
        }

        public CefRectangle Rectangle
        {
            get
            {
                var n_rect = _self->rect;
                return new CefRectangle(n_rect.x, n_rect.y, n_rect.width, n_rect.height);
            }
            set
            {
                _self->rect = new cef_rect_t(value.X, value.Y, value.Width, value.Height);
            }
        }

        public CefRectangle AvailableRectangle
        {
            get
            {
                var n_rect = _self->available_rect;
                return new CefRectangle(n_rect.x, n_rect.y, n_rect.width, n_rect.height);
            }
            set
            {
                _self->available_rect = new cef_rect_t(value.X, value.Y, value.Width, value.Height);
            }
        }
    }
}
