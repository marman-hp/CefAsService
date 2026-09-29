namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefTaskRunner
    {
        public static CefTaskRunner GetForCurrentThread()
        {
            return CefTaskRunner.FromNativeOrNull(cef_task_runner_t.get_for_current_thread());
        }

        public static CefTaskRunner GetForThread(CefThreadId threadId)
        {
            return CefTaskRunner.FromNativeOrNull(cef_task_runner_t.get_for_thread(threadId));
        }

        public bool IsSame(CefTaskRunner that)
        {
            return cef_task_runner_t.is_same(_self, that.ToNative()) != 0;
        }

        public bool BelongsToCurrentThread
        {
            get { return cef_task_runner_t.belongs_to_current_thread(_self) != 0; }
        }

        public bool BelongsToThread(CefThreadId threadId)
        {
            return cef_task_runner_t.belongs_to_thread(_self, threadId) != 0;
        }

        public bool PostTask(CefTask task)
        {
            return cef_task_runner_t.post_task(_self, task.ToNative()) != 0;
        }

        public bool PostDelayedTask(CefTask task, long delay)
        {
            return cef_task_runner_t.post_delayed_task(_self, task.ToNative(), delay) != 0;
        }
    }
}
