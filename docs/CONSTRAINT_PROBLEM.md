# Dance Studio Timetable Scheduling: A Constraint Programming Approach

## Introduction

The dance studio timetable scheduling problem is a variant of the flexible job shop scheduling problem adapted for scheduling dance classes, group sessions, and solo performances across multiple teachers and studios. The goal is to create an optimal weekly timetable that minimizes total teaching time while clustering student activities and prioritizing younger students for earlier time slots.

## Problem Description

We need to schedule a set of classes (groups and solos) across available teachers and time slots. Each class has specific requirements:
- A duration
- One or more possible teachers who can teach it
- Students who must attend

The scheduling must respect teacher availability, avoid conflicts, and optimize for multiple objectives.

## Simplifying Assumptions

To make the problem tractable, we make the following assumptions:

1. **Teacher-Studio Coupling**: A teacher uses their assigned studio exclusively during their availability window. We don't need to consider studio as a separate variable - it's determined by the teacher's availability.

2. **Teacher as Machine**: For a given day, each teacher acts like a "machine" in classic job shop terminology. However, their availability may not be continuous (e.g., available 9-12 and 2-5, but not 12-2).

3. **Fixed Groups Pre-allocated**: Any groups with fixed times are pre-allocated and simply reduce the available time slots for their assigned teacher on that day.

4. **Unit Capacity**: Each teacher can only teach one class at a time (capacity = 1), and each class requires exactly one teaching slot (demand = 1).

5. **Multi-objective Optimization**: We optimize for three objectives:
   - Maximize free time at end of teacher's day (push classes earlier in the day)
   - Minimize student fragmentation (cluster each student's activities)
   - Prioritize younger students for earlier time slots

6. **Teacher-Day Relative Time**: To ensure fairness across teachers with different start times, we use times relative to each teacher's first availability on that day (see Implementation Notes).

---

## Mathematical Formulation

### Parameters

| Symbol | Description |
|--------|-------------|
| D | Set of days of the week, indexed by d |
| T(d) | Set of teachers available on day d, indexed by t |
| A(d,t) | Set of available time windows for teacher t on day d, indexed by a |
| day_start(t,d) | The earliest availability start time for teacher t on day d |
| C | Set of non-fixed classes (flexible groups and solos), indexed by c |
| G | Set of fixed groups, indexed by g |
| H(g) | Start time of fixed group g |
| J(g) | End time of fixed group g |
| O(c) | Set of scheduling alternatives for class c, indexed by o |
| dur(c) | Duration of class c (in minutes) |
| window_start(o) | Absolute start time of the availability window for alternative o |
| window_end(o) | Absolute end time of the availability window for alternative o |
| window_duration(o) | Duration of availability window: window_end(o) - window_start(o) |
| teacher(c,o) | Teacher assigned when class c uses alternative o |
| day(c,o) | Day assigned when class c uses alternative o |
| S | Set of students, indexed by s |
| C(s) | Set of flexible classes that student s is enrolled in |
| G(s) | Set of fixed groups that student s is enrolled in |
| age(s) | Age of student s |
| U(s) | Set of unavailability windows for student s |
| W_cross | Penalty weight for cross-day gaps (should be high) |

### Decision Variables

| Variable | Domain | Description |
|----------|--------|-------------|
| rel_start(c,o) | Integer in [0, window_duration(o) - dur(c)] | Relative start time of class c within window o |
| rel_end(c,o) | Integer in [dur(c), window_duration(o)] | Relative end time of class c within window o |
| p(c,o) | {0, 1} | Binary: 1 if alternative o is chosen for class c |

Note: 
- Absolute times are computed as: `abs_start = window_start(o) + rel_start(c,o)`
- Teacher-day relative times are computed as: `teacher_rel_start = abs_start - day_start(teacher(o), day(o))`

### Objective Function

We minimize a weighted combination of objectives:

```
Minimize:  alpha * SUM(FreeTimePenalty(c))  +  beta * SUM(StudentGap(s))  +  gamma * SUM(AgePenalty(c))
                  for all c in C                    for all s in S              for all c in C
```

Where:
- alpha: Weight for free time maximization (pushes classes earlier in the day)
- beta: Weight for student clustering
- gamma: Weight for age-based scheduling

**Free Time Penalty (Alpha):**

For each class c with selected alternative o:
```
FreeTimePenalty(c) = abs_end(c,o) - day_start(teacher(o), day(o))
                   = window_start(o) - day_start(teacher(o), day(o)) + rel_end(c,o)
```

This is the end time relative to when the teacher starts their day. By minimizing this:
- Classes are pushed earlier in the teacher's day
- A class at 9am for a teacher starting at 9am has penalty equal to just its duration
- A class at 2pm for a teacher starting at 9am has a higher penalty
- A class at 2pm for a teacher starting at 2pm has penalty equal to just its duration (fair!)

**Student Gap Calculation (Beta):**

```
StudentGap(s) = SUM( Gap(c1, c2) )
                for all c1, c2 in C(s) UNION G(s) where c1 != c2
```

Where G(s) is the set of fixed groups that student s is enrolled in.

Gap(c1, c2) is:
- **Same day**: The absolute time difference between classes, calculated as:
  ```
  Gap = max(0, abs_start(c2) - abs_end(c1), abs_start(c1) - abs_end(c2))
  ```
  Where `abs_time = window_start + rel_time`. This works correctly even when classes are in different availability windows on the same day.
- **Different days**: W_cross (high penalty to encourage same-day clustering)

**Age Penalty Calculation (Gamma):**

```
AgePenalty(c) = (100 / min_age(c)) * (abs_start(c,o) - day_start(teacher(o), day(o)))
```

Where min_age(c) is the minimum age among students in class c. This gives younger students' classes a higher penalty for later start times relative to the teacher's day.

---

## Constraints

### 1. Task Duration Constraint

For each class c and alternative o:
```
rel_end(c,o) = rel_start(c,o) + dur(c)
```

The relative end time equals relative start time plus duration.

### 2. Alternative Selection Constraint

For each class c:
```
SUM( p(c,o) ) = 1    for all o in O(c)
```

Exactly one scheduling alternative must be chosen for each class.

### 3. Window Bounds Constraint

For each class c and chosen alternative o:
```
0 <= rel_start(c,o) <= window_duration(o) - dur(c)
dur(c) <= rel_end(c,o) <= window_duration(o)
```

The class must fit entirely within the availability window.

### 4. No Overlap Constraint (Same Teacher, Same Window)

For each pair of classes c1, c2 assigned to the same teacher, same day, AND same window:
```
NoOverlap(rel_start(c1), rel_end(c1), rel_start(c2), rel_end(c2))
```

A teacher cannot teach two classes simultaneously. Since we use relative times within windows for constraints, NoOverlap constraints are only applied between classes in the same window.

### 5. No Overlap Constraint (Same Student, Same Window)

For each student s and pair of classes c1, c2 in C(s) in the same window:
```
NoOverlap(rel_start(c1), rel_end(c1), rel_start(c2), rel_end(c2))
```

A student cannot attend two classes simultaneously within the same window. Classes in different windows cannot overlap by definition (they're in disjoint time ranges).

### 6. Student Availability Constraint

For each class c and student s enrolled in c:
```
For all u in U(s): NoOverlap(abs_start(c), abs_end(c), u.start, u.end)
```

Classes must not be scheduled during student unavailability windows. This constraint uses absolute times.

---

## Implementation Notes

### Teacher-Day Relative Time

To ensure fairness in the objective function across teachers with different availability start times, we use times relative to each **teacher's first availability start time on that day**:

- `day_start(teacher, day)` = the earliest time the teacher is available on that day
- `teacher_rel_time = abs_time - day_start`

**Why this matters:**
- Without this adjustment, a class scheduled at 9am would have a lower objective penalty than a class at 2pm
- This would unfairly penalize teachers who start later in the day
- With teacher-day relative times, a class at the start of any teacher's day has the same base penalty

**Example:**
- Teacher A starts at 9am, Teacher B starts at 2pm
- A class scheduled at 9am for Teacher A: penalty based on 0 + duration
- A class scheduled at 2pm for Teacher B: penalty based on 0 + duration (same!)
- A class scheduled at 2pm for Teacher A: penalty based on 300 + duration (5 hours later)

**Note:** We still use window-relative times (0 to window_duration) for the constraint variables and NoOverlap constraints. The teacher-day offset is applied only in the objective function calculations.

### Generating Alternatives

For each flexible class c, we generate alternatives based on:
1. Which teachers can teach the class
2. Which days those teachers are available
3. The available time windows on those days

Each valid (teacher, day, time-window) combination becomes an alternative o in O(c).

### Handling Discontinuous Availability

Teachers may have gaps in their availability (e.g., lunch breaks or fixed groups). We model this by:
1. Splitting availability into continuous windows
2. Each window uses relative times (0 to window_duration) for constraints
3. Classes must fit entirely within one continuous window
4. NoOverlap constraints are applied per-window (not per-day)
5. Objective penalties use teacher-day relative times (accounting for the window's offset from day start)

### Pre-processing Fixed Groups

Before solving:
1. Identify all fixed groups
2. Subtract their time slots from teacher availability
3. This may split continuous windows into smaller segments
4. Fixed groups are added to the solution after solving

### Solver Strategy

We use Google OR-Tools CP-SAT solver with:
1. Optional interval variables for each class alternative (using window-relative times)
2. NoOverlap constraints grouped by (teacher, day, window) and (student, day, window)
3. Conditional constraints using `OnlyEnforceIf` for alternative selection
4. Linear objective combining all penalty terms (using teacher-day relative times)

---

## Example

Consider a simple scenario:

**Teachers:**
- Alice: Available Mon 9:00-17:00 at Studio A
- Bob: Available Mon 9:00-12:00, Tue 14:00-18:00 at Studio B

**Students:**
- Emma (age 8): Enrolled in Group1, Solo1
- Liam (age 12): Enrolled in Group1, Group2

**Classes:**
- Group1: 60 min, can be taught by Alice or Bob
- Group2: 45 min, can be taught by Alice only
- Solo1 (Emma): 10 min, taught by Bob

**Fixed Groups:**
- Alice has a fixed class Mon 12:00-13:00 (splits her availability into 9:00-12:00 and 13:00-17:00)

**Day Start Times:**
- Alice on Monday: 9:00 (day_start = 540 minutes)
- Bob on Monday: 9:00 (day_start = 540 minutes)
- Bob on Tuesday: 14:00 (day_start = 840 minutes)

The solver would:
1. Generate alternatives for each flexible class (including both of Alice's windows as separate alternatives)
2. Apply constraints to ensure no conflicts
3. Optimize to maximize teacher free time (relative to their day start) while clustering Emma's activities
4. A class at 1pm for Alice has higher penalty than a class at 9am (both relative to her 9am start)
5. A class at 2pm for Bob on Tuesday has low penalty (it's the start of his day)
6. Convert window-relative times back to absolute times for the final schedule

---

## Future Enhancements

1. **Room/Equipment Constraints**: If specific equipment is needed
2. **Teacher Preferences**: Soft constraints for preferred teaching times
3. **Break Requirements**: Minimum gaps between consecutive classes
4. **Travel Time**: If students/teachers need to move between locations
5. **Fairness Constraints**: Balanced workload distribution among teachers
