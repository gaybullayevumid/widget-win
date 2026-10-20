using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace DesktopDashboard;

/// <summary>
/// Custom entry point so a second launch (Start menu, startup entry, double-click) hands over to
/// the running instance, which then shows its window, instead of opening a second dashboard.
/// </summary>
public static class Program
{
    private const string InstanceKey = "DesktopDashboard.Main";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!mainInstance.IsCurrent)
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();

            // This process was just started by the user, so it may take the foreground; pass
            // that right on so the running instance can bring its window forward.
            Services.NativeMethods.AllowSetForegroundWindow(mainInstance.ProcessId);

            // Redirect from a worker thread: blocking the STA thread on the async call can deadlock.
            Task.Run(() => mainInstance.RedirectActivationToAsync(activation).AsTask()).Wait();
            return 0;
        }

        mainInstance.Activated += (_, _) => App.Current?.OnRedirectedActivation();

        Application.Start(callbackParams =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App(isStartupLaunch: args.Contains(Core.Settings.RunKeyStartupRegistration.StartupArgument));
        });

        return 0;
    }
}
