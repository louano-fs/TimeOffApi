# Week 1 Checkpoint Video Script

**Target length:** Approximately 5 minutes  
**Speaking style:** Give the answer first, then the reason, then an example.

> Before recording, replace the bracketed test-status sentence with your verified result.

## 0:00–0:20 — Introduction

Hello, I’m Luis Andrei Ouano. My main Week 1 outcome was completing the foundation of the TimeOff API while improving my understanding of asynchronous C# and automated testing.

I implemented the main workflows, added isolated and database-backed tests, and merged the completed work into the main branch.

## 0:20–1:35 — Week 1 Summary

First, I built the API using ASP.NET Core, Entity Framework Core, and SQLite. I added the main domain models and development seed data containing approximately 50 employees and several pending time-off requests.

Second, I implemented time-off approval and rejection. The service confirms that a request exists and is still pending before changing its status. Competing decisions allow only the first valid change to succeed.

Third, I used Moq to isolate the time-off service from its repository. I used an in-memory SQLite database separately when relational behavior was important.

Finally, I expanded the clock tests to cover seconds-level durations, overtime, employees without managers, overlapping sessions, inactive employees, breaks, deleted records, and invalid timestamps.

The project currently contains 25 automated test cases. **[After verifying your final run, say either: “All 25 test cases are passing.” or briefly explain the remaining blocker.]**

My biggest learning was understanding why asynchronous and testable code matters, not only how to write it.

## 1:35–2:35 — Question 1: What is the difference between `Task` and `Task<T>`?

The short answer is that `Task` represents an asynchronous operation that does not return a value, while `Task<T>` represents an asynchronous operation that returns a value of type `T`.

For example, a method that only saves changes may return `Task` because the caller only needs to know when it finishes. A method that retrieves a time-off request may return `Task<TimeOffRequest>` because the caller needs the request afterward.

When I use `await` with a `Task`, the method continues after the operation completes. When I await a `Task<T>`, I also receive a result of type `T`.

## 2:35–3:35 — Question 2: Why does `.Result` block, and what should be used instead?

The short answer is that `.Result` waits synchronously, which blocks the current thread until the asynchronous operation finishes. The correct pattern is to use `await` and keep the full call chain asynchronous.

This matters in ASP.NET Core because a blocked request thread cannot process other work while it waits. Under load, repeated blocking reduces scalability. In some application environments, blocking can also contribute to a deadlock.

For example, instead of writing `var request = GetRequestAsync().Result`, I should make the calling method asynchronous and write `var request = await GetRequestAsync()`.

Using `await` frees the thread to perform other work. The method continues after the operation finishes.

## 3:35–4:35 — Question 3: What is the difference between mock data and a mocking framework?

The short answer is that mock data is sample information passed into a test, while a mocking framework creates a controlled substitute for a dependency.

For example, a pending time-off request with sample dates is mock data. It gives the test a known input, but it does not replace a dependency.

Moq is a mocking framework. I used it to substitute for the time-off request repository, return a specific request, and verify that the status-change method was called correctly.

This let me test service rules without a real database, keeping the tests isolated and focused. I used SQLite separately for integration tests that needed actual relational behavior.

## 4:35–5:00 — Closing

To summarize, Week 1 gave me a stronger foundation in asynchronous C# and automated testing. I can explain when to use `Task` or `Task<T>`, why `await` is preferred over `.Result`, and how mocking differs from supplying test data.

For Week 2, I will focus on strict TypeScript, union types, narrowing, SQL analysis, and reproducible performance measurement.

Thank you.

## Quick Recording Reminders

- Speak slightly slower than normal and pause briefly between sections.
- Look at the camera when giving the first sentence of each answer.
- Do not read the headings or timestamps aloud.
- Emphasize the answer-first sentence, then explain the reason and example.
- Keep the repository or test results ready in case you want to show evidence on screen.
