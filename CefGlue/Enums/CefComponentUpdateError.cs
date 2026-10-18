namespace Xilium.CefGlue
{
    public enum CefComponentUpdateError
    {
        None = 0,
        UpdateInProgress = 1,
        UpdateCanceled = 2,
        RetryLater = 3,
        ServiceError = 4,
        UpdateCheckError = 5,
        CrxNotFound = 6,
        InvalidArgument = 7,
        BadCrxDataCallback = 8,
    }
}
