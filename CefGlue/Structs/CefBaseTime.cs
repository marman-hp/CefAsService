namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;

    using Xilium.CefGlue.Interop;

    [StructLayout(LayoutKind.Sequential, Pack = libcef.ALIGN)]
    public readonly struct CefBaseTime
    {
        private readonly long _microseconds;

        public CefBaseTime(long ticks)
        {
            _microseconds = ticks;
        }

        public long Ticks => _microseconds;

        public static CefBaseTime Now() => libcef.basetime_now();

        public unsafe bool UtcExplode(out CefTime exploded)
        {
            return libcef.time_from_basetime(this, out exploded) != 0;
        }

        public static unsafe bool FromUtcExploded(in CefTime exploded, out CefBaseTime time)
        {
            return libcef.time_to_basetime(in exploded, out time) != 0;
        }
    }
}
