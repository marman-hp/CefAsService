namespace Xilium.CefGlue
{
    using System;

    [Flags]
    public enum CefFileDialogMode
    {
        Open,

        OpenMultiple,

        OpenFolder,

        Save,
        NumValues,
    }
}
