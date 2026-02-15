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
   - Minimize makespan (teachers finish as early as possible)
   - Minimize student fragmentation (cluster each student's activities)
   - Prioritize younger students for earlier time slots

---

## Mathematical Formulation

### Parameters

| Symbol | Description |
|--------|-------------|
| D | Set of days of the week, indexed by d |
| T(d) | Set of teachers available on day d, indexed by t |
| A(d,t) | Set of available time windows for teacher t on day d, indexed by a |
| C | Set of non-fixed classes (flexible groups and solos), indexed by c |
| G | Set of fixed groups, indexed by g |
| H(g) | Start time of fixed group g |
| J(g) | End time of fixed group g |
| O(c) | Set of scheduling alternatives for class c, indexed by o |
| dur(c) | Duration of class c (in minutes) |
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
| start(c) | Integer >= 0 | Start time of class c (in minutes from day start) |
| end(c) | Integer >= 0 | End time of class c |
| p(c,o) | {0, 1} | Binary: 1 if alternative o is chosen for class c |
| makespan | Integer >= 0 | Maximum end time across all teachers/days |

### Objective Function

We minimize a weighted combination of objectives:

```
Minimize:  alpha * makespan  +  beta * SUM(StudentGap(s))  +  gamma * SUM(AgePenalty(c))
                                      for all s in S              for all c in C
```

Where:
- alpha: Weight for makespan minimization
- beta: Weight for student clustering
- gamma: Weight for age-based scheduling

**Student Gap Calculation:**

```
StudentGap(s) = SUM( Gap(c1, c2) )
                for all c1, c2 in C(s) UNION G(s) where c1 != c2
```

Where G(s) is the set of fixed groups that student s is enrolled in.

Gap(c1, c2) is:
- The time difference between classes if on the same day
- W_cross if on different days (high penalty to encourage same-day clustering)

**Age Penalty Calculation:**

```
AgePenalty(c) = (1 / min_age(c)) * start(c)
```

Where min_age(c) is the minimum age among students in class c. This gives younger students' classes a higher penalty for later start times.

---

## Constraints

### 1. Task Duration Constraint

For each class c:
```
end(c) = start(c) + dur(c)
```

The end time equals start time plus duration.

### 2. Alternative Selection Constraint

For each class c:
```
SUM( p(c,o) ) = 1    for all o in O(c)
```

Exactly one scheduling alternative must be chosen for each class.

### 3. Teacher Availability Constraint

For each class c and chosen alternative o:
```
start(c) >= A(d,t).start  AND  end(c) <= A(d,t).end
```

Where t = teacher(c,o) and d = day(c,o). The class must fit within the teacher's available time window.

### 4. No Overlap Constraint (Same Teacher)

For each pair of classes c1, c2 assigned to the same teacher on the same day:
```
NoOverlap(start(c1), end(c1), start(c2), end(c2))
```

A teacher cannot teach two classes simultaneously.

### 5. No Overlap Constraint (Same Student)

For each student s and pair of classes c1, c2 in C(s) on the same day:
```
NoOverlap(start(c1), end(c1), start(c2), end(c2))
```

A student cannot attend two classes simultaneously.

### 6. Student Availability Constraint

For each class c and student s enrolled in c:
```
For all u in U(s): NoOverlap(start(c), end(c), u.start, u.end)
```

Classes must not be scheduled during student unavailability windows.

### 7. Makespan Constraint

```
makespan >= end(c)    for all c in C
```

The makespan must be at least as large as the latest ending class.

---

## Implementation Notes

### Generating Alternatives

For each flexible class c, we generate alternatives based on:
1. Which teachers can teach the class
2. Which days those teachers are available
3. The available time windows on those days

Each valid (teacher, day, time-window) combination becomes an alternative o in O(c).

### Handling Discontinuous Availability

Teachers may have gaps in their availability (e.g., lunch breaks). We model this by:
1. Splitting availability into continuous windows
2. Each window becomes a separate constraint for fitting classes
3. Classes must fit entirely within one continuous window

### Pre-processing Fixed Groups

Before solving:
1. Identify all fixed groups
2. Subtract their time slots from teacher availability
3. This may split continuous windows into smaller segments

### Solver Strategy

We recommend using a constraint programming solver (e.g., Google OR-Tools CP-SAT) with:
1. Interval variables for class time slots
2. NoOverlap constraints for teacher and student conflicts
3. Optional intervals for alternative selection
4. Custom search strategy prioritizing:
   - Classes with fewer alternatives first
   - Students with more classes first
   - Younger students' classes first

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
- Alice has a fixed class Mon 12:00-13:00

The solver would:
1. Generate alternatives for each flexible class
2. Apply constraints to ensure no conflicts
3. Optimize to minimize makespan while clustering Emma's activities and giving her earlier slots due to younger age

---

## Future Enhancements

1. **Room/Equipment Constraints**: If specific equipment is needed
2. **Teacher Preferences**: Soft constraints for preferred teaching times
3. **Break Requirements**: Minimum gaps between consecutive classes
4. **Travel Time**: If students/teachers need to move between locations
5. **Fairness Constraints**: Balanced workload distribution among teachers
