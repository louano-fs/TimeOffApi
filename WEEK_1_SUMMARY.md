# Week 1 Summary — TimeOff API

## Summary

I completed the core Week 1 work for the TimeOff API by building the project foundation, applying asynchronous programming practices, and adding isolated and integration-level tests for the main time-off and time-clock workflows. The work is merged into `main`, and the solution builds successfully.

## What I completed

- Built the ASP.NET Core API with EF Core and SQLite, including the employee, time-off request, and time-log domain models.
- Added development seed data for approximately 50 employees and sample pending time-off requests.
- Used `Task`, `Task<T>`, and `await` throughout the service and data-access layers so asynchronous operations do not synchronously block request-processing threads.
- Implemented approve and reject workflows for time-off requests, including validation for missing requests and requests that are no longer pending.
- Added Moq-based unit tests for the approval and rejection service. These tests use a mocked repository and do not connect to a database.
- Added an SQLite integration test to verify that competing decisions cannot both update the same pending request.
- Expanded clock-in and clock-out coverage for seconds-level durations, overtime, employees without managers, overlapping sessions, inactive employees, breaks, deleted records, and invalid timestamps.
- Kept pull request descriptions and technical explanations in English and practiced point-first communication.

## What I learned

### `Task` and `Task<T>`

The main difference is that `Task` represents an asynchronous operation that completes without returning a value, while `Task<T>` represents an asynchronous operation that returns a value of type `T`. For example, saving a change may return `Task`, while retrieving a time-off request may return `Task<TimeOffRequest?>`.

### Why `.Result` blocks

`.Result` waits synchronously for an asynchronous operation to finish, so the current thread cannot do other work while it waits. This can reduce server scalability and may cause deadlocks in environments with a synchronization context. The correct pattern is to make the calling method asynchronous and use `await` through the full call chain.

### Mock data and a mocking framework

Mock data is sample input used by a test, such as a pending time-off request. A mocking framework such as Moq creates a controlled substitute for a dependency. It can define the dependency's behavior and verify how the code interacted with it. In the approval tests, the request is mock data, while the Moq repository is the mocked dependency.

## Testing and evidence

- The test project currently defines 25 test cases across the time-off decision and time-clock workflows.
- The solution compiles successfully with `dotnet test TimeOffApi.slnx --no-restore` before test execution begins.
- Repository: [TimeOffApi](https://github.com/louano-fs/TimeOffApi)
- Approval and rejection tests: [PR #2](https://github.com/louano-fs/TimeOffApi/pull/2)
- Clock-action edge-case tests: [PR #4](https://github.com/louano-fs/TimeOffApi/pull/4)
- Green test run evidence: **[Add screenshot or CI run link]**
- Seeded database evidence: **[Add screenshot link]**

## Challenges and how I handled them

The main challenge was separating isolated unit testing from database-backed testing. I addressed this by placing the approval and rejection rules behind a repository interface and mocking that interface with Moq. I used an in-memory SQLite database only where relational behavior mattered, such as verifying that competing decisions allow only the first pending-status transition.

I also had to cover clock actions beyond the happy path. I added focused cases for boundary conditions and domain conflicts so failures return specific error codes and do not leave partial changes in the database.

## Week 1 outcome

Week 1 improved my understanding of asynchronous C# code and the purpose of isolated tests. I can now explain why async code should use `await` instead of `.Result`, when to use `Task` versus `Task<T>`, and how a mocking framework differs from simply supplying test data. The TimeOff API now has a working foundation and broader automated coverage for its most important Week 1 workflows.

## Next steps

- Complete the Week 1 communication checkpoint and submit the five-minute video.
- Begin Week 2 by converting the provided JavaScript module to strict TypeScript.
- Practice interfaces, type aliases, discriminated unions, and narrowing.
- Continue using point-first English in updates, pull requests, and walkthroughs.

## Items to attach before submission

- Skill IQ baseline scores: **[C#: __ | TypeScript: __ | SQL Server Fundamentals: __ | EF Core: __]**
- Green build/test screenshot or CI link
- Seeded database screenshot
- Week 1 checkpoint video link
