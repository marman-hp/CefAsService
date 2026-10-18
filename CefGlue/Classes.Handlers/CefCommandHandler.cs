namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Design;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefCommandHandler
    {
        private int on_chrome_command(cef_command_handler_t* self, cef_browser_t* browser, int command_id, CefWindowOpenDisposition disposition)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            return OnChromeCommand(m_browser, command_id, disposition) ? 1 : 0;
        }

        protected abstract bool OnChromeCommand(CefBrowser browser, int commandId, CefWindowOpenDisposition disposition);

        private int is_chrome_app_menu_item_visible(cef_command_handler_t* self, cef_browser_t* browser, int command_id)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var result = IsChromeAppMenuItemVisible(m_browser, command_id);
            return result ? 1 : 0;
        }

        protected virtual bool IsChromeAppMenuItemVisible(CefBrowser browser, int commandId) => true;

        private int is_chrome_app_menu_item_enabled(cef_command_handler_t* self, cef_browser_t* browser, int command_id)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var result = IsChromeAppMenuItemEnabled(m_browser, command_id);
            return result ? 1 : 0;
        }

        protected virtual bool IsChromeAppMenuItemEnabled(CefBrowser browser, int commandId) => true;

        private int is_chrome_page_action_icon_visible(cef_command_handler_t* self, CefChromePageActionIconType icon_type)
        {
            CheckSelf(self);

            var result = IsChromePageActionIconVisible(icon_type);
            return result ? 1 : 0;
        }

        protected virtual bool IsChromePageActionIconVisible(CefChromePageActionIconType iconType) => true;

        private int is_chrome_toolbar_button_visible(cef_command_handler_t* self, CefChromeToolbarButtonType button_type)
        {
            CheckSelf(self);

            var result = IsChromeToolbarButtonVisible(button_type);
            return result ? 1 : 0;
        }

        protected virtual bool IsChromeToolbarButtonVisible(CefChromeToolbarButtonType buttonType) => true;
    }
}
