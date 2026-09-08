## 7. Features

The bulk of this document. One subsection per feature, ordered by dependency tier (§5.2) so that a
reader encountering a concept has already met what it rests on, and so that the order doubles as the
suggested build order (§10).

Each subsection follows the same shape: a description, numbered user stories, use cases with their
error flows, numbered functional requirements, this feature's external-technology requirements,
executable acceptance criteria, and source notes recording the bug-for-bug decisions the feature
forces on the clone team.

**Requirement identifiers** are scoped to the subsection: `US-4.2` is the second user story of §7.4,
`FR-8.13` the thirteenth functional requirement of §7.8, `AC-3.5` the fifth acceptance criterion of
§7.3. Product-wide requirements carry `GR-` identifiers and live in §6; non-functional requirements
carry `NFR-` identifiers and live in §9. Where a feature requirement merely instantiates a
product-wide rule, it cites the `GR-` rather than restating it.

**Two reading notes.** First, §7.1 is unlike the others: it specifies a capability the source
*obtains* rather than implements, and it is placed first because everything else depends on its
semantics. Second, §7.6 and §7.10 are cross-cutting concerns given their own subsections
deliberately — the product's failure contract and its security posture are each a single coherent
decision surface, and a clone that meets them feature-by-feature will meet them inconsistently.

| § | Feature | Kind | Tier |
|---|---|---|---|
| 7.1 | Command Extensibility Contract | required external capability | 1 |
| 7.2 | Configuration & Settings | cross-cutting | 2 |
| 7.3 | Console Presentation & Interaction | user-facing | 2 |
| 7.4 | Plugin Discovery & Command Registration | platform capability | 2 |
| 7.5 | Interactive Shell Session | user-facing | 3 |
| 7.6 | Error Handling & Failure Reporting | cross-cutting | 3 |
| 7.7 | Package Registry Client | integration | 1 |
| 7.8 | Package Search Command | user-facing | 4 |
| 7.9 | Package Install Command | user-facing (incomplete in source) | 4 |
| 7.10 | Input Validation & Supply-Chain Safety | cross-cutting | 5 |
| 7.11 | Shell Distribution & Entry Points | platform capability | 5 |
