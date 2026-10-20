using System.Runtime.InteropServices;
using DesktopDashboard.Core.Settings;
using static DesktopDashboard.Services.NativeMethods;

namespace DesktopDashboard.Services;

/// <summary>
/// Keeps the widget at the bottom of the window stack in <see cref="WindowLayer.Desktop"/> mode.
/// Every z-order change is rewritten to <c>HWND_BOTTOM</c>, so neither clicking the widget nor
/// opening other apps brings it above them. When the user summons it explicitly (tray, second
/// launch) it may come forward until it loses activation, then it sinks back to the desktop.
/// </summary>
internal sealed class WindowLayerController : IDisposable
{
    private const nuint SubclassId = 1;

    private readonly nint _hwnd;

    // Kept in a field so the GC cannot collect the delegate while Windows still calls it.
    private readonly SubclassProc _proc;
    private readonly nint _procPointer;
    private WindowLayer _layer = WindowLayer.Normal;
    private bool _raised;

    public WindowLayerController(nint hwnd)
    {
        _hwnd = hwnd;
        _proc = WindowProc;
        _procPointer = Marshal.GetFunctionPointerForDelegate(_proc);
        SetWindowSubclass(hwnd, _procPointer, SubclassId, 0);
    }

    public void Apply(WindowLayer layer)
    {
        _layer = layer;
        _raised = false;
        if (layer == WindowLayer.Desktop)
        {
            SendToBottom();
        }
    }

    /// <summary>
    /// Brings the widget to the front and focuses it. In desktop mode it stays in front only until
    /// it is deactivated; if Windows refuses focus (foreground lock) it goes straight back down,
    /// because without activation there would be no deactivation to send it back.
    /// </summary>
    public void BringForward()
    {
        _raised = _layer == WindowLayer.Desktop;
        if (_layer != WindowLayer.OnTop)
        {
            SetWindowPos(_hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }

        SetForegroundWindow(_hwnd);
        if (_raised && GetForegroundWindow() != _hwnd)
        {
            _raised = false;
            SendToBottom();
        }
    }

    private void SendToBottom() =>
        SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

    private unsafe nint WindowProc(nint hwnd, uint msg, nint wParam, nint lParam, nuint idSubclass, nuint refData)
    {
        if (_layer == WindowLayer.Desktop)
        {
            if (msg == WM_WINDOWPOSCHANGING && !_raised)
            {
                var pos = (WINDOWPOS*)lParam;
                if ((pos->flags & SWP_NOZORDER) == 0)
                {
                    pos->hwndInsertAfter = HWND_BOTTOM;
                }
            }
            else if (msg == WM_ACTIVATE && (wParam & 0xFFFF) == 0 && _raised)
            {
                var result = DefSubclassProc(hwnd, msg, wParam, lParam);
                _raised = false;
                SendToBottom();
                return result;
            }
        }

        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose() => RemoveWindowSubclass(_hwnd, _procPointer, SubclassId);
}
