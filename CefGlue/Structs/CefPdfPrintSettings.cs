namespace Xilium.CefGlue
{
    using System;
    using Xilium.CefGlue.Interop;

    public sealed class CefPdfPrintSettings
    {
        public bool Landscape { get; set; }

        public bool PrintBackground { get; set; }

        public double Scale { get; set; }

        public double PaperWidth { get; set; }
        public double PaperHeight { get; set; }

        public bool PreferCssPageSize { get; set; }

        public CefPdfPrintMarginType MarginType { get; set; }

        public double MarginTop { get; set; }
        public double MarginRight { get; set; }
        public double MarginBottom { get; set; }
        public double MarginLeft { get; set; }

        public string PageRanges { get; set; }

        public bool DisplayHeaderFooter { get; set; }

        public string HeaderTemplate { get; set; }

        public string FooterTemplate { get; set; }

        public bool GenerateTaggedPdf { get; set; }

        public bool GenerateDocumentOutline { get; set; }

        internal unsafe cef_pdf_print_settings_t* ToNative()
        {
            var ptr = cef_pdf_print_settings_t.Alloc();

            ptr->landscape = Landscape ? 1 : 0;
            ptr->print_background = PrintBackground ? 1 : 0;
            ptr->scale = Scale;
            ptr->paper_width = PaperWidth;
            ptr->paper_height = PaperHeight;
            ptr->prefer_css_page_size = PreferCssPageSize ? 1 : 0;
            ptr->margin_type = MarginType;
            ptr->margin_top = MarginTop;
            ptr->margin_right = MarginRight;
            ptr->margin_bottom = MarginBottom;
            ptr->margin_left = MarginLeft;
            cef_string_t.Copy(PageRanges, &ptr->page_ranges);
            ptr->display_header_footer = DisplayHeaderFooter ? 1 : 0;
            cef_string_t.Copy(HeaderTemplate, &ptr->header_template);
            cef_string_t.Copy(FooterTemplate, &ptr->footer_template);
            ptr->generate_tagged_pdf = GenerateTaggedPdf ? 1 : 0;
            ptr->generate_document_outline = GenerateDocumentOutline ? 1 : 0;

            return ptr;
        }
    }
}
