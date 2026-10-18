using System;

namespace Xilium.CefGlue
{
    using Xilium.CefGlue.Interop;

    public sealed class CefCompositionUnderline
    {
        public CefRange Range { get; set; }

        public CefColor Color { get; set; }

        public CefColor BackgroundColor;

        public bool Thick { get; set; }

        public CefCompositionUnderlineStyle Style { get; set; }

        internal cef_composition_underline_t ToNative()
        {
            var result = new cef_composition_underline_t { size = (UIntPtr)cef_composition_underline_t.Size };
            result.range = new cef_range_t(Range.From, Range.To);
            result.color = Color.ToArgb();
            result.background_color = BackgroundColor.ToArgb();
            result.thick = Thick ? 1 : 0;
            result.style = Style;
            return result;
        }
    }
}
