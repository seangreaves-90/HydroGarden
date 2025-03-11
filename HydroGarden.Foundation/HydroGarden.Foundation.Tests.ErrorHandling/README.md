# HydroGarden Error Handling Test Suite

This test suite provides comprehensive unit tests for the error handling components in the HydroGarden system. The tests focus on verifying the correct functioning of error detection, classification, and recovery mechanisms.

## Test Coverage Overview

The test suite covers the following key areas:

### Core Error Components
- **ComponentError**: Tests for error classification, recovery tracking, and context enrichment
- **ErrorContextBuilder**: Tests for building rich error context with diagnostic information
- **ErrorMonitorBase**: Tests for error tracking, statistics, and recovery registration

### Error Classification
- **ErrorCodes**: Tests for error code definitions and unrecoverable error detection
- **ErrorTaxonomy**: Tests for error classification, root cause analysis, and impact assessment

### Recovery Mechanisms
- **RecoveryStrategyBase**: Tests for the base recovery strategy functionality and backoff mechanisms
- **RecoveryOrchestrator**: Tests for orchestrating multiple recovery strategies with proper ordering

### Specific Recovery Strategies
- **RestartComponentStrategy**: Tests for device restart recovery logic
- **ReinitializeConfigurationStrategy**: Tests for configuration reset recovery
- **CommunicationRecoveryStrategy**: Tests for communication recovery operations
- **CircuitBreakerRecoveryStrategy**: Tests for circuit breaker reset operations

### Error-Related Events
- **ErrorRelatedEvents**: Tests for error event creation and publishing

### Exceptions
- **CircuitBreakerExceptions**: Tests for circuit breaker exception handling

## Test Approach

The tests follow these principles:

1. **Isolated Testing**: Each component is tested in isolation with dependencies mocked
2. **Comprehensive Coverage**: Tests verify both positive and negative paths
3. **Edge Cases**: Tests include handling of null values, exceptions, and boundary conditions
4. **Behavioral Verification**: Tests verify the behavior, not just the implementation

## Test Categories

The test suite is organized into the following categories:

- **Component Tests**: Test individual error handling components
- **Recovery Strategy Tests**: Test specific recovery strategies
- **Exception Tests**: Test exception handling and custom exceptions
- **Event Tests**: Test error event creation and handling

## Note on Integration Tests

Integration tests are intentionally excluded from this test suite due to upcoming architectural changes. The focus is solely on unit tests that verify the correct functioning of individual components.

## Running the Tests

The tests can be run using the standard .NET test runner:

```bash
dotnet test HydroGarden.Foundation.Tests.ErrorHandling.csproj
```

Or using Visual Studio's Test Explorer.
