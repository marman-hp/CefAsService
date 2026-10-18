namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefDomVisitor
    {
        private void visit(cef_domvisitor_t* self, cef_domdocument_t* document)
        {
            CheckSelf(self);

            var m_document = CefDomDocument.FromNative(document);

            Visit(m_document);

            m_document.Dispose();
        }

        protected abstract void Visit(CefDomDocument document);
    }
}
