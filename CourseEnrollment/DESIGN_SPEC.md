# Architectural Design Specification: Course Registration & Waitlist Slice

## 1. System Boundary and Scope

This service models a single bounded slice of a university or enterprise learning platform responsible for course registration, real-time capacity management, and automated FIFO waitlist promotion.

### Explicit Boundary Exclusions
- **Identity & Access Management**: Authentication, JWT token verification, and role-based permissions are omitted; requests supply raw `learnerId` strings.
- **External Persistent Storage**: Database engines (e.g., PostgreSQL, SQL Server) and distributed caches are excluded in favor of an atomic, thread-safe in-memory store.
- **Tuition & Payment Workflows**: Billing, payment gateways, and enrollment holds are treated as out-of-scope upstream concerns.

---

## 2. Core Invariants and Correctness Guarantees

The engine enforces four invariants under all execution scenarios, including high concurrent load:

1. **Capacity Boundary**:
   $$\text{EnrolledCount} \le \text{Capacity}$$
   The active enrollment count must never exceed the declared seat limit under any interleaved execution of enrollments and drops.

2. **Mutual Exclusivity**:
   A single `learnerId` cannot exist in both the `EnrolledLearners` set and the `Waitlist` queue simultaneously.

3. **Request Idempotency & Duplicate Guards**:
   - Submitting an enrollment for an already enrolled learner returns a duplicate failure status without altering counts or seat allocations.
   - Submitting an enrollment for an already waitlisted learner does not append a duplicate entry or modify queue position.

4. **Deterministic FIFO Promotion**:
   When an enrolled seat is vacated, the earliest waitlisted learner who satisfies all prerequisites must be awarded the open seat.

---

## 3. Prerequisite & Waitlist Policies

### Policy Selected: Skip-and-Evict
During waitlist auto-promotion, the engine executes a **Skip-and-Evict** strategy:

- **Mechanics**:
  1. An enrolled learner drops, freeing a seat.
  2. The engine inspects the learner at the head of the waitlist queue.
  3. If the learner satisfies the course prerequisites, they are dequeued and added to `EnrolledLearners`.
  4. If the learner **fails** the prerequisite check (e.g., academic prerequisites changed or were retroactively modified), the learner is **evicted** from the waitlist entirely.
  5. The engine continues inspecting subsequent queue entries in FIFO order until a qualified candidate is found and enrolled, or the waitlist is exhausted.
- **Rationale**:
  Retaining ineligible candidates at the head of a FIFO queue causes indefinite head-of-line blocking for all qualified candidates behind them. Skipping and retaining would degrade time complexity on every subsequent drop. Immediate eviction ensures queue freshness and bounded operational latency.

---

## 4. Concurrency Model & Synchronization Architecture

Incoming Requests (Parallel Threads)
│
▼
[ CoursesController ]
│
▼
[ CourseEnrollmentService ]  ──> Acquires lock (course.SyncLock)
│                     │
│                     ├─ Evaluates Invariants (Capacity, Duplicates, Prereqs)
│                     ├─ Mutates Aggregate State (Add/Remove Enrolled, Dequeue)
│                     └─ Re-entrant lock on CourseOffering internal sync object
│
▼
Releases SyncLock & Returns

### Aggregate-Level Locking
- Synchronization is scoped to each `CourseOffering` aggregate root via a private synchronization object (`SyncLock`).
- Operations on different courses run completely in parallel without contention.
- Operations on the same course acquire exclusive execution rights during state evaluation and mutation.

### Reentrant Thread Safety
- The Service layer acquires `lock (course.SyncLock)` across the entire composite workflow (validate prerequisites -> evaluate capacity -> mutate state) to eliminate Time-of-Check to Time-of-Use (TOCTOU) race conditions.
- Lower-level collection methods inside `CourseOffering` also acquire `lock (_syncLock)`. Because .NET `Monitor` locks are reentrant for the owning thread, inner locks resolve instantaneously without deadlocking while safeguarding direct property access.

### Defensive Snapshotting
- State-querying properties (`EnrolledLearners`, `Waitlist`) return defensive copies (`.ToList()`) inside synchronized blocks.
- This prevents callers or serialization engines from triggering `InvalidOperationException` (collection modified during enumeration) if background threads mutate state during read execution.

---

## 5. Domain Model & Data Structures

| Entity / Model | Storage Type | Purpose | Concurrency Property |
| :--- | :--- | :--- | :--- |
| `CourseOffering` | Aggregate Root | Encapsulates capacity limits, enrolled seats, and waitlist queue. | Coarse-grained `SyncLock`. |
| `_enrolledLearners` | `HashSet<string>` | O(1) set tracking unique enrolled learner IDs. | Case-insensitive ordinal comparison. |
| `_waitlist` | `List<WaitlistEntry>` | Ordered sequence maintaining timestamped queue positions. | FIFO order preserved via indexed queue operations. |
| `_requiredPrerequisites` | `HashSet<string>` | Canonical set of required course IDs. | Immutable after course creation. |
| `Learner` | Domain Model | Encapsulates learner ID and completed prerequisite course IDs. | Read-only set operations (`IsSupersetOf`). |

---

## 6. HTTP API Status Mapping

| Domain Status | HTTP Status Code | Scenario |
| :--- | :--- | :--- |
| `Enrolled` | `200 OK` | Learner successfully enrolled into an open seat. |
| `Waitlisted` | `200 OK` | Course at capacity; learner appended to FIFO queue. |
| `Dropped` | `200 OK` | Learner removed; promotion evaluated and executed if applicable. |
| `CourseNotFound` | `404 Not Found` | Requested `courseId` does not exist in repository. |
| `DuplicateRequest` | `409 Conflict` | Learner is already enrolled or already on waitlist. |
| `FailedPrerequisite` | `400 Bad Request` | Learner missing one or more required prerequisite courses. |
| `NotEnrolled` | `400 Bad Request` | Attempted to drop a learner not actively enrolled or waitlisted. |