using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;
using classique.timetabler.Models;

namespace classique.timetabler.Data
{
    public class TimetableData
    {
        public event EventHandler? DataChanged;

        private ObservableCollection<Teacher> _teachers = new();
        private ObservableCollection<Studio> _studios = new();
        private ObservableCollection<Group> _groups = new();
        private ObservableCollection<Student> _students = new();
        private List<ScheduledClass> _scheduledClasses = new();

        // Solver weights
        private long _alphaMakespan = 100;
        private long _betaStudentClustering = 100;
        private long _gammaAgePriority = 1;
        private long _crossDayPenalty = 480; // Default to 8 hours in minutes

        // Last generated schedule result (not persisted)
        private ScheduleResult? _lastScheduleResult;

        /// <summary>
        /// The final scheduled classes (accepted timetable).
        /// This is populated when the user accepts a generated schedule.
        /// Note: Setting this property does NOT raise DataChanged to avoid clearing itself.
        /// </summary>
        public List<ScheduledClass> ScheduledClasses
        {
            get => _scheduledClasses;
            set { _scheduledClasses = value; }
        }

        /// <summary>
        /// Alpha: Weight for makespan minimization.
        /// Higher values prioritize finishing all classes as early as possible.
        /// </summary>
        public long AlphaMakespan
        {
            get => _alphaMakespan;
            set { _alphaMakespan = value; RaiseDataChanged(); }
        }

        /// <summary>
        /// Beta: Weight for student clustering.
        /// Higher values prioritize grouping each student's classes together to minimize gaps.
        /// </summary>
        public long BetaStudentClustering
        {
            get => _betaStudentClustering;
            set { _betaStudentClustering = value; RaiseDataChanged(); }
        }

        /// <summary>
        /// Gamma: Weight for age-based scheduling.
        /// Higher values prioritize scheduling younger students' classes earlier in the day.
        /// </summary>
        public long GammaAgePriority
        {
            get => _gammaAgePriority;
            set { _gammaAgePriority = value; RaiseDataChanged(); }
        }

        /// <summary>
        /// W_cross: Penalty for cross-day gaps in student schedules (in minutes).
        /// When a student has classes on different days, this penalty is applied instead of the actual time gap.
        /// Should be high to encourage same-day clustering.
        /// </summary>
        public long CrossDayPenalty
        {
            get => _crossDayPenalty;
            set { _crossDayPenalty = value; RaiseDataChanged(); }
        }

        /// <summary>
        /// The most recently generated schedule result.
        /// This is transient and not saved to file - it holds the result of the last solve.
        /// </summary>
        [JsonIgnore]
        public ScheduleResult? LastScheduleResult
        {
            get => _lastScheduleResult;
            set { _lastScheduleResult = value; }
        }

        public ObservableCollection<Teacher> Teachers
        {
            get => _teachers;
            set
            {
                UnsubscribeFromCollection(_teachers);
                _teachers = value;
                SubscribeToCollection(_teachers);
                RaiseDataChanged();
            }
        }

        public ObservableCollection<Studio> Studios
        {
            get => _studios;
            set
            {
                UnsubscribeFromCollection(_studios);
                _studios = value;
                SubscribeToCollection(_studios);
                RaiseDataChanged();
            }
        }

        public ObservableCollection<Group> Groups
        {
            get => _groups;
            set
            {
                UnsubscribeFromCollection(_groups);
                _groups = value;
                SubscribeToCollection(_groups);
                RaiseDataChanged();
            }
        }

        public ObservableCollection<Student> Students
        {
            get => _students;
            set
            {
                UnsubscribeFromCollection(_students);
                _students = value;
                SubscribeToCollection(_students);
                RaiseDataChanged();
            }
        }

        public TimetableData()
        {
            SubscribeToCollection(_teachers);
            SubscribeToCollection(_studios);
            SubscribeToCollection(_groups);
            SubscribeToCollection(_students);
        }

        private void SubscribeToCollection<T>(ObservableCollection<T> collection) where T : INotifyPropertyChanged
        {
            if (collection == null) return;
            collection.CollectionChanged += OnCollectionChanged;
            foreach (var item in collection)
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }

        private void UnsubscribeFromCollection<T>(ObservableCollection<T> collection) where T : INotifyPropertyChanged
        {
            if (collection == null) return;
            collection.CollectionChanged -= OnCollectionChanged;
            foreach (var item in collection)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Subscribe to new items
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is INotifyPropertyChanged notifyItem)
                    {
                        notifyItem.PropertyChanged += OnItemPropertyChanged;
                    }
                }
            }

            // Unsubscribe from removed items
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is INotifyPropertyChanged notifyItem)
                    {
                        notifyItem.PropertyChanged -= OnItemPropertyChanged;
                    }
                }
            }

            RaiseDataChanged();
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Only raise DataChanged for properties that affect scheduling
            // Exclude display-only computed properties
            if (e.PropertyName != null && !IsDisplayOnlyProperty(e.PropertyName))
            {
                RaiseDataChanged();
            }
        }

        private static bool IsDisplayOnlyProperty(string propertyName)
        {
            // These are computed display properties that don't affect the schedule
            return propertyName switch
            {
                "TeacherNames" => true,
                "FirstTeacherName" => true,
                "StudioName" => true,
                "ScheduleDisplay" => true,
                "StudentCount" => true,
                "StudentNames" => true,
                "StudentNamesDisplay" => true,
                "HasWarnings" => true,
                "ValidationWarnings" => true,
                "WarningsSummary" => true,
                "DayGrouping" => true,
                "SortableStartTime" => true,
                "GroupNames" => true,
                "SolosSummary" => true,
                "TeacherName" => true,
                "Age" => true,
                _ => false
            };
        }

        private void RaiseDataChanged()
        {
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Creates a snapshot of the current AppData for use by the solver.
        /// This avoids threading issues by copying the data.
        /// </summary>
        public static TimetableData FromAppData(TimetableData source)
        {
            var data = new TimetableData
            {
                AlphaMakespan = source.AlphaMakespan,
                BetaStudentClustering = source.BetaStudentClustering,
                GammaAgePriority = source.GammaAgePriority,
                CrossDayPenalty = source.CrossDayPenalty
            };

            // Copy collections
            foreach (var teacher in source.Teachers)
            {
                data.Teachers.Add(teacher);
            }
            foreach (var studio in source.Studios)
            {
                data.Studios.Add(studio);
            }
            foreach (var group in source.Groups)
            {
                data.Groups.Add(group);
            }
            foreach (var student in source.Students)
            {
                data.Students.Add(student);
            }

            return data;
        }
    }
}
