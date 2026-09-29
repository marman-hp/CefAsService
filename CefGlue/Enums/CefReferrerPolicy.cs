namespace Xilium.CefGlue
{
    public enum CefReferrerPolicy
    {
        ClearReferrerOnTransitionFromSecureToInsecure,

        Default = ClearReferrerOnTransitionFromSecureToInsecure,

        ReduceReferrerGranularityOnTransitionCrossOrigin,

        OriginOnlyOnTransitionCrossOrigin,

        NeverClearReferrer,

        Origin,

        ClearReferrerOnTransitionCrossOrigin,

        OriginClearOnTransitionFromSecureToInsecure,

        NoReferrer,

        NumValues,
    }
}
