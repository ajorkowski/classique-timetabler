namespace classique.timetabler.Models
{
    /// <summary>
    /// Represents a scheduled class in the final timetable.
    /// This is the output of the solver - a concrete assignment of a class to a time slot.
    /// </summary>
    public class ScheduledClass
    {
        /// <summary>
        /// Unique identifier for this scheduled entry
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// True if this is a solo, false if it's a group
        /// </summary>
        public bool IsSolo { get; set; }

        /// <summary>
        /// The solo ID if IsSolo is true, null otherwise
        /// </summary>
        public Guid? SoloId { get; set; }

        /// <summary>
        /// The student IDs enrolled in this class.
        /// For solos, this will have one entry. For groups, multiple entries.
        /// </summary>
        public List<Guid> StudentIds { get; set; } = [];

        /// <summary>
        /// The group ID if IsSolo is false, null otherwise
        /// </summary>
        public Guid? GroupId { get; set; }

        /// <summary>
        /// The teacher assigned to teach this class
        /// </summary>
        public Guid TeacherId { get; set; }

        /// <summary>
        /// The studio where this class takes place
        /// </summary>
        public Guid StudioId { get; set; }

        /// <summary>
        /// The day of the week this class is scheduled
        /// </summary>
        public DayOfWeek Day { get; set; }

        /// <summary>
        /// The start time of the class
        /// </summary>
        public TimeOnly StartTime { get; set; }

        /// <summary>
        /// The end time of the class (calculated from start time + duration)
        /// </summary>
        public TimeOnly EndTime { get; set; }

        /// <summary>
        /// Duration of the class in minutes
        /// </summary>
        public int DurationMinutes { get; set; }
    }
}