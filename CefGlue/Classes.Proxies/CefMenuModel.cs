namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefMenuModel
    {
        public static CefMenuModel Create(CefMenuModelDelegate handler)
        {
            return CefMenuModel.FromNative(
                cef_menu_model_t.create(handler.ToNative())
                );
        }

        public bool IsSubMenu
        {
            get
            {
                return cef_menu_model_t.is_sub_menu(_self) != 0;
            }
        }

        public bool Clear()
        {
            return cef_menu_model_t.clear(_self) != 0;
        }

        public nuint Count
        {
            get { return cef_menu_model_t.get_count(_self); }
        }

        public bool AddSeparator()
        {
            return cef_menu_model_t.add_separator(_self) != 0;
        }

        public bool AddItem(int commandId, string label)
        {
            fixed (char* label_str = label)
            {
                var n_label = new cef_string_t(label_str, label.Length);
                return cef_menu_model_t.add_item(_self, commandId, &n_label) != 0;
            }
        }

        public bool AddCheckItem(int commandId, string label)
        {
            fixed (char* label_str = label)
            {
                var n_label = new cef_string_t(label_str, label.Length);
                return cef_menu_model_t.add_check_item(_self, commandId, &n_label) != 0;
            }
        }

        public bool AddRadioItem(int commandId, string label, int groupId)
        {
            fixed (char* label_str = label)
            {
                var n_label = new cef_string_t(label_str, label.Length);
                return cef_menu_model_t.add_radio_item(_self, commandId, &n_label, groupId) != 0;
            }
        }

        public CefMenuModel AddSubMenu(int commandId, string label)
        {
            fixed (char* label_str = label)
            {
                var n_label = new cef_string_t(label_str, label.Length);
                return CefMenuModel.FromNative(
                    cef_menu_model_t.add_sub_menu(_self, commandId, &n_label)
                    );
            }
        }

        public bool InsertSeparatorAt(nuint index)
        {
            return cef_menu_model_t.insert_separator_at(_self, index) != 0;
        }

        public bool InsertItemAt(nuint index, int commandId, string label)
        {
            fixed (char* label_str = label)
            {
                var n_label = new cef_string_t(label_str, label.Length);
                return cef_menu_model_t.insert_item_at(_self, index, commandId, &n_label) != 0;
            }
        }

        public bool InsertCheckItemAt(nuint index, int commandId, string label)
        {
            fixed (char* label_str = label)
            {
                var n_label = new cef_string_t(label_str, label.Length);
                return cef_menu_model_t.insert_check_item_at(_self, index, commandId, &n_label) != 0;
            }
        }

        public bool InsertRadioItemAt(nuint index, int commandId, string label, int groupId)
        {
            fixed (char* label_str = label)
            {
                var n_label = new cef_string_t(label_str, label.Length);
                return cef_menu_model_t.insert_radio_item_at(_self, index, commandId, &n_label, groupId) != 0;
            }
        }

        public CefMenuModel InsertSubMenuAt(nuint index, int commandId, string label)
        {
            fixed (char* label_str = label)
            {
                var n_label = new cef_string_t(label_str, label.Length);
                return CefMenuModel.FromNative(
                    cef_menu_model_t.add_sub_menu(_self, commandId, &n_label)
                    );
            }
        }

        public bool Remove(int commandId)
        {
            return cef_menu_model_t.remove(_self, commandId) != 0;
        }

        public bool RemoveAt(nuint index)
        {
            return cef_menu_model_t.remove_at(_self, index) != 0;
        }

        public int GetIndexOf(int commandId)
        {
            return cef_menu_model_t.get_index_of(_self, commandId);
        }

        public int GetCommandIdAt(nuint index)
        {
            return cef_menu_model_t.get_command_id_at(_self, index);
        }

        public bool SetCommandIdAt(nuint index, int commandId)
        {
            return cef_menu_model_t.set_command_id_at(_self, index, commandId) != 0;
        }

        public string GetLabel(int commandId)
        {
            var n_result = cef_menu_model_t.get_label(_self, commandId);
            return cef_string_userfree.ToString(n_result);
        }

        public string GetLabelAt(nuint index)
        {
            var n_result = cef_menu_model_t.get_label_at(_self, index);
            return cef_string_userfree.ToString(n_result);
        }

        public bool SetLabel(int commandId, string label)
        {
            fixed (char* label_str = label)
            {
                var n_label = new cef_string_t(label_str, label.Length);
                return cef_menu_model_t.set_label(_self, commandId, &n_label) != 0;
            }
        }

        public bool SetLabelAt(nuint index, string label)
        {
            fixed (char* label_str = label)
            {
                var n_label = new cef_string_t(label_str, label.Length);
                return cef_menu_model_t.set_label_at(_self, index, &n_label) != 0;
            }
        }

        public CefMenuItemType GetItemType(int commandId)
        {
            return cef_menu_model_t.get_type(_self, commandId);
        }

        public CefMenuItemType GetItemTypeAt(nuint index)
        {
            return cef_menu_model_t.get_type_at(_self, index);
        }

        public int GetGroupId(int commandId)
        {
            return cef_menu_model_t.get_group_id(_self, commandId);
        }

        public int GetGroupIdAt(nuint index)
        {
            return cef_menu_model_t.get_group_id_at(_self, index);
        }

        public bool SetGroupId(int commandId, int groupId)
        {
            return cef_menu_model_t.set_group_id(_self, commandId, groupId) != 0;
        }

        public bool SetGroupIdAt(nuint index, int groupId)
        {
            return cef_menu_model_t.set_group_id_at(_self, index, groupId) != 0;
        }

        public CefMenuModel GetSubMenu(int commandId)
        {
            return CefMenuModel.FromNativeOrNull(
                cef_menu_model_t.get_sub_menu(_self, commandId)
                );
        }

        public CefMenuModel GetSubMenuAt(nuint index)
        {
            return CefMenuModel.FromNativeOrNull(
                cef_menu_model_t.get_sub_menu_at(_self, index)
                );
        }

        public bool IsVisible(int commandId)
        {
            return cef_menu_model_t.is_visible(_self, commandId) != 0;
        }

        public bool IsVisibleAt(nuint index)
        {
            return cef_menu_model_t.is_visible_at(_self, index) != 0;
        }

        public bool SetVisible(int commandId, bool visible)
        {
            return cef_menu_model_t.set_visible(_self, commandId, visible ? 1 : 0) != 0;
        }

        public bool SetVisibleAt(nuint index, bool visible)
        {
            return cef_menu_model_t.set_visible_at(_self, index, visible ? 1 : 0) != 0;
        }

        public bool IsEnabled(int commandId)
        {
            return cef_menu_model_t.is_enabled(_self, commandId) != 0;
        }

        public bool IsEnabledAt(nuint index)
        {
            return cef_menu_model_t.is_enabled_at(_self, index) != 0;
        }

        public bool SetEnabled(int commandId, bool enabled)
        {
            return cef_menu_model_t.set_enabled(_self, commandId, enabled ? 1 : 0) != 0;
        }

        public bool SetEnabledAt(nuint index, bool enabled)
        {
            return cef_menu_model_t.set_enabled_at(_self, index, enabled ? 1 : 0) != 0;
        }

        public bool IsChecked(int commandId)
        {
            return cef_menu_model_t.is_checked(_self, commandId) != 0;
        }

        public bool IsCheckedAt(nuint index)
        {
            return cef_menu_model_t.is_checked_at(_self, index) != 0;
        }

        public bool SetChecked(int commandId, bool value)
        {
            return cef_menu_model_t.set_checked(_self, commandId, value ? 1 : 0) != 0;
        }

        public bool SetCheckedAt(nuint index, bool value)
        {
            return cef_menu_model_t.set_checked_at(_self, index, value ? 1 : 0) != 0;
        }

        public bool HasAccelerator(int commandId)
        {
            return cef_menu_model_t.has_accelerator(_self, commandId) != 0;
        }

        public bool HasAcceleratorAt(nuint index)
        {
            return cef_menu_model_t.has_accelerator_at(_self, index) != 0;
        }

        public bool SetAccelerator(int commandId, int keyCode, bool shiftPressed, bool ctrlPressed, bool altPressed)
        {
            return cef_menu_model_t.set_accelerator(
                _self,
                commandId,
                keyCode,
                shiftPressed ? 1 : 0,
                ctrlPressed ? 1 : 0,
                altPressed ? 1 : 0
                ) != 0;
        }

        public bool SetAcceleratorAt(nuint index, int keyCode, bool shiftPressed, bool ctrlPressed, bool altPressed)
        {
            return cef_menu_model_t.set_accelerator_at(
                _self,
                index,
                keyCode,
                shiftPressed ? 1 : 0,
                ctrlPressed ? 1 : 0,
                altPressed ? 1 : 0
                ) != 0;
        }

        public bool RemoveAccelerator(int commandId)
        {
            return cef_menu_model_t.remove_accelerator(_self, commandId) != 0;
        }

        public bool RemoveAcceleratorAt(nuint index)
        {
            return cef_menu_model_t.remove_accelerator_at(_self, index) != 0;
        }

        public bool GetAccelerator(int commandId, out int keyCode, out bool shiftPressed, out bool ctrlPressed, out bool altPressed)
        {
            int n_keyCode;
            int n_shiftPressed;
            int n_ctrlPressed;
            int n_altPressed;

            var result = cef_menu_model_t.get_accelerator(_self, commandId, &n_keyCode, &n_shiftPressed, &n_ctrlPressed, &n_altPressed) != 0;

            keyCode = n_keyCode;
            shiftPressed = n_shiftPressed != 0;
            ctrlPressed = n_ctrlPressed != 0;
            altPressed = n_altPressed != 0;

            return result;
        }

        public bool GetAcceleratorAt(nuint index, out int keyCode, out bool shiftPressed, out bool ctrlPressed, out bool altPressed)
        {
            int n_keyCode;
            int n_shiftPressed;
            int n_ctrlPressed;
            int n_altPressed;

            var result = cef_menu_model_t.get_accelerator_at(_self, index, &n_keyCode, &n_shiftPressed, &n_ctrlPressed, &n_altPressed) != 0;

            keyCode = n_keyCode;
            shiftPressed = n_shiftPressed != 0;
            ctrlPressed = n_ctrlPressed != 0;
            altPressed = n_altPressed != 0;

            return result;
        }

        public bool SetColor(int commandId, CefMenuColorType colorType, uint color)
        {
            return cef_menu_model_t.set_color(_self, commandId, colorType, color) != 0;
        }

        public bool SetColorAt(int index, CefMenuColorType colorType, uint color)
        {
            return cef_menu_model_t.set_color_at(_self, index, colorType, color) != 0;
        }

        public bool GetColor(int commandId, CefMenuColorType colorType, out uint color)
        {
            uint n_color;
            var result = cef_menu_model_t.get_color(_self, commandId, colorType, &n_color) != 0;
            color = n_color;
            return result;
        }

        public bool GetColorAt(int index, CefMenuColorType colorType, out uint color)
        {
            uint n_color;
            var result = cef_menu_model_t.get_color_at(_self, index, colorType, &n_color) != 0;
            color = n_color;
            return result;
        }

        public bool SetFontList(int commandId, string fontList)
        {
            fixed (char* fontList_str = fontList)
            {
                var n_fontList = new cef_string_t(fontList_str, fontList != null ? fontList.Length : 0);
                return cef_menu_model_t.set_font_list(_self, commandId, &n_fontList) != 0;
            }
        }

        public bool SetFontListAt(int index, string fontList)
        {
            fixed (char* fontList_str = fontList)
            {
                var n_fontList = new cef_string_t(fontList_str, fontList != null ? fontList.Length : 0);
                return cef_menu_model_t.set_font_list_at(_self, index, &n_fontList) != 0;
            }
        }
    }
}
