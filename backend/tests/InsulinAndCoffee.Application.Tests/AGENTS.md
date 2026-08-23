# AGENTS.md

## Test project conventions

- Use xUnit with `Fact`, `Theory`, and `InlineData` where appropriate.
- Use xUnit `Assert` rather than introducing FluentAssertions, Shouldly, or another assertion library.
- Name tests using `MethodName_StateUnderTest_ExpectedBehavior`.
- Keep a clear Arrange / Act / Assert structure without requiring explicit AAA comments.
- Construct the service under test directly unless existing test infrastructure provides another established pattern.
- Use the real `AppDbContext` with EF Core InMemory and a unique database name per test for application-service persistence tests.
- Do not introduce Moq, NSubstitute, or another mocking framework when the existing InMemory `AppDbContext` pattern is sufficient.
- Use `WebApplicationFactory<Program>` with EF Core InMemory for API contract tests.
- Use `FixedTimeProvider` when behavior depends on deterministic current time or local-day boundaries.
- Use `TimeProvider.System` when the tested behavior does not depend on deterministic time.
- Async tests must return `Task`.
- Service calls should pass `CancellationToken.None` explicitly.
- Dispose test `AppDbContext` instances with `await using`.
- Use decimal literals with the `m` suffix for calculation-related values.
- Assert exact decimal and rounding behavior in calculation tests.
- Preserve the distinction between `null` `ConfirmedBolus` and explicit `0m` `ConfirmedBolus` in confirmation-state tests.
- When persistence integrity matters, assert both the returned result and the persisted database state.
- API error contract tests must verify the established ProblemDetails `status`, `title`, `detail`, and `traceId` contract.
