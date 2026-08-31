# Xcaciv.Command.Packages Constitution

## Core Principles

### I. Security-First (NON-NEGOTIABLE)

All code must be securable and resilient. Security is a dynamic process, not a static state. Package management operations must never introduce vulnerabilities into host systems.

Core requirements:

- HTTPS-only package sources with valid SSL certificates (allow self-signed cert override with explicit configuration)
- All inputs validated and sanitized before processing
- Derived Integrity Principle: server-side values are authoritative; client intent must not dictate facts
- Canonical Input Handling: narrow data types, normalization, sanitization, validation (allow explicit values, never reject patterns)
- No hardcoded secrets; use Windows Credential Manager or environment variables
- Fail safely without leaking sensitive information in error messages
- Strong typing and immutable data structures where appropriate
- Transparency: all errors logged appropriately; system behavior auditable

### II. Framework Integration

Seamless integration with Xcaciv.Command framework is mandatory. This library exists to extend command functionality through package management.

Requirements:

- Implement ICommandDelegate interface for all commands
- Use CommandRoot attribute to group package commands
- Use CommandRegister to expose commands (Search, Install, Remove, List, Update)
- Define parameters using CommandParameterOrdered and CommandParameterNamed
- Support CommandFlag for boolean options
- Accept IEnvironmentContext for configuration
- Commands immediately available after package installation

### III. Standalone Library Architecture

This library must be self-contained and independently releasable. Minimize dependencies on Xcaciv.Cupcake.Core for broader applicability.

Design goals:

- Independent release cadence from Xcaciv.Cupcake
- Reusable by other projects using Xcaciv.Command framework
- Clear API boundaries with minimal external dependencies
- NuGet package distribution for easy consumption
- Modular, loosely coupled, highly cohesive code
- One class per file; file name and path match class name

### IV. Command Package Validation (CRITICAL)

Installed packages must contain valid Xcaciv.Command implementations. Packages failing validation are automatically removed.

Validation process:

- After extraction, scan assemblies for reference to Xcaciv.Command.Interface
- Verify at least one type implements ICommandDelegate
- If validation fails, remove package files and display clear error message
- Log validation outcomes for auditability
- Apply validation consistently for all install/update operations
- Rollback failed installations completely

### V. Performance & Reliability

Operations must be fast, reliable, and resilient to failures. Users expect package management to work consistently.

Standards:

- All network operations async/await
- Package search < 3 seconds average
- Package installation < 30 seconds (typical package)
- Memory usage during installation < 50MB
- Streaming downloads for large packages
- Timeout handling for unresponsive sources (default: 30 seconds)
- Graceful error handling with retry logic for transient failures
- Cancellation support for long-running operations
- Installation rollback on failure

## Security Requirements

### Input Validation & Sanitization

All inputs are potential attack vectors. Rigorous validation at all boundaries is mandatory.

Requirements:

- Validate package names, search terms, URLs before processing
- Sanitize search terms to prevent injection attacks
- Clamp result limits (max: 100) to prevent abuse
- Validate package source URLs (HTTPS required, valid format)
- Use parameterized queries and safe APIs
- Prefer narrow data types (enums, booleans over strings)
- Never trust client-provided values for critical operations

### Configuration & Credentials

Sensitive data must be protected. Configuration must be secure and isolated.

Requirements:

- Library-specific config file (Xcaciv.Command.Packages.config.json) in user-space
- Never store credentials in config files
- Use Windows Credential Manager or environment variables for secrets
- Config file contains non-secret metadata only (source URLs, preferences)
- Environment variables override file settings
- Validate all configuration values before use
- Support multiple package sources with precedence

### Error Handling & Logging

Errors must be handled gracefully without exposing sensitive information. Logging must support troubleshooting.

Requirements:

- User-friendly error messages without technical details
- Detailed errors logged for administrators (structured logging)
- Network errors handled without exposing internal state
- Distinguish timeout errors from other network failures
- Audit logs for security-relevant events (source changes, installations)
- Transparent: never silently swallow exceptions (except at UI boundary)

### External Dependencies Policy

Control over external dependencies ensures security and maintainability.

Requirements:

- AllowExternalDependencies setting defaults to false (strict mode)
- When disabled, reject packages with external NuGet dependencies
- When enabled, require explicit user acknowledgment during installation
- Document security implications of allowing external dependencies
- Audit logs record all installations involving external dependencies
- Validate dependency integrity during installation

## Development Standards

### Code Quality & Maintainability

Code must be analyzable, modifiable, and testable (SSEM Maintainability attribute).

Standards:

- Simple, readable code with low cyclomatic complexity
- Clear, consistent naming conventions (no "Helper" or "Utils" classes)
- PascalCase for public members; camelCase for private fields (no underscores)
- Use `is null`/`is not null` for null checks
- Access static members with capitalized type names (String.Empty, Int32.MaxValue)
- Nullable reference types enabled
- Use `var` over explicit type declarations
- Latest C# features (file-scoped namespaces, records, pattern matching)
- Meaningful names revealing intent
- Async/await for all asynchronous operations

### Exception Handling

Only UI layer catches general exceptions. All other layers catch specific exceptions they can handle.

Rules:

- Never use try/catch for flow control or default values
- Only catch exceptions that can be recovered from or need context added
- When catching and rethrowing, always add context
- Use `throw;` instead of `throw ex;` to preserve stack traces
- Document exceptional cases in code comments

### Project Structure

Follow established directory conventions for consistency and discoverability.

Structure:

```text
/src
  ├─ Xcaciv.Command.Packages/           # Core library code
  ├─ Xcaciv.Command.Packages.Tests/     # Unit and integration tests
/docs                                     # Documentation
README.md                                 # Project overview and usage
```

Namespace conventions:

- Base namespace matches project name
- Subdirectories extend base namespace
- One class per file; file name matches class name
- Exception classes in Exceptions subfolder at appropriate level
- Commands in Commands subfolder
- Services in Services subfolder

### Testing Strategy

Testing requirements to be defined based on project needs. When tests are written:

Guidelines:

- Use xUnit test framework
- Focus on complex logic, edge cases, security validation
- Mock external dependencies (NuGet APIs)
- Test error scenarios and rollback behavior
- Integration tests for package operations end-to-end
- Performance tests for search and installation operations

### Build & Deployment

Use Visual Studio 2026 for all build operations. Support multiple build configurations.

Standards:

- Target .NET 10 with latest C# features
- Central Package Management via Directory.Packages.props
- Build configurations: Debug, Release, Compact (AOT, trimmed, single-file)
- DebugType: none for Release and Compact builds
- NuGet package for distribution
- Semantic versioning (MAJOR.MINOR.PATCH)
- Independent release cadence from Xcaciv.Cupcake

## Governance

This constitution supersedes all other development practices and documentation for Xcaciv.Command.Packages. It embodies the principles of FIASSE and SSEM to produce securable, maintainable, trustworthy and reliable software.

Rules:

- All code reviews must verify compliance with security requirements
- Complexity must be justified; prefer simplicity (YAGNI)
- Breaking changes require version bump and migration guidance
- Amendments require documentation and rationale
- Refer to copilot-instructions.md for detailed coding guidance
- PRD (prd.md) defines functional requirements; constitution defines how we build

**Version**: 1.0.0 | **Ratified**: 2025-12-28 | **Last Amended**: 2025-12-28
