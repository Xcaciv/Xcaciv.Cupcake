# ChatDbg — Ultimate Tool Reference

> **Deliverable 4 of 4.** A *hypothetical* specification of the complete tool surface for a rebuilt ChatDbg,
> structured as a Cupcake-patterned shell whose entire capability set is attribute-declared, dynamically-loadable
> tools.
>
> **This document is a handoff brief.** It is written to be consumed by an architect — human or model — who will
> produce the architecture documentation from it. It specifies *what tools exist, what they accept, what they
> guarantee, and how they compose*. It deliberately stops short of architecture: no assembly diagrams, no class
> models, no sequence diagrams of internal collaborators. Those are the next artifact, and this is its input.

---

## 0. Orientation

### 0.1 What this is, and what it is not

| | |
|---|---|
| **Is** | A tool catalog: 117 tools in 9 packages, each with its registration, parameters, pipeline behaviour, environment interaction, failure modes, security posture and traceability. |
| **Is** | A host specification: how a Cupcake-patterned shell composes, starts, dispatches, loads packages, and shuts down. |
| **Is** | A conventions contract: the shared environment namespace, result formats, error and audit rules that every tool must honour. |
| **Is not** | An architecture. There are no internal component diagrams, no dependency-injection graphs, no layering rules. |
| **Is not** | An implementation. Attribute declarations appear because they *are* the specification language of the tool framework; no method bodies do. |
| **Is not** | A description of the existing product. It is the *target*. Where it departs from the source, it says so and says why. |

### 0.2 The three fixed constraints

This specification is written against three components that are **given, not chosen**:

| Component | Role | Version | Reference |
|---|---|---|---|
| **Xcaciv.Cupcake** | The host pattern — how the shell is composed, how it loops, how it contributes tools | pattern, not a dependency | `synthesis/ref-cupcake.md` (521 lines) |
| **Xcaciv.Command** | How tools are *defined* and *executed* — the delegate contract, the attribute vocabulary, the typed parameter system, the pipeline | 3.3.x | `synthesis/ref-command.md` (2,145 lines) |
| **Xcaciv.Loader** | How tool packages are *loaded* — isolation, path policy, integrity verification, unload | 2.1.2 | `synthesis/ref-loader.md` (1,060 lines) |

Everything else — runtime, inference libraries, rendering, storage — is chosen in `TECH-STACK-TARGET.md` and is out of scope here.

### 0.3 Provenance: where the behaviour came from

Every tool in this catalog is either **ported** or **new**.

- **Ported** tools trace to observed behaviour in the source product at commit `d8c18f61d6bb73666ed97cd4885e877e35558485`, by way of a feature dossier and a PRD subsection. Their defaults, valid ranges, path expressions and error strings are the *real* ones, not invented ones.
- **New** tools have no ancestor in the source. Each is marked **NEW** and carries a stated reason for existing.

Of the 117 tools, **63 are ported and 54 are new**. The new ones cluster where the source had a capability but no way to reach it: the source shipped three fully-implemented commands that neither front end ever registered, a diagnostic-logging component whose configuration surface no user could touch, and a credential store with no way to list, rotate or scan what it held.

### 0.4 How to read a tool entry

Each tool is specified as:

1. **Registration** — command name, root command, description, usage prototype.
2. **Parameters** — one row each: name, kind (ordered / named / flag / suffix), type, required, default, allowed values or range, help text. Values carried from the source are real; parameters with no ancestor are marked NEW.
3. **Pipeline behaviour** — source, filter, sink, or not pipeable; what one piped chunk means.
4. **Environment interaction** — keys read, keys written, whether environment-modifying rights are needed.
5. **Failure modes** — bad input, missing prerequisites, upstream pipeline errors, and what the operator sees for each.
6. **Security and audit** — whether any parameter or output carries a secret and must be masked; whether the action is destructive and needs confirmation.
7. **Traceability** — the PRD feature subsection and the source command it descends from, or NEW.

---

## 0.5 Package map

| # | Package | Root | Owns | Trust required | Ships in box? | Tools |
|---|---|---|---|---|---|---|
| 1 | `ChatDbg.Tools.SessionConversation` | `CHAT` | Conversational turns and the conversation record | Network (via backend) | Yes | 12 |
| 2 | `ChatDbg.Tools.ConfigurationProfiles` | `SET` | Every tunable, the settings file, named profiles | Filesystem (user profile) | Yes | 16 |
| 3 | `ChatDbg.Tools.CredentialsSecretStorage` | `CRED` | Secret supply, resolution, storage, rotation, redaction | OS keystore + filesystem | Yes | 13 |
| 4 | `ChatDbg.Tools.SystemPromptLibrary` | `PROMPT` | Named instruction prompts and their lifecycle | Filesystem (user profile) | Yes | 14 |
| 5 | `ChatDbg.Tools.ModelBackends` | `MODEL` | The backend registry, selection, capability probing, local model load | Network + native library | Yes | 11 |
| 6 | `ChatDbg.Tools.TokenIntrospection` | `TOKEN` | Log-probability capture, tokenization, probability maps, attribution, statistics | Native library (local paths) | Yes | 13 |
| 7 | `ChatDbg.Tools.PresentationVisualization` | `VIEW` | Layout, heat mapping, themes, terminal capability, degradation | Terminal only | Yes | 13 |
| 8 | `ChatDbg.Tools.DiagnosticsObservability` | `DIAG` | Engine diagnostics, capture, rotation, export, health check, audit | Filesystem + native log hook | Yes | 12 |
| 9 | `ChatDbg.Tools.ToolPackageManagement` | `PKG` | Acquiring, verifying, trusting, updating and removing tool packages | **Elevated** — writes the plugin root | Yes (must be) | 13 |

Package 9 is the one package that **cannot** be acquired at runtime, because it is the thing that does the acquiring. It ships in the box and its own trust decisions are made by the host, not by itself.

---

## 0.6 What changes relative to the source product

The tool surface is a redesign, not a transcription. The substantive departures:

| # | Source behaviour | Target behaviour | Why |
|---|---|---|---|
| 1 | Two front ends with **divergent** command surfaces, hand-wired separately in each | One tool set, one controller, both front ends driving it | The source's full-screen host silently omits a backend and mutates a settings record nothing reads. Divergence was not a feature; it was drift. |
| 2 | Invented probability data presented as measured | **Never fabricate — owner-ratified, decision D-001 (`DECISIONS.md`).** Declared capability, honest refusal, provenance on every derived value; a capability-absent provider **auto-disables** the log-probabilities setting with a switch-provider instruction, the enabling UI is **disabled** where the platform allows it, and view attempts produce a **non-blocking** notice | The product exists to help a user judge model confidence. Inventing confidence inverts its purpose. |
| 3 | Three implemented commands registered by neither host | Every tool in the catalog is reachable, or it is not in the catalog | Shipping unreachable code as if complete is the defect; the fix is registration discipline. |
| 4 | Capability discovered by attempting and catching | Capability is a **declared, queryable value**, probed once and cached | A backend that cannot supply alternatives should say so before the call, not after. |
| 5 | No cancellation anywhere | Cancellation is part of the tool contract; a long generation is interruptible | An unabortable generation up to the maximum response length is a hard freeze. |
| 6 | Secrets resolved by side-effecting property reads on a serialization record | Secrets resolved once at an explicit boundary; the settings type is structurally incapable of holding one | Prevents the class of bug where a secret round-trips into a plaintext settings file. |
| 7 | GPU backend an unconditional dependency (~550 MB, 88% of output) | Acceleration backends are opt-in per artifact | Nobody should download a CUDA payload to run a cloud-backed chat. |
| 8 | Single flat command namespace, ad-hoc argument splitting | Root-grouped commands with typed, validated, attribute-declared parameters | Validation before execution is the framework's injection defense, and it generates the help for free. |
| 9 | No pipelines | Every tool declares its pipeline role; analysis composes | Token analysis is inherently a data-processing task, and the framework already threads pipelines. |

---

## 0.7 Handoff brief — what the architect should produce from this

This document deliberately answers *what* and leaves *how* open. The architecture documentation that follows it should decide and record:

1. **Assembly and project topology** — how the 9 packages, the contract assembly, the shared core and the two front ends map onto build artifacts, and which of them are separately versioned and separately shipped.
2. **The contract boundary** — precisely which types cross the plugin isolation boundary, and therefore which assembly must be pinned in the host's default load context and never duplicated.
3. **Internal composition** — the service graph behind the tools: what is a singleton for the process, what is per-session, what is per-invocation, and where the backend resolver sits.
4. **The introspection port** — the internal interface that carries per-token distributions from three structurally different backends into one presentation model, including how a backend that cannot supply a tier declares that.
5. **State ownership** — which component owns the conversation record, the settings, the loaded model handle and the diagnostic buffer, and what the threading rules are around each.
6. **Failure and cancellation propagation** — how an interrupt reaches native inference, how a pipeline stage failure surfaces, and what the disposal order is at shutdown.
7. **The test architecture** — how a tool is tested without a backend, how the terminal surface is tested, and how native inference is exercised without a multi-gigabyte model in the loop.
8. **The 74 open design questions** recorded across the package chapters, each of which needs a decision or an explicit deferral.

Where this document states a number, a default, a range or a path, treat it as a requirement traceable to the PRD. Where it states a *preference*, it says so.

### Companion documents

| Document | Answers |
|---|---|
| `PRD-ChatDbg.md` | What the product must *do*, implementation-agnostic, with 1,354 functional requirements and their acceptance criteria |
| `TECH-STACK-AS-BUILT.md` | What the source actually used, and which of its decisions must survive a port |
| `TECH-STACK-TARGET.md` | What the rebuild should use, with 30 architecture decision records |
| **This document** | What the tool surface is |
| `synthesis/ref-{cupcake,command,loader}.md` | The three fixed frameworks, in full |
| `DECISIONS.md` | Owner decisions that bind these specifications — D-001: no fabricated telemetry, auto-disable + guidance, UI gating |

---
---

# Part I — The Host
