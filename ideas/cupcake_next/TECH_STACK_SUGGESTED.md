# TECH-STACK-SUGGESTED.md

> Recommended 3-layer architecture for a multi-provider LLM system.  
> Last updated: 2026-08-31

---

## Overview

This document describes the suggested layering strategy for building a robust, multi-provider LLM system. The design separates provider-agnostic abstractions (Core) from runtime-profile specialization (Core.Profile.*) and from host-specific integration (Host). Each layer has clear ownership, a defined dependency direction, and a namespace convention that encodes its role.

```ascii
┌────────────────────────────────────────────┐
│           Layer 3 — Host                   │
│  (application entry-points & wiring)       │
└──────────────┬─────────────────────────────┘
               │ depends on
┌──────────────▼─────────────────────────────┐
│        Layer 2 — Core.Profile.*            │
│  Interactive  │  Headless                  │
└──────────────┬─────────────────────────────┘
               │ depends on
┌──────────────▼─────────────────────────────┐
│           Layer 1 — Core                   │
│  (provider-agnostic abstractions)          │
└────────────────────────────────────────────┘
```

Dependency rule: **lower layers never reference higher layers.**

---

## Layer 1 — `Core`

### Purpose

Layer 1 is the foundational, provider-agnostic nucleus of the system. It defines the canonical contracts (interfaces, abstract base classes, value objects, and shared utilities) that every higher layer depends on. Nothing in `Core` knows about a specific LLM provider, a specific UI framework, or a deployment target.

### Responsibilities

- Define `ILanguageModel`, `IEmbeddingModel`, `IChatSession`, and related provider-neutral interfaces.
- Own domain value objects: `Prompt`, `CompletionResult`, `TokenUsage`,
  `ModelCapabilities`, `ProviderDescriptor`.
- Provide cross-cutting utilities: retry policies, structured logging contracts, configuration schema base types, and error hierarchies.
- Provide LLM call loop adaptor interface to allow a framework level implementation of the chat session (`IChatLoopController`) controller managing round trips to the provider, when to call tools, what messages to send to the user, and when to return controle to the user.
- Publish the **provider registration** contract so that concrete adapters (OpenAI,  Anthropic, Azure, local GGUF, etc.) can register themselves at startup without `Core` referencing them directly.

### Namespace Guidance

| Concern | Namespace |
| --- | --- |
| Interfaces & abstractions | `Core.Abstractions` |
| Domain value objects | `Core.Domain` |
| Shared utilities | `Core.Utilities` |
| Error hierarchy | `Core.Errors` |
| Configuration schemas | `Core.Configuration` |
| Provider registration | `Core.Providers` |

### Key Design Rules

- No framework-specific types (no ASP.NET, no WPF, no Blazor, no console I/O).
- No direct `HttpClient` usage; expose `IHttpClientFactory`-compatible hooks only.
- Stable, semver-versioned; breaking changes require a major version bump.

---

## Layer 2 — `Core.Profile.*`

Layer 2 splits into two sibling profiles that **both depend on Layer 1** but
address entirely different runtime characteristics. A profile is a curated
composition of services, middleware, and defaults tuned for one deployment shape.

---

### 2a — `Core.Profile.Interactive`

#### Purpose

`Core.Profile.Interactive` is the profile for human-facing, latency-sensitive
workloads where a user is present and waiting for streamed output. It optimises
for perceived responsiveness, rich UX feedback (streaming tokens, progress
indicators, cancellation), and conversational state management.

#### Responsibilities

- Wire streaming-first LLM client adapters (server-sent events, WebSocket, gRPC
  streaming) on top of `Core.Abstractions.ILanguageModel`.
- Manage `IChatSession` lifecycle: context window tracking, message history
  trimming, and per-session memory stores.
- Provide `IStreamingRenderer` — a hook the Host layer uses to push tokens to
  a UI surface without the profile knowing which surface that is.
- Implement interactive retry UX: surface partial results on timeout, prompt user
  to retry rather than silently failing.
- Expose telemetry events tuned for UX metrics: time-to-first-token (TTFT),
  inter-token latency, session abandonment rate.

#### Namespace Guidance

| Concern | Namespace |
| --- | --- |
| Streaming adapters | `Core.Profile.Interactive.Streaming` |
| Session management | `Core.Profile.Interactive.Sessions` |
| Streaming renderer contract | `Core.Profile.Interactive.Rendering` |
| UX-oriented telemetry | `Core.Profile.Interactive.Telemetry` |
| Interactive DI extensions | `Core.Profile.Interactive.Extensions` |

#### Key Design Rules

- Must not import Host-layer types; communicate upward through events/callbacks
  defined in its own `Rendering` namespace.
- Streaming adapters must honour `CancellationToken` at every `await` boundary.
- Session state must be serialisable so the Host can persist and restore it.

---

### 2b — `Core.Profile.Headless`

#### Purpose

`Core.Profile.Headless` is the profile for automated, throughput-optimised
workloads where no human is present: batch inference, scheduled pipelines,
evaluation harnesses, and background agents. It optimises for throughput,
resource efficiency, deterministic retries, and structured output.

#### Responsibilities

- Wire non-streaming (request/response) LLM client adapters with connection
  pooling and concurrency throttling.
- Implement structured output enforcement: JSON schema validation, automatic
  re-prompting on schema violations, output parsing pipelines.
- Provide a batch execution engine: work-item queuing, parallelism controls,
  back-pressure, and checkpoint/resume for long-running jobs.
- Emit machine-readable telemetry: tokens-per-second throughput, batch
  completion rate, cost estimates per provider, error classification histograms.
- Supply a `IHeadlessResultSink` contract for the Host to attach storage,
  message queues, or webhook dispatch without the profile coupling to them.

#### Namespace Guidance

| Concern | Namespace |
| --- | --- |
| Request/response adapters | `Core.Profile.Headless.Adapters` |
| Structured output | `Core.Profile.Headless.StructuredOutput` |
| Batch execution engine | `Core.Profile.Headless.Batch` |
| Throughput telemetry | `Core.Profile.Headless.Telemetry` |
| Result sink contract | `Core.Profile.Headless.Sinks` |
| Headless DI extensions | `Core.Profile.Headless.Extensions` |

#### Key Design Rules

- Default to deterministic settings (`temperature: 0`, fixed seed where supported)
  unless the caller explicitly overrides.
- Retry logic must be exponential-backoff with jitter; never busy-poll.
- Structured output failures must be logged with the raw response before
  re-prompting, to aid debugging.

---

### Shared Profile Constraints (both 2a and 2b)

- Both profiles depend **only** on `Core` (Layer 1); they must not reference each
  other or any Host-layer type.
- Each profile ships its own DI extension method (`AddInteractiveProfile()` /
  `AddHeadlessProfile()`) consumed by the Host layer.
- A single Host application **may** register both profiles simultaneously — for
  example, an API server that serves real-time chat endpoints (Interactive) and
  also runs nightly evaluation jobs (Headless).

---

## Layer 3 — `Host`

### Purpose

The Host layer is the application composition root. It owns startup, dependency
injection wiring, configuration loading, and the entry-point(s) (HTTP server,
CLI, background service, etc.). The Host is the only layer permitted to reference
both profile packages and to make cross-cutting decisions (e.g., which providers
to register, which profiles to activate).

### Responsibilities

- Load environment-specific configuration (appsettings, env vars, secrets store).
- Register providers discovered at runtime against `Core.Providers` contracts.
- Call profile extension methods (`AddInteractiveProfile()` and/or
  `AddHeadlessProfile()`) to configure the DI container.
- Implement `IStreamingRenderer` (for Interactive) and `IHeadlessResultSink`
  (for Headless) using the actual UI/storage technology (SignalR hub, Blazor
  component, Azure Service Bus, S3, etc.).
- Define health checks, authentication middleware, observability pipelines
  (OpenTelemetry exporters), and deployment targets.

### Namespace Guidance

| Concern | Namespace |
| --- | --- |
| Application entry-point | `Host` or `Host.App` |
| HTTP / API surface | `Host.Api` |
| Background workers | `Host.Workers` |
| UI components / pages | `Host.UI` |
| Infrastructure adapters | `Host.Infrastructure` |
| Startup & DI wiring | `Host.Startup` |

> Host namespaces are intentionally flexible — the exact structure depends on the
> chosen application framework (ASP.NET Core, MAUI, Worker Service, etc.).

### Key Design Rules

- The Host **must not** contain business logic; delegate everything to Layer 1 or 2.
- Provider secrets (API keys) must be loaded from a secrets store or environment
  variable injection, never hardcoded.
- The Host is the only layer with permission to reference framework-specific
  packages (e.g., `Microsoft.AspNetCore.*`, `Microsoft.Extensions.Hosting`).

---

## Namespace Suffix Convention

Consistent suffix conventions signal the role of a type at a glance.

| Suffix | Role | Example |
| --- | --- | --- |
| (none / noun) | Value object or DTO | `CompletionResult`, `TokenUsage` |
| `I` prefix | Contract definition | `ILanguageModel`, `IChatSession` |
| `Adapter` | Concrete provider binding | `OpenAiAdapter`, `AnthropicAdapter` |
| `Profile` | DI composition module | `InteractiveProfile`, `HeadlessProfile` |
| `Session` | Stateful conversation unit | `ChatSession`, `InteractiveSession` |
| `Renderer` | Output delivery hook | `SignalRStreamingRenderer` |
| `Sink` | Result persistence hook | `BlobStorageResultSink` |
| `Engine` | Orchestration / execution unit | `BatchEngine`, `RetryEngine` |
| `Policy` | Configurable behaviour rule | `RetryPolicy`, `ThrottlePolicy` |
| `Extensions` | Static DI helper methods | `Interactive.Extensions` |
| `Telemetry` | Metrics / tracing helpers | `Interactive.Telemetry` |

---

## Provider Registration Pattern

Concrete provider adapters register themselves against `Core.Providers` without
`Core` taking a hard dependency on them. The recommended pattern:

```ascii
Core.Providers.IProviderRegistry          ← defined in Layer 1
     ↑
ConcreteProvider.OpenAiAdapter            ← separate package, no layer
     ↑
Host.Startup registers adapters by calling:
    registry.Register<OpenAiAdapter>(options => { ... });
```

This keeps `Core` provider-agnostic and lets the Host swap, add, or remove
providers through configuration alone.

---

## Summary Table

| | Layer 1 `Core` | Layer 2 `Core.Profile.Interactive` | Layer 2 `Core.Profile.Headless` | Layer 3 `Host` |
| --- | --- | --- | --- | --- |
| **Knows about** | Abstractions only | Streaming UX, sessions | Batch, structured output | Everything |
| **Depends on** | Nothing above | `Core` only | `Core` only | Layers 1 & 2 |
| **Referenced by** | Layers 2 & 3 | Layer 3 | Layer 3 | Nobody |
| **Framework coupling** | None | Minimal (streaming primitives) | Minimal (async/Task) | Full (ASP.NET, MAUI, etc.) |
| **Key extension point** | `IProviderRegistry` | `IStreamingRenderer` | `IHeadlessResultSink` | DI composition root |
| **Primary consumer** | Library authors | Interactive app developers | Pipeline / batch developers | DevOps / app deployers |

---

*This document is a living recommendation. Update it as the architecture evolves.*

---

**A few things to note about the document structure:**

- **Strict one-way dependency** — `Core` ← `Core.Profile.*` ← `Host`. No upward references, ever.
- **Profiles are siblings, not a hierarchy** — Interactive and Headless both sit at the same layer and must never reference each other, which means a Host can safely activate one or both without circular risk.
- **Extension-point contracts live in the profile** (`IStreamingRenderer`, `IHeadlessResultSink`) while implementations live in the Host — this is the key seam that keeps UI and infrastructure frameworks out of Layers 1 and 2.
- **Provider adapters are layer-less packages** — they register against `Core.Providers.IProviderRegistry` at Host startup, so swapping providers is a config change, not a code change.
