namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefMenuModelDelegate
    {
        private void execute_command(cef_menu_model_delegate_t* self, cef_menu_model_t* menu_model, int command_id, CefEventFlags event_flags)
        {
            CheckSelf(self);

            var m_menuModel = CefMenuModel.FromNative(menu_model);
            ExecuteCommand(m_menuModel, command_id, event_flags);
        }

        protected abstract void ExecuteCommand(CefMenuModel menuModel, int commandId, CefEventFlags eventFlags);

        private void mouse_outside_menu(cef_menu_model_delegate_t* self, cef_menu_model_t* menu_model, cef_point_t* screen_point)
        {
            CheckSelf(self);

            var m_menuModel = CefMenuModel.FromNative(menu_model);
            var m_screenPoint = new CefPoint(screen_point->x, screen_point->y);
            MouseOutsideMenu(m_menuModel, m_screenPoint);
        }

        protected virtual void MouseOutsideMenu(CefMenuModel menuModel, CefPoint screenPoint) { }

        private void unhandled_open_submenu(cef_menu_model_delegate_t* self, cef_menu_model_t* menu_model, int is_rtl)
        {
            CheckSelf(self);

            var m_menuModel = CefMenuModel.FromNative(menu_model);
            UnhandledOpenSubmenu(m_menuModel, is_rtl != 0);
        }

        protected virtual void UnhandledOpenSubmenu(CefMenuModel menuModel, bool isRtl) { }

        private void unhandled_close_submenu(cef_menu_model_delegate_t* self, cef_menu_model_t* menu_model, int is_rtl)
        {
            CheckSelf(self);

            var m_menuModel = CefMenuModel.FromNative(menu_model);
            UnhandledCloseSubmenu(m_menuModel, is_rtl != 0);
        }

        protected virtual void UnhandledCloseSubmenu(CefMenuModel menuModel, bool isRtl) { }

        private void menu_will_show(cef_menu_model_delegate_t* self, cef_menu_model_t* menu_model)
        {
            CheckSelf(self);

            var m_menuModel = CefMenuModel.FromNative(menu_model);
            MenuWillShow(m_menuModel);
        }

        protected abstract void MenuWillShow(CefMenuModel menuModel);

        private void menu_closed(cef_menu_model_delegate_t* self, cef_menu_model_t* menu_model)
        {
            CheckSelf(self);

            var m_menuModel = CefMenuModel.FromNative(menu_model);
            MenuClosed(m_menuModel);
        }

        protected abstract void MenuClosed(CefMenuModel menuModel);

        private int format_label(cef_menu_model_delegate_t* self, cef_menu_model_t* menu_model, cef_string_t* label)
        {
            CheckSelf(self);

            var m_menuModel = CefMenuModel.FromNative(menu_model);
            var m_label = cef_string_t.ToString(label);

            if (FormatLabel(m_menuModel, ref m_label))
            {
                cef_string_t.Copy(m_label, label);
                return 1;
            }
            else
            {
                return 0;
            }
        }

        protected abstract bool FormatLabel(CefMenuModel menuModel, ref string label);
    }
}
