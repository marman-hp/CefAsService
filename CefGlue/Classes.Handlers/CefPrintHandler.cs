namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefPrintHandler
    {
        private void on_print_start(cef_print_handler_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            OnPrintStart(mBrowser);
        }

        protected virtual void OnPrintStart(CefBrowser browser)
        {
        }

        private void on_print_settings(cef_print_handler_t* self, cef_browser_t* browser, cef_print_settings_t* settings, int get_defaults)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            using (var m_settings = CefPrintSettings.FromNative(settings))
            {
                OnPrintSettings(mBrowser, m_settings, get_defaults != 0);
            }

        }

        protected abstract void OnPrintSettings(CefBrowser browser, CefPrintSettings settings, bool getDefaults);

        private int on_print_dialog(cef_print_handler_t* self, cef_browser_t* browser, int has_selection, cef_print_dialog_callback_t* callback)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var m_callback = CefPrintDialogCallback.FromNative(callback);
            var m_result = OnPrintDialog(mBrowser, has_selection != 0, m_callback);
            return m_result ? 1 : 0;
        }

        protected abstract bool OnPrintDialog(CefBrowser browser, bool hasSelection, CefPrintDialogCallback callback);

        private int on_print_job(cef_print_handler_t* self, cef_browser_t* browser, cef_string_t* document_name, cef_string_t* pdf_file_path, cef_print_job_callback_t* callback)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var m_documentName = cef_string_t.ToString(document_name);
            var m_pdfFilePath = cef_string_t.ToString(pdf_file_path);
            var m_callback = CefPrintJobCallback.FromNative(callback);

            var m_result = OnPrintJob(mBrowser, m_documentName, m_pdfFilePath, m_callback);

            return m_result ? 1 : 0;
        }

        protected abstract bool OnPrintJob(CefBrowser browser, string documentName, string pdfFilePath, CefPrintJobCallback callback);

        private void on_print_reset(cef_print_handler_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            OnPrintReset(mBrowser);
        }

        protected abstract void OnPrintReset(CefBrowser browser);

        private cef_size_t get_pdf_paper_size(cef_print_handler_t* self, cef_browser_t* browser, int device_units_per_inch)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNativeOrNull(browser);

            var m_result = GetPdfPaperSize(mBrowser, device_units_per_inch);

            var n_result = new cef_size_t
            {
                width = m_result.Width,
                height = m_result.Height,
            };

            return n_result;
        }

        protected virtual CefSize GetPdfPaperSize(CefBrowser browser, int deviceUnitsPerInch)
            => new CefSize();
    }
}
