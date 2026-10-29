# Technical Limitations and Production Considerations

## 1. Storage & Scalability

### Single-Process In-Memory State
- **Current Behavior**: State is managed in-memory via `CourseOfferingRepository` using a `ConcurrentDictionary<string, CourseOffering>`.
- **Limitation**: Course offering states, active enrollments, and waitlist queues do not survive application restarts or container terminations.
- **Production Path**: Transition the persistence layer to a distributed ACID-compliant datastore (such as PostgreSQL or SQL Server). Maintain concurrency guarantees using pessimistic locking (`SELECT ... FOR UPDATE`) or transactional row versioning with optimistic concurrency tokens.

### Horizontal Scaling & Node Affinity
- **Current Behavior**: Concurrency synchronization relies on an in-memory monitor lock (`Monitor.Enter` / `lock (course.SyncLock)`) localized to an individual runtime process.
- **Limitation**: Running multiple instances of the API behind a load balancer will result in partitioned state across nodes, allowing race conditions and over-enrollment beyond the maximum capacity limit.
- **Production Path**: Implement a distributed locking mechanism (e.g., Redis via RedLock) keyed by `courseId`, or use a single-writer partition pattern (such as an actor framework like Akka.NET or Orleans) to enforce strict aggregate concurrency across a cluster.

---

## 2. Business Domain & Queue Operations

### Synchronous Prerequisite Validation
- **Current Behavior**: Prerequisite checks are performed synchronously in memory (`HashSet<string>.IsSupersetOf`) during enrollment and waitlist promotion.
- **Limitation**: In enterprise academic/training platforms, prerequisites often involve external API calls, transcript verification, or degree-audit systems. Holding synchronization locks across external network boundaries would cause thread starvation and degraded throughput.
- **Production Path**: Introduce an asynchronous outbox/event-driven pipeline. When an enrolled seat opens, the system reserves the seat tentatively and publishes a `PromotionCandidateSelected` event. The learner is given a time-to-live (TTL) claim window (e.g., 24 hours) to verify prerequisites and confirm their seat.

### Unbounded Waitlist Capacity
- **Current Behavior**: The waitlist queue expands dynamically without an upper bound.
- **Limitation**: Memory consumption can grow unbounded during high-demand enrollment surges, and learners deep in the queue have a near-zero statistical probability of admission.
- **Production Path**: Introduce a configurable bounded limit (`MaxWaitlistCapacity`) to reject waitlist additions when the queue exceeds operational viability.

### Lack of Priority Queuing
- **Current Behavior**: The waitlist strictly enforces first-in, first-out (FIFO) queuing based on registration timestamp.
- **Limitation**: Does not accommodate priority tiers (e.g., graduating seniors, honors students, or departmental majors) who require priority placement ahead of general applicants.
- **Production Path**: Replace the standard FIFO collection with a priority queue supporting weighted tiers while retaining FIFO order within each tier.

---

## 3. Security, Authentication & Observability

### Absence of Authentication & Authorization
- **Current Behavior**: Any client can trigger enrollment, drops, or catalog reads by supplying arbitrary `learnerId` values in the payload or route.
- **Limitation**: No protection against unauthorized drops or impersonation attacks.
- **Production Path**: Implement JWT bearer authentication (e.g., via Microsoft Entra ID or OpenID Connect) and authorize drop operations only if the authenticated user's claims match the targeted `learnerId` or an administrator role.

### Telemetry & Audit Trails
- **Current Behavior**: State transitions (e.g., automated promotions, prerequisite-based evictions) return results within the HTTP response lifecycle without persistent audit trails.
- **Limitation**: Historical capacity trends and promotion forensics cannot be reconstructed after the fact.
- **Production Path**: Integrate OpenTelemetry for distributed tracing and emit structured domain events (`LearnerEnrolledEvent`, `LearnerWaitlistEvictedEvent`, `WaitlistPromotedEvent`) to a centralized event store (e.g., Kafka or Azure Event Hubs).