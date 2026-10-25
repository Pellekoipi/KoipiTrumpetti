using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.ViewModels;
using System.Collections.Generic;

namespace EarTrumpet.UI.Helpers
{
    public static class TaskbarAppVisibility
    {
        public static bool IsVisibleMixerApp(IAppItemViewModel app)
        {
            if (app == null || IsSystemSounds(app))
            {
                return false;
            }

            foreach (var processId in EnumerateProcessIds(app))
            {
                if (TaskbarWindowMonitor.Current.HasTaskbarWindow(processId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSystemSounds(IAppItemViewModel app)
        {
            var concrete = app as AppItemViewModel;
            if (concrete != null && concrete.IsSystemSoundsSession)
            {
                return true;
            }

            if (app.ChildApps == null)
            {
                return false;
            }

            foreach (var child in app.ChildApps)
            {
                if (IsSystemSounds(child))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<int> EnumerateProcessIds(IAppItemViewModel app)
        {
            if (app.ProcessId > 0)
            {
                yield return app.ProcessId;
            }

            if (app.ChildApps == null)
            {
                yield break;
            }

            foreach (var child in app.ChildApps)
            {
                foreach (var processId in EnumerateProcessIds(child))
                {
                    yield return processId;
                }
            }
        }
    }
}
