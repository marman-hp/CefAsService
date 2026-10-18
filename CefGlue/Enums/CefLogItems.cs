namespace Xilium.CefGlue
{
    using System;

    [Flags]
    public enum CefLogItems
    {
        Default = 0,
        None = 1,
        ProcessId = 1 << 1,
        ThreadId = 1 << 2,
        TimeStamp = 1 << 3,
        TickCount = 1 << 4,
    }
}
