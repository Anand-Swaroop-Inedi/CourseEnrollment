# AI Prompt & Collaboration Log

This log documents the iterative engineering prompts used during the design, architecture, implementation, refactoring, and hardening of the Course Registration & Waitlist engine, detailing AI suggestions, human architectural decisions, and verification methods.

---

### Entry 1: Architecture & Project Scaffolding
* **User Prompt**:  
  > *"I'm building a clean, bounded course-waitlist engine in .NET 10 without external database dependencies. Help me scaffold a clean solution structure separating domain models, API endpoints, and automated tests. Let's make sure the core domain has zero framework dependencies so it stays completely testable."*
* **AI Output**:  
  * Recommended a 3-project Clean Architecture layout: `CourseEnrollment.Core` (Class Library), `CourseEnrollment.Api` (ASP.NET Core Web API), and `CourseEnrollment.Tests` (xUnit).
  * Provided initial project files and folder structure.
* **Engineering Decision & Action Taken**:  
  * **Accepted**. Kept `Core` isolated from ASP.NET Core dependencies. Discarded unnecessary generic repository wrappers and focused strictly on the `CourseOffering` aggregate root.
* **Verification Method**:  
  * Executed `dotnet build` from root; confirmed solution built cleanly with zero dependencies on third-party ORMs or framework-heavy libraries.

---

### Entry 2: Aggregate Root Thread Safety & TOCTOU Race Condition Prevention
* **User Prompt**:  
  > *"Take a look at how CourseOffering and CourseEnrollmentService interact. If multiple HTTP threads try to enroll or drop at the exact same millisecond, how do we guarantee EnrolledCount never breaches Capacity? Should the lock live inside the collection methods or wrap the whole service workflow?"*
* **AI Output**:  
  * Suggested placing locks exclusively inside `AddEnrolled()` and `RemoveEnrolled()`.
  * Explained .NET `Monitor` reentrancy if a coarse lock was chosen.
* **Engineering Decision & Action Taken**:  
  * **Rejected the fine-grained-only approach & refined**. Identified a classic Time-of-Check to Time-of-Use (TOCTOU) race condition: two threads checking capacity simultaneously before calling `AddEnrolled()` would both pass and over-enroll.
  * Implemented a coarse-grained `lock (course.SyncLock)` in `CourseEnrollmentService` spanning the entire validation-to-mutation transaction, while preserving inner defensive locks and snapshotting (`.ToList()`) on `CourseOffering` properties.
* **Verification Method**:  
  * Authored `ConcurrencyTests.cs` using a `TaskCompletionSource` trigger to fire 40 simultaneous enrollment tasks against a 5-capacity course. Confirmed `EnrolledCount` remained strictly at 5 with the rest queued in FIFO order.

---

### Entry 3: Ineligible Waitlist Candidate Policy ("Skip-and-Evict")
* **User Prompt**:  
  > *"When an enrolled student drops, a seat frees up for the waitlist. But what happens if the student at the very front of the FIFO waitlist no longer qualifies for the prerequisites? Do we skip them and keep them in line, or kick them out? Give me the cleanest domain approach to avoid queue deadlocks."*
* **AI Output**:  
  * Outlined two alternatives: "Skip-and-Retain" (leave them at position 1 and check position 2) vs. "Skip-and-Evict" (dequeue and discard the invalid entry, then inspect the next).
* **Engineering Decision & Action Taken**:  
  * **Adopted Skip-and-Evict**. Decided that keeping an unqualified student at the head creates head-of-line blocking and compounds latency on every subsequent drop. Implemented a `while` loop promoting the first candidate satisfying `learner.CompletedPrerequisites.IsSupersetOf(...)` and removing unqualified candidates.
* **Verification Method**:  
  * Wrote `Drop_WhenWaitlistHeadIsIneligible_EvictsHeadAndPromotesNextEligible` unit test. Verified that the ineligible waiter was removed, the subsequent eligible candidate was promoted to seat 1, and no queue corruption occurred.

---

### Entry 4: RESTful Endpoint Modeling & HTTP Semantics
* **User Prompt**:  
  > *"Let's design CoursesController. I want clean REST patterns rather than generic RPC verbs. How should we structure the drop endpoint and map domain results so the client gets proper status codes for duplicates, prerequisite failures, and not-found courses?"*
* **AI Output**:  
  * Suggested `POST /api/courses/drop` accepting a JSON body containing `courseId` and `learnerId`, returning `400 Bad Request` for all errors.
* **Engineering Decision & Action Taken**:  
  * **Refactored to REST standards**. Changed the route to `DELETE /api/courses/{courseId}/learners/{learnerId}` using route parameters. 
  * Replaced blanket `400` errors with semantic HTTP codes: `200 OK` (success/drop/waitlisted), `404 Not Found` (invalid course), `409 Conflict` (duplicate enrollment/waitlist), and `400 Bad Request` (prerequisite failure / invalid state).
* **Verification Method**:  
  * Tested endpoints in Swagger UI and verified assertions in unit tests checking status code mappings.

---

### Entry 5: Catalog Observability for Evaluators
* **User Prompt**:  
  > *"When an evaluator tests our API via Swagger, how will they know what courses exist or what prerequisite strings to pass without digging into C# code? Can we add a discovery endpoint?"*
* **AI Output**:  
  * Suggested adding `GET /api/courses` to expose all seeded offerings alongside their capacity, seat availability, and prerequisite list.
* **Engineering Decision & Action Taken**:  
  * **Accepted**. Added `GetAllCoursesAsync` across `ICourseOfferingRepository`, `ICourseEnrollmentService`, and `CoursesController`. Seeded the catalog with realistic identifiers: `AI-301` ("Artificial Intelligence Foundations") requiring `MATH201` and `CS201`.
* **Verification Method**:  
  * Tested `GET /api/courses` in Swagger UI; verified accurate reporting of capacity, seat delta, and prerequisite codes.

---

### Entry 6: Concurrency Test Race Hardening
* **User Prompt**:  
  > *"In our ConcurrencyTests, Enumerable.Select with Task.Run is starting tasks sequentially rather than hitting the service all at the exact same millisecond. How do we ensure true concurrent execution to prove our lock actually holds up under real thread contention?"*
* **AI Output**:  
  * Recommended using `TaskCompletionSource` (or `ManualResetEventSlim`) to halt all worker tasks until all threads are created, then releasing them simultaneously via `SetResult()`.
* **Engineering Decision & Action Taken**:  
  * **Accepted and applied**. Wrapped parallel enrollment and drop tasks with a shared `TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)`.
* **Verification Method**:  
  * Ran `dotnet test` with detailed verbosity; verified that all 40 parallel tasks executed concurrently without race conditions, deadlocks, or capacity over-allocation.

---

### Entry 7: Storage Architecture & Repository Naming
* **User Prompt**:  
  > *"I don't like the name InMemoryCourseRepository—it feels like mock test code rather than enterprise architecture. What's a better name that follows Domain-Driven Design without leaking that it's in-memory?"*
* **AI Output**:  
  * Proposed three naming options: `CourseStore`, `CourseOfferingRepository`, and `LocalCourseRepository`.
* **Engineering Decision & Action Taken**:  
  * **Selected `CourseOfferingRepository` and `ICourseOfferingRepository`**. Aligned repository naming directly with the aggregate root (`CourseOffering`), treating in-memory storage as an internal implementation detail rather than an interface moniker. Added `ArgumentNullException.ThrowIfNull` guards and whitespace checks.
* **Verification Method**:  
  * Updated dependency injection in `Program.cs` and constructor injection in `CourseEnrollmentService`. Verified clean build and execution.

---

### Entry 8: Domain Model Separation: Course Identity vs Display Title
* **User Prompt**:  
  > *"In CourseOffering, why are we using courseId for everything? Shouldn't a real university offering have both a machine code like AI-301 and a readable title like 'Artificial Intelligence Foundations'?"*
* **AI Output**:  
  * Outlined how adding `string title` impacts constructor invariants, public read properties, and API serializations.
* **Engineering Decision & Action Taken**:  
  * **Accepted & Implemented**. Extended `CourseOffering` constructor to require `title`, enforcing `ArgumentException.ThrowIfNullOrWhiteSpace`. Updated API responses in `CoursesController` so consumers see both `courseId` and `title`.
* **Verification Method**:  
  * Updated seed data and unit test setups; verified that `GET /api/courses` returns clear, structured metadata.

---

### Entry 9: Swashbuckle Dependency and Swagger 404 Troubleshooting
* **User Prompt**:  
  > *"I hit compile errors on AddSwaggerGen and UseSwagger*
* **AI Output**:  
  * Explained that `Swashbuckle.AspNetCore` was missing from `.Api.csproj`.
* **Engineering Decision & Action Taken**:  
  * **Configured developer experience**. Installed `Swashbuckle.AspNetCore` and set `"launchUrl": "swagger"` in `launchSettings.json` to navigate directly to the interactive documentation on start.
* **Verification Method**:  
  * Launched the API via Visual Studio; verified that the browser loaded Swagger UI directly with full documentation for `/enroll`, `/drop`, and `/courses`.

---