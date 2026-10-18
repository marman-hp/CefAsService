namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefTask
    {
        private void execute(cef_task_t* self)
        {
            CheckSelf(self);

            Execute();
        }

        protected abstract void Execute();
    }
}
