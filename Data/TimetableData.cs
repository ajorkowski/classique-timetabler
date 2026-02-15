using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
        private double _alphaMakespan = 1.0;
        private double _betaStudentClustering = 1.0;
        private double _gammaAgePriority = 1.0;
        private double _crossDayPenalty = 480.0; // Default to 8 hours in minutes

        public ObservableCollection<Teacher> Teachers
        {
            get => _teachers;
            set
            {
                if (_teachers != null)
                    _teachers.CollectionChanged -= OnCollectionChanged;
                _teachers = value;
                if (_teachers != null)
                    _teachers.CollectionChanged += OnCollectionChanged;
                RaiseDataChanged();
            }
        }

        public ObservableCollection<Studio> Studios
        {
            get => _studios;
            set
            {
                if (_studios != null)
                    _studios.CollectionChanged -= OnCollectionChanged;
                _studios = value;
                if (_studios != null)
                    _studios.CollectionChanged += OnCollectionChanged;
                RaiseDataChanged();
            }
        }

        public ObservableCollection<Group> Groups
        {
            get => _groups;
            set
            {
                if (_groups != null)
                    _groups.CollectionChanged -= OnCollectionChanged;
                _groups = value;
                if (_groups != null)
                    _groups.CollectionChanged += OnCollectionChanged;
                RaiseDataChanged();
            }
        }

        public ObservableCollection<Student> Students
        {
            get => _students;
            set
            {
                if (_students != null)
                    _students.CollectionChanged -= OnCollectionChanged;
                _students = value;
                if (_students != null)
                    _students.CollectionChanged += OnCollectionChanged;
                RaiseDataChanged();
            }
        }

        /// <summary>
        /// The final scheduled classes (accepted timetable).
        /// This is populated when the user accepts a generated schedule.
        /// </summary>
        public List<ScheduledClass> ScheduledClasses
        {
            get => _scheduledClasses;
            set { _scheduledClasses = value; RaiseDataChanged(); }
        }

        /// <summary>
        /// Alpha: Weight for makespan minimization.
        /// Higher values prioritize finishing all classes as early as possible.
        /// </summary>
        public double AlphaMakespan
        {
            get => _alphaMakespan;
            set { _alphaMakespan = value; RaiseDataChanged(); }
        }

        /// <summary>
        /// Beta: Weight for student clustering.
        /// Higher values prioritize grouping each student's classes together to minimize gaps.
        /// </summary>
        public double BetaStudentClustering
        {
            get => _betaStudentClustering;
            set { _betaStudentClustering = value; RaiseDataChanged(); }
        }

        /// <summary>
        /// Gamma: Weight for age-based scheduling.
        /// Higher values prioritize scheduling younger students' classes earlier in the day.
        /// </summary>
        public double GammaAgePriority
        {
            get => _gammaAgePriority;
            set { _gammaAgePriority = value; RaiseDataChanged(); }
        }

        /// <summary>
        /// W_cross: Penalty for cross-day gaps in student schedules (in minutes).
        /// When a student has classes on different days, this penalty is applied instead of the actual time gap.
        /// Should be high to encourage same-day clustering.
        /// </summary>
        public double CrossDayPenalty
        {
            get => _crossDayPenalty;
            set { _crossDayPenalty = value; RaiseDataChanged(); }
        }

        public TimetableData()
        {
            _teachers.CollectionChanged += OnCollectionChanged;
            _studios.CollectionChanged += OnCollectionChanged;
            _groups.CollectionChanged += OnCollectionChanged;
            _students.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaiseDataChanged();
        }

        private void RaiseDataChanged()
        {
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
