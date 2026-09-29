namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefNavigationEntryVisitor
    {
        private int visit(cef_navigation_entry_visitor_t* self, cef_navigation_entry_t* entry, int current, int index, int total)
        {
            CheckSelf(self);
            using (var m_entry = CefNavigationEntry.FromNative(entry))
            {
                var m_result = Visit(m_entry, current != 0, index, total);
                return m_result ? 1 : 0;
            }
        }

        protected abstract bool Visit(CefNavigationEntry entry, bool current, int index, int total);
    }
}
