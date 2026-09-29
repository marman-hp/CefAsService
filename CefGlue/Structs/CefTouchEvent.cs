using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xilium.CefGlue.Interop;

namespace Xilium.CefGlue
{
    public class CefTouchEvent
    {
        public int Id { get; set; }

        public float X { get; set; }

        public float Y { get; set; }

        public float RadiusX { get; set; }

        public float RadiusY { get; set; }

        public float RotationAngle { get; set; }

        public float Pressure { get; set; }

        public CefTouchEventType Type { get; set; }

        public CefEventFlags Modifiers { get; set; }

        public CefPointerType PointerType { get; set; }

        internal void ToNative(out cef_touch_event_t value)
        {
            value = new cef_touch_event_t();
            value.id = Id;
            value.x = X;
            value.y = Y;
            value.radius_x = RadiusX;
            value.radius_y = RadiusY;
            value.rotation_angle = RotationAngle;
            value.pressure = Pressure;
            value.type = Type;
            value.modifiers = Modifiers;
            value.pointer_type = PointerType;
        }
    }
}
