# Course Waitlist & Registration Engine (.NET 10)

A bounded, thread-safe course registration service slice implementing atomic enrollments, cancellations/drops, and automated FIFO waitlist promotions with prerequisite validation.

---

## 1. Architectural Highlights

- **Clean Domain Boundary**: Encapsulates business invariants inside the `CourseOffering` aggregate root within `CourseEnrollment.Core`.
- **Thread Safety & Invariant Guarantees**: Enforces atomic check-and-mutate state transitions using C# `lock` synchronization over the aggregate root. Under high concurrency:
  - $\text{EnrolledCount} \le \text{Capacity}$ is never breached.
  - No duplicate enrollments or duplicate waitlist placements occur.
  - Read snapshots prevent enumeration corruption (`InvalidOperationException`).
- **Prerequisite Validation & Skip-and-Evict Policy**:
  - Requires all prerequisite courses before granting enrollment or queue placement.
  - When an enrolled learner drops, the system inspects the waitlist in strict FIFO order. If a candidate at the head is no longer eligible, they are cleanly evicted, and the engine evaluates the next candidate in line.
- **In-Memory Store**: Managed by `CourseOfferingRepository` registered as a Singleton to maintain state across HTTP requests without external database overhead.

---

## 2. Seed Data

The service automatically seeds an initial course offering upon startup:

| Field | Seed Value |
| :--- | :--- |
| **Course ID** | `AI-301` |
| **Course Title** | `Artificial Intelligence Foundations` |
| **Max Capacity** | `2` |
| **Required Prerequisites** | `["MATH201", "CS201"]` |

---

## 3. Project Structure

```text
CourseEnrollment/
├── src/
│   ├── CourseEnrollment.Core/         # Domain models, enums, interfaces, services, storage
│   └── CourseEnrollment.Api/          # ASP.NET Core REST API & Swagger UI
└── tests/
    └── CourseEnrollment.Tests/        # xUnit unit, lifecycle, and concurrency stress tests
```

---

## 4. Getting Started

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/)

### Build the Solution
```bash
dotnet build
```

### Run All Automated Tests
To run all 11 unit, promotion lifecycle, and high-concurrency stress tests:
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run the Web API
```bash
dotnet run --project src/CourseEnrollment.Api
```
The API starts locally. If using Swagger UI, open:
```text
https://localhost:<port>/swagger
```

---

## 5. API Reference & Verification Flow

### 1. Discover Active Courses
- **Endpoint**: `GET /api/courses`
- **Description**: Inspect all available courses, capacity limits, enrolled counts, and required prerequisites.

### 2. Enroll an Eligible Learner
- **Endpoint**: `POST /api/courses/AI-301/enroll`
- **Request Body**:
  ```json
  {
    "learnerId": "Learner_01",
    "completedPrerequisites": ["MATH201", "CS201"]
  }
  ```
- **Responses**:
  - `200 OK`: Enrolled or waitlisted (when at full capacity).
  - `400 Bad Request`: Missing prerequisite or invalid request.
  - `409 Conflict`: Duplicate enrollment or duplicate waitlist attempt.

### 3. Drop a Learner & Auto-Promote
- **Endpoint**: `DELETE /api/courses/AI-301/learners/{learnerId}`
- **Responses**:
  - `200 OK`: Learner removed. If a seat opened, details the automatically promoted learner ID.
  - `400 Bad Request`: Learner was not enrolled or waitlisted.

### 4. Inspect State & Waitlist Order
- **Endpoint**: `GET /api/courses/AI-301`
- **Description**: Returns real-time course telemetry, active seat allocation, enrolled learner IDs, and prioritized FIFO waitlist position entries.

---

## 6. Verification Test Scenarios

| Scenario | Steps | Expected Outcome |
| :--- | :--- | :--- |
| **Prerequisite Rejection** | Submit `POST /api/courses/AI-301/enroll` without `"CS201"`. | `400 Bad Request` (`FailedPrerequisite`). |
| **Normal Enrollment** | Submit two valid requests for `Learner_01` and `Learner_02`. | Both return `200 OK` (`Enrolled`). Available seats reach `0`. |
| **Duplicate Guard** | Resubmit enrollment for `Learner_01`. | `409 Conflict` (`DuplicateRequest`). State unmodified. |
| **Waitlist Overflow** | Enroll `Learner_03` with valid prerequisites. | `200 OK` (`Waitlisted`), placed at Position 1. |
| **FIFO Auto-Promotion** | Submit `DELETE /api/courses/AI-301/learners/Learner_01`. | `Learner_03` is promoted to enrolled status automatically. |
| **Concurrency Stress** | Execute `dotnet test --filter ConcurrencyTests`. | 40 parallel requests never exceed capacity limit. |