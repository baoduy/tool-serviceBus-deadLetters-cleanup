<!--
SYNC IMPACT REPORT - Constitution v1.0.0

VERSION CHANGE: Template (Unpopulated) → v1.0.0
  - Bump Type: MINOR (new constitution created for existing project)
  - Rationale: Formalizing governance for ServiceBusDeadLettersCleanup based on existing codebase patterns

NEW PRINCIPLES:
  - I. Async-First Architecture (derived from QueueCleanupService, SubscriptionCleanupService, BackgroundService pattern)
  - II. Configuration-Driven Design (derived from IOptions<BusConfig>, IOptions<StorageConfig> pattern)
  - III. Separation of Service Concerns (derived from dual cleanup services architecture)
  - IV. Observable Error Handling (derived from error event handlers in existing code)
  - V. Resource Management & Cleanup (derived from IAsyncDisposable and Azure SDK patterns)

NEW SECTIONS:
  - Technology Stack & Constraints (captured from README, project file, Dockerfile)
  - Development Workflow (feature branches, code review, testing, documentation standards)
  - Governance (amendment procedures, version control expectations)

TEMPLATE UPDATES:
  - plan-template.md: ✅ No changes needed (already generic enough)
  - spec-template.md: ✅ No changes needed (already generic enough)
  - tasks-template.md: ✅ No changes needed (already generic enough)
  - checklist-template.md: ⚠ Manual review suggested (not accessed yet)

RATIFICATION:
  - Date: 2026-01-23
  - Author: GitHub Copilot
  - Applied to: ServiceBusDeadLettersCleanup project

TODO ITEMS:
  - None deferred; all placeholders filled with project-derived values
  - Recommended: Schedule constitution review at next architecture meeting
-->

# ServiceBusDeadLettersCleanup Constitution

## Core Principles

### I. Async-First Architecture

All I/O operations (Service Bus, Blob Storage, administration calls) MUST use async/await patterns. No blocking calls allowed. Background services MUST implement `BackgroundService` and `IAsyncDisposable` for proper lifecycle management and resource cleanup.

### II. Configuration-Driven Design

Application behavior MUST be driven by `appsettings.json` and `IOptions<T>` pattern. Connection strings, container names, and service configurations must never be hardcoded. Configuration classes MUST be validated at startup via dependency injection and configuration binding.

### III. Separation of Service Concerns

Clean separation between Service Bus operations and Storage operations through dedicated cleanup services. Each service (QueueCleanupService, SubscriptionCleanupService) handles one specific dead-letter queue type. Shared utilities (Extensions, Message serialization) must not contain business logic.

### IV. Observable Error Handling

All error paths MUST log via Console or structured logging. Processors MUST implement error event handlers that capture and report exceptions without crashing the service. Error recovery strategies (e.g., removing failed processors) MUST be implemented rather than silently failing.

### V. Resource Management & Cleanup

All Azure SDK clients (ServiceBusClient, BlobContainerClient, ServiceBusAdministrationClient) MUST be properly disposed. Services MUST implement disposal patterns and gracefully handle CancellationTokens on shutdown. Blob containers MUST be created idempotently with `CreateIfNotExistsAsync`.

## Technology Stack & Constraints

- **Language**: C# (.NET)
- **Target Frameworks**: .NET 8.0+ (with backward compatibility where feasible)
- **Azure SDK**: Azure.Messaging.ServiceBus, Azure.Storage.Blobs
- **Configuration**: Microsoft.Extensions.Options pattern
- **Hosting**: ASP.NET Core `WebApplication` with background services
- **Deployment**: Docker containerization required (Dockerfile provided)
- **Environment Separation**: Support Development and Production configuration profiles

## Development Workflow

- **Feature Branches**: Named as `NNN-feature-description` (e.g., `001-add-metrics`)
- **Code Review**: All PRs MUST verify compliance with principles above
- **Testing**: Unit tests required for service logic and error scenarios; integration tests for Azure SDK interactions
- **Configuration**: All changes to default `appsettings.json` MUST document required environment variables/connection strings
- **Documentation**: Inline XML comments on public methods; README updated when features or configuration options change

## Governance

This constitution defines non-negotiable development standards for the ServiceBusDeadLettersCleanup project. All contributors MUST:

1. Review this constitution before making architectural changes
2. Justify any deviations in pull request description with explicit approval required
3. Update constitution if new principles or constraints are discovered through development
4. Ensure backward compatibility or document breaking changes in version history

The constitution supersedes informal guidelines. Changes require git commit history and MUST increment semantic version.

**Version**: 1.0.0 | **Ratified**: 2026-01-23 | **Last Amended**: 2026-01-23
