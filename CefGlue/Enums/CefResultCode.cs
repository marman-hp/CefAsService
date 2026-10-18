namespace Xilium.CefGlue
{
    public enum CefResultCode
    {

        NormalExit,

        Killed,

        Hung,

        KilledBadMessage,

        GpuDeadOnArrival,

        ChromeFirst,

        MissingData = 7,

        UnsupportedParam = 13,

        ProfileInUse = 21,

        PackExtensionError = 22,

        NormalExitProcessNotified = 24,

        InvalidSandboxState = 31,

        CloudPolicyEnrollmentFailed = 32,

        GpuExitOnContextLost = 34,

        NormalExitPackExtensionSuccess = 36,

        SystemResourceExhausted = 37,

        ChromeLast = 38,

        SandboxFatalFirst = 7006,

        SandboxFatalIntegrity = SandboxFatalFirst,

        SandboxFatalDroptoken,

        SandboxFatalFlushhandles,

        SandboxFatalCachedisable,

        SandboxFatalClosehandles,

        SandboxFatalMitigation,

        SandboxFatalMemoryExceeded,

        SandboxFatalWarmup,

        SandboxFatalBrokerShutdownHung,

        SandboxFatalLast,

        NumValues,
    }
}