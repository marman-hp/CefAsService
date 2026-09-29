using System;
using Xilium.CefGlue.Common;
using Xilium.CefGlue.Common.Coordinators;

namespace Xilium.CefGlue.Headless
{
    internal sealed class HeadlessOffscreenRenderCallback : DefaultOffscreenRenderCallback
    {
        public HeadlessOffscreenRenderCallback(IPaintDispatcher paintDispatcher, IDragDropCoordinator dragDropCoordinator)
            : base(paintDispatcher, dragDropCoordinator)
        {
        }

        public override void GetScreenInfo(CefScreenInfo screenInfo)
        {
            base.GetScreenInfo(screenInfo);

            var surface = Context?.Target?.RenderSurface;
            if (surface == null)
            {
                return;
            }

            var w = Math.Max(1, surface.Width);
            var h = Math.Max(1, surface.Height);
            var rect = new CefRectangle(0, 0, w, h);
            screenInfo.Rectangle = rect;
            screenInfo.AvailableRectangle = rect;
        }

        public override void HandleVirtualKeyboardRequested(CefTextInputMode inputMode)
        {
            (Context?.Target as HeadlessTarget)?.RaiseVirtualKeyboardRequested(inputMode);
        }
    }
}
