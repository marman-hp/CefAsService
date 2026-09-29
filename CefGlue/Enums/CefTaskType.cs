namespace Xilium.CefGlue
{
    using System;

    public enum CefTaskType
    {
        Unknown,
        Browser,
        Gpu,
        Zygote,
        Utility,
        Renderer,
        Extension,
        Guest,
        Plugin,
        SandboxHelper,
        DedicatedWorker,
        SharedWorker,
        ServiceWorker,
        NumValues,
    }
}
