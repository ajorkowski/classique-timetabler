using System.IO;
using System.Windows.Threading;

namespace classique.timetabler.Data
{
    public static class AutoSaveService
    {
        private static readonly string AutoSaveFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassiqueTimetabler");
        
        private static readonly string AutoSaveFile = Path.Combine(AutoSaveFolder, "autosave.timetable");
        
        private static DispatcherTimer? _timer;
        private static bool _isDirty;
        private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Raised when autosave starts
        /// </summary>
        public static event EventHandler? AutoSaveStarted;

        /// <summary>
        /// Raised when autosave completes
        /// </summary>
        public static event EventHandler? AutoSaveCompleted;

        public static void StartWatching()
        {
            if (_timer != null)
                return;

            AppData.Current.DataChanged += OnDataChanged;
            
            _timer = new DispatcherTimer
            {
                Interval = AutoSaveInterval
            };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        public static void StopWatching()
        {
            if (_timer == null)
                return;

            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _timer = null;
            
            AppData.Current.DataChanged -= OnDataChanged;
            
            // Save any pending changes before stopping
            if (_isDirty)
            {
                Save();
            }
        }

        private static void OnDataChanged(object? sender, EventArgs e)
        {
            _isDirty = true;
        }

        private static void OnTimerTick(object? sender, EventArgs e)
        {
            if (_isDirty)
            {
                Save();
                _isDirty = false;
            }
        }

        public static void Save()
        {
            try
            {
                AutoSaveStarted?.Invoke(null, EventArgs.Empty);
                
                EnsureAutoSaveFolderExists();
                FileService.Save(AutoSaveFile);
                
                AutoSaveCompleted?.Invoke(null, EventArgs.Empty);
            }
            catch
            {
                // Silently fail autosave to not disrupt user
                AutoSaveCompleted?.Invoke(null, EventArgs.Empty);
            }
        }

        public static bool HasAutoSave()
        {
            return File.Exists(AutoSaveFile);
        }

        public static bool TryRecover()
        {
            try
            {
                if (!HasAutoSave())
                    return false;

                return FileService.TryLoad(AutoSaveFile);
            }
            catch
            {
                // Recovery failed, start fresh
            }
            
            return false;
        }

        public static void ClearAutoSave()
        {
            try
            {
                if (File.Exists(AutoSaveFile))
                {
                    File.Delete(AutoSaveFile);
                }
            }
            catch
            {
                // Ignore deletion errors
            }
        }

        private static void EnsureAutoSaveFolderExists()
        {
            if (!Directory.Exists(AutoSaveFolder))
            {
                Directory.CreateDirectory(AutoSaveFolder);
            }
        }
    }
}
