# Copilot Instructions

The github repo is Xcaciv/Xcaciv.Cupcake and the primary branch that I work off of is main.

Never stub or mock functionality unless explicitly asked to do so.
Your terminal is PowerShell, only use powershell syntax.
prefer using Visual Studio 2026 for building and testing instead of donet CLI commands.

## Formatting and Style

- Do not use emojis in code, documentation, or responses. Use only standard markdown formatting.
- Extended ASCII characters are acceptable where appropriate.
- Use standard markdown headers, lists, tables, and code blocks for formatting.

## Naming Conventions

- Never use the term "Helper" or "Utils" in class names. Use descriptive, intent-revealing names instead.
- Examples:
  - BAD: `PathHelper`, `StringHelper`, `ValidationUtils`
  - GOOD: `PathValidator`, `StringFormatter`, `InputValidator`
- Before extracting functionality to a separate class, consider if it belongs closer to where it is used.
- Follow the Single Responsibility Principle, but avoid over-emphasis of DRY to the detriment of maintainability.
- Prioritize analyzability: code should be easy to understand and trace, even if it means some controlled duplication.

This document includes guiding principles for .NET development, based on the **Framework for Integrating Application Security into Software Engineering (FIASSE)** and the **Securable Software Engineering Model (SSEM)**. The goal is to influence code generation on a fundamental level to produce software that is inherently "securable", adaptable and resilient to evolving threats.

The primary directive is to **"Resiliently add computing value."** This means generating code that is not only functional but also possesses intrinsic qualities that allow it to be maintained and secured over its lifecycle.

---
# Exception handling

Only the UI layer is allowed to catch general exceptions and log them. All other layers should only catch specific exceptions that they can handle or recover from or add context to.
eg. `catch (FileNotFoundException ex) { throw new ConfigurationException("Config file missing", ex); }`

Never use try/catch for flow control or to return a default value. Only use try/catch when an exception can be recovered from gracefully, when context needs to be added to an exception, when an exception needs to be logged. If you need to catch an exception and rethrow it, always add context. In the rare occasion that you cannot, use `throw;` instead of `throw ex;` to preserve the original stack trace.

# Security

Avoid string concatenation for commands or queries. Use parameterized queries or safe APIs.

## The Securable Principle

Security is not a static state but a dynamic process. When generating code, prioritize qualities that allow the software to be evolved, corrected, and adapted to new threats efficiently and without introducing new vulnerabilities.

## Software Engineering principles to follow for security:

#### The Derived Integrity Principle

The Derived Integrity Principle asserts that any value critical to a system’s integrity—whether related to business logic or internal state—must be calculated or derived within the trusted server-side context, never accepted directly from the client.

- Server as the Source of Truth: The server must own and compute authoritative values (e.g., prices, permissions, object states).
- Client Intent ≠ Client Authority: Clients express intent (e.g., “I want to buy item X”) but must not dictate facts (e.g., “It costs $0.01”).

#### Canonical Input Handling

Prefer the most narrow data types possible for input parameters. For example, use enums or booleans instead of strings where applicable.
- Canonicalization/Normalization: Ensures that input data conforms to expected formats, types, lengths, and ranges before processing.
Prevents unexpected or malicious data from entering the system.
- Sanitization: Cleans input data to remove or neutralize potentially harmful content. Prevents malicious data from being executed or interpreted as code, protecting against injection attacks.
- Validation: Checks that input data meets specific criteria before processing. Ensures that only valid and expected data is processed, reducing the risk of injection attacks or unexpected behavior. Always prefer allowing explicit values instead of rejecting unexpected values.

## The Securable Software Engineering Model (SSEM)

Focus on the following three core attributes of securable software when generating or modifying code.

### Maintainability

**Goal:** Create code that can be easily understood, modified, and tested, as this is the foundation for long-term security.

-   **Analyzability:** Generate code that is simple, readable, and has low cyclomatic complexity. Use clear and consistent naming conventions. The easier the code is to understand, the easier it is to spot and fix security flaws.
-   **Modifiability:** Produce modular, loosely coupled, and highly cohesive code. This ensures that changes in one part of the system do not have unintended security consequences elsewhere.
-   **Testability:** Write code that is inherently testable. Use pure functions where possible and apply dependency injection. Code with high test coverage is more likely to be secure.

### Trustworthiness

**Goal:** Create code that behaves as expected, protects sensitive data, and ensures that actions are legitimate and traceable.

-   **Confidentiality:** Protect data from unauthorized disclosure. Never hardcode secrets (credentials, API keys). Use placeholders for secrets and rely on secure configuration management or vault systems. Employ strong, modern encryption for data in transit and at rest.
-   **Authenticity & Accountability:** Ensure users and services are who they claim to be and that their actions are traceable. Implement strong authentication and authorization checks for all sensitive operations. Generate detailed audit logs for security-relevant events (e.g., login attempts, access control decisions, data modification).
-   **Integrity:** Protect data and system resources from unauthorized modification.

### Reliability

**Goal:** Create code that operates correctly, resists failure, and can recover from attacks.

-   **Integrity (as part of Reliability):** All inputs are potential vectors for attack. Implement rigorous input validation at the boundaries of your application. Use parameterized queries and safe APIs to prevent injection attacks. Prefer safe serialization formats.
-   **Resilience:** Implement robust and graceful error handling. Fail safely without leaking sensitive information in error messages. Code should be resilient to unexpected inputs and states.
-   **Availability:** Ensure the system remains operational. Be mindful of resource consumption and potential denial-of-service vectors (e.g., unbounded operations, resource leaks).

## Securable strategies

### Transparency

Transparency is the principle of designing a system so that its internal state and behavior are observable and understandable to authorized parties. Transparency means that exceptions are not silently swallowed, all errors are logged appropriately, and system behavior can be audited. Logging should be structured and include context to facilitate monitoring and forensic analysis.

### Resilient Coding Practices

- Strong typing to ensure data is usable in the intended way.
- Filtering and validating all input at trust boundaries. Input validation ensures that data conforms to expected formats, types, lengths, and ranges before processing.
- Properly escaping and encoding all output destined for other systems or interpreters (exiting trust boundaries). This prevents injection attacks by ensuring that data is treated as data, not executable code.
- Sandboxing the use of null values to input checks and database communication. Use exceptions to handle exceptional cases.
- Implement comprehensive and strategic error handling to manage unexpected conditions gracefully, rather than allowing the application to crash or behave unpredictably.
- Using immutable data structures for threaded programming to prevent insecure modification and ensure thread safety and prevent race conditions.
- Canonical Input Handling: Canonicalization/Normalization, Sanitization Validation

---

# Project Structure 

-   **Modularity:** Organize code into well-defined, independent modules or components. This improves maintainability and limits the change surface of security vulnerabilities.
-   **Layering:** Adhere to architectural layering (e.g., presentation, business logic, data access). This enforces separation of concerns and helps in applying security controls at appropriate layers.
-   **Configuration Management:** Externalize configuration settings, especially security-sensitive ones. Use environment variables or windowsWindows Credential Manager stores instead of hardcoding values.
-   **Dependency Management:** Regularly audit and update third-party libraries and frameworks to mitigate known vulnerabilities. Use tools to scan for vulnerable dependencies.

### Directory Structure

```
/src
  ├──/ProjectName.Interface     # interfaces code (if applicable)
  ├──/ProjectName.Core          # core function code (if applicable)
  ├──/ProjectName.Shell         # shell application code (if applicable)
  ├──/ProjectName.Shell.Tests   # Unit and integration tests (if applicable)
  ├──/ProjectName.Module        # module code (if applicable)
  ├──/ProjectName.Module.Tests  # Unit and integration tests (if applicable)
  ├──/ProjectName.Cli           # cli application code (if applicable)
  ├──/ProjectName.Cli.Tests     # Unit and integration tests (if applicable)
  ├──/ProjectName.Web           # web application code (if applicable)
  ├──/ProjectName.Web.Tests     # Unit and integration tests (if applicable)
  ├──/ProjectName.Api           # API application code (if applicable)
  ├──/ProjectName.Api.Tests     # Unit and integration tests (if applicable)
  ├──/ProjectName.Module.SubModule           # Module SubModule layer (if applicable)
  └──/ProjectName.Module.SubModule.Tests     # Module SubModule layer Unit and integration tests (if applicable)
/docs                   # Documentation files including readme, design docs, etc.
/tmp                    # Test command and temporary files
README.md               # Project description, installation/usage instructions
```

Naming conventions for namespaces follow the directory structure, e.g., directory `src/ProjectName.Module.SubModule/` contains `ProjectName.Module.SubModule.csproj` whose base namespace is `ProjectName.Module.SubModule`. Each project sub directory extends the base namespace, e.g., `src/ProjectName.Module.SubModule/Services/` contains code in the `ProjectName.Module.SubModule.Services` namespace. 

One class per file, file name and path must match class name. Always put exception classes in an exception subfolder under the highest level in the project the exception is thrown. 

Avoid using `Helper` or `Utils` in class names, instead use descriptive names. When creating classes specific to a single Command or Service, put them in a subfolder named after the Command or Service.

---

## Tech Stack

### Framework & Runtime:
- **.NET 9** with C# latest features, file-scoped namespaces
- Console Application - Terminal-based chat shell
- **AWSSDK.BedrockRuntime** AWS Bedrock integration
- **Azure.AI.OpenAI** Azure OpenAI integration
- **Spectre.Console** Console UI framework
- **Microsoft.SemanticKernel** AI orchestration framework
- **Blazor Server** for interactive web UI with real-time updates
- **Azure Functions** (.NET 9) for serverless backend APIs
- **.NET Aspire** for local orchestration and service discovery
- **Bootstrap** + custom CSS variables for responsive theming
### AI/ML Services:
- Amazon Bedrock - AWS AI service for chat functionality
- Azure OpenAI Service - Microsoft's AI service integration
- LLamaSharp - Local LLM support
- HuggingFace Transformers
### Data & Configuration:
- JSON - Configuration file format (settings.json)
- File System - Chat history import/export
### Security & Credentials:
- Windows Credential Manager - Secure credential storage (Windows-specific)
- Environment Variables - Cross-platform credential management
- Windows DPAPI - Data Protection API for encryption
### AWS Integration:
- AWS SDK - For Bedrock integration
- AWS Credential Chain - Standard AWS authentication
- AWS IAM - Role-based authentication support
### Development Tools:5
- Git - Version control
- Visual Studio 2026 - Build, run and profiler tooling
### Platform Support:
- Windows 10/11 - Primary platform (required for Windows Credential Manager)
- Cross-platform - Linux and macOS support for core functionality

## Code Style
- Prefer async/await over direct Task handling
- When checking for nul in C# prefer to use `is null` or `is not null`
- Use nullable reference types
- Use var over explicit type declarations 
- Always implement IDisposable when dealing with event handlers or subscriptions
- Prefer using async/await for asynchronous operations
- Use latest C# features (e.g., records, pattern matching)
- Use consistent naming conventions:
  - PascalCase for public members, methods, and properties
  - camelCase for private fields and properties (no underscore prefix)
  - Example: `private bool disposed;` not `private bool _disposed;`
- When accessing static members on scalar types, use the capitalized type name
  - Example: `String.Empty` not `string.Empty`, `Int32.MaxValue` not `int.MaxValue`
- Use meaningful names for variables, methods, and classes
- Use dependency injection for services and components
- Use interfaces for service contracts and put them in a unique file
- Use file scoped namespaces in C# and are PascalCased
- Always add namespace declarations to Blazor components matching their folder structure
- Organize using directives:
  - Put System namespaces first
  - Put Microsoft namespaces second
  - Put application namespaces last
  - Remove unused using directives
  - Sort using directives alphabetically within each group

## Component Structure
- Keep components small and focused
- Extract reusable logic into services
- Use cascading parameters sparingly
- Prefer component parameters over cascading values

## Error Handling
- Use try-catch blocks in event handlers
- Implement proper error boundaries
- Display user-friendly error messages
- Log errors appropriately
- **Usage Limit Errors**: Check for JSON error responses with "USAGE_LIMIT_EXCEEDED" ErrorCode and display UsageLimitDialog instead of raw error messages

## Performance
- Implement proper component lifecycle methods
- Use @key directive when rendering lists
- Avoid unnecessary renders
- Use virtualization for large lists

## Testing
- Write unit tests for complex component logic only if i ask for tests
- Test error scenarios
- Mock external dependencies
- Use MSTest for component testing
- Create tests in the feedbackflow.tests project

## Documentation
- Document public APIs
- Include usage examples in comments
- Document any non-obvious behavior
- Keep documentation up to date

## File Organization
- Keep related files together
- Use meaningful file names
- Follow consistent folder structure
- Group components by feature when possible

### Azure Functions Development
- **Local settings**: Create `local.settings.json` with required API keys
- **Storage emulator**: Uses `AzureWebJobsStorage: "UseDevelopmentStorage=true"` for local development  
- **Required API keys**: GitHub PAT, YouTube API key, Azure OpenAI endpoint/key, Reddit credentials
- **Functions runtime**: `dotnet-isolated` (.NET 9)
- **Key endpoints**: SaveSharedAnalysis, GetSharedAnalysis, GitHubIssuesReport, WeeklyReportProcessor

### Package Management
- Uses **Central Package Management** via `Directory.Packages.props`
- Key packages: Azure.AI.OpenAI, Azure.Data.Tables, Microsoft.Azure.Functions.Worker, Blazor.SpeechSynthesis
- All projects target **.NET 9** with nullable reference types enabled

## Build

### Build Preferences

- Always use Visual Studio 2026 for building and testing.
- Do not suggest `dotnet build`, `dotnet test`, or other CLI commands.
- Prefer Visual Studio menu actions, Solution Explorer context menus, or MSBuild integration.
- Assume the developer is using Visual Studio 2026 with full IDE support.

### Build Options

Create a "Compact" build configuration with the following properties:
- PublishAot: true for AOT compilation
- PublishTrimmed: true for trimming unused code
- PublishSingleFile: true for single executable
- SelfContained: true for self-contained deployment
- DebugType: none for release and compact builds
- TrimmerDefaultAction: link to remove unused code

# Code Review Guidelines for Copilot

- Ensure all methods use PascalCase.
- Private fields should use camelCase without underscores.
- Use `is null` / `is not null` for null checks.
- Access static members with capitalized type names (e.g., `String.Empty`).
- Flag spelling errors in documentation and project descriptions.
- Do not suggest combining lines of code if it does not improve functionality