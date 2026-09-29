namespace Xilium.CefGlue
{
    public enum CefMediaRouteCreateResult
    {
        UnknownError,
        Ok,
        TimedOut,
        RouteNotFound,
        SinkNotFound,
        InvalidOrigin,
        OffTheRecordMismatchDeprecated,
        NoSupportedProvider,
        Cancelled,
        RouteAlreadyExists,
        DesktopPickerFailed,
        RouteAlreadyTerminated,
        RedundantRequest,
        UserNotAllowed,
        NotificationDisabled,
        NumValues,
    }
}
