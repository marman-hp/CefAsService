namespace Xilium.CefGlue
{
    public enum CefXmlNodeType
    {
        Unsupported,
        ProcessingInstruction,
        DocumentType,
        ElementStart,
        ElementEnd,
        Attribute,
        Text,
        CData,
        EntityReference,
        WhiteSpace,
        Comment,
        NumValues,
    }
}
