namespace Xilium.CefGlue
{
    public enum CefComponentState
    {
        New = 0,
        Checking = 1,
        CanUpdate = 2,
        Downloading = 3,
        Decompressing = 4,
        Patching = 5,
        Updating = 6,
        Updated = 7,
        UpToDate = 8,
        UpdateError = 9,
        Run = 10,
    }
}
