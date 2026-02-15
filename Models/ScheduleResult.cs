namespace classique.timetabler.Models
{
    /// <summary>
    /// Container for the results of a scheduling run.
    /// This holds the generated timetable and metadata about the solve.
    /// </summary>
    public class ScheduleResult
    {
        /// <summary>
        /// Unique identifier for this result
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// When this schedule was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// The scheduled classes that make up the timetable
        /// </summary>
        public List<ScheduledClass> ScheduledClasses { get; set; } = new();

        /// <summary>
        /// Whether the solver found a valid solution
        /// </summary>
        public bool IsFeasible { get; set; }

        /// <summary>
        /// Whether the solution is proven optimal
        /// </summary>
        public bool IsOptimal { get; set; }

        /// <summary>
        /// The objective value achieved (lower is better)
        /// </summary>
        public double ObjectiveValue { get; set; }

        /// <summary>
        /// The makespan value (latest end time across all classes)
        /// </summary>
        public int MakespanMinutes { get; set; }

        /// <summary>
        /// Total student gap penalty in the solution
        /// </summary>
        public double TotalStudentGapPenalty { get; set; }

        /// <summary>
        /// Total age priority penalty in the solution
        /// </summary>
        public double TotalAgePenalty { get; set; }

        /// <summary>
        /// How long the solver took to find this solution
        /// </summary>
        public TimeSpan SolveTime { get; set; }

        /// <summary>
        /// Any messages or notes from the solver
        /// </summary>
        public string? SolverMessage { get; set; }

        /// <summary>
        /// The weights used when generating this schedule
        /// </summary>
        public double AlphaMakespan { get; set; }
        public double BetaStudentClustering { get; set; }
        public double GammaAgePriority { get; set; }
        public double CrossDayPenalty { get; set; }
    }
}