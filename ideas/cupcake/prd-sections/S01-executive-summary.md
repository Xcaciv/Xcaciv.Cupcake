## 1. Executive Summary

### What the product is

**Cupcake** is an interactive, plugin-driven command shell for a text terminal. Its own README
describes it in four words — "A sweet shell" (`README.md:1-2`).

A person launches it, sees a prompt, types command lines one at a time, and reads the results. The
shell itself knows almost nothing about commands. It supplies the session, the terminal rendering,
the startup sequence that discovers commands, and a small built-in vocabulary for finding and
fetching more. Everything a user actually *does* comes from Commands — either the four supplied by
the underlying command capability, two compiled into the shipping program, or any number loaded at
startup from Plugin bundles found on disk.

The distinguishing bet is **extensibility over batteries**. The shell ships nearly empty and expects
to be filled: a Plugin author writes a Command bundle, publishes it to a package registry, and a
user finds and installs it from inside the shell itself. The Package Search command that queries a
registry (§7.8) is the most complete feature in the repository, and the Package Install command that
would complete the loop (§7.9) is declared, registered, help-documented — and deliberately inert,
returning a literal refusal.

### Who it serves

Four actors, all established from code (§3): the **Shell user** at a terminal, who is the only human
in the running system; the **Shell Operator** — the program that constructs and configures a Session
and registers commands compiled into it, a role the shipping executable plays in twelve lines; the
**Plugin author**, who publishes Command bundles to a registry; and the **Package Registry** as an
external system. There is no authentication, authorization, user account, role, tenancy, or
persisted identity anywhere in the product, and a reimplementer must not invent any (§3, §6).

### Scope of the clone

The whole product, feature for feature: eleven features (§7), spanning the interactive session, the
colour terminal adapter, plugin discovery, the two package commands, the registry client, the
configuration surface, the error and failure contract, the extensibility contract the shell requires
of its command capability, the input-validation and supply-chain posture, and the packaging and
entry points.

Two things are deliberately **excluded** and both are recorded in §2 rather than silently dropped: a
second, empty executable that prints a greeting and is otherwise dead, and the "having tests" of the
source, whose content nonetheless feeds acceptance criteria throughout.

### The honest state of the source

A reimplementer needs to know this before reading a single requirement.

**The product does not build at the pinned commit**, for two independent and separately-established
reasons (§2.4). The Package Search command references a result accumulator whose declaration was
deleted in commit `e1123b2` while all five uses were kept, and no version of the framework defines
such a member. Independently, dependency restore fails for every project: the feed that claims all
first-party packages is an unexpanded environment token, the private hosted feed is declared but
given no routing rule and so is never consulted, and the framework packages are absent from the
public index.

**And the headline feature cannot work.** The Plugin scan composes a search mask with a wildcard in
its *directory* portion — `*/bin/*.dll` — and hands it to a recursive directory enumeration. That
was executed against a real filesystem during verification, with a correct plugin layout in place:
it does not match the files, and it does not return empty. It **raises a directory-not-found
condition naming the literal path `<root>/*/bin`**, because the directory portion of a mask is joined
literally and its wildcard is never expanded. A control run with a plain `*.dll` pattern found the
same files, confirming the layout was right and the mask is the cause.

Two consequences follow, and they reshape the product's story. **No Plugin is ever loadable from
disk** — the extensibility mechanism the whole design is built around does not function as shipped.
And **any Plugin Directory that exists at all is fatal at startup**: the raised condition is neither
of the two "nothing found" conditions the startup guard tolerates, so it becomes a fatal load failure
that ends the process with exit status 1. The only path to a working prompt is the one where the
plugin folder is absent — which is the default state anyway, since the compiled-in default path uses
a Windows separator that is a literal filename character elsewhere. This supersedes the milder
reading, held earlier in the analysis, that only an *empty* plugin folder was fatal.

**The product itself was never run**, because it cannot be built. Every requirement here is read from
source. Where a specific mechanism could be isolated and executed on its own — the scan mask above,
the progress arithmetic, the path-containment primitive — that was done, and it is stated at the
point of claim. Everything else specifies **what the code says**, not what a running system was seen
to do, and the load-bearing inferences are flagged `INFERRED` for exactly that reason.

The product also carries an unusual density of quirks for its size: **84 are catalogued** (§11.2).
Several are user-visible and behaviourally load-bearing — a flag that never takes effect because
presence is tested rather than value; a detail-level option that silently downgrades output when its
case differs; a first-run experience that is *friendlier* when the user has done less setup, because
any plugin folder that exists is fatal while a missing one is tolerated; a progress reading that
computes a ratio and renders it with a percent sign; a parameter matcher unanchored at the end, so
`-taken` binds a parameter named `take` and swallows the next token. These are documented as observed
and tagged, and the keep-or-fix decision for every one of them is put to the commissioning team in
§11, not taken here.

### What a clone must source or build

The single largest dependency is not a library a clone can install: it is the **command capability**
itself — registry, tokenizer, dispatcher, declarative parameter metadata, pipeline engine, help
generator and sandboxed plugin loader. The source obtains it as a first-party package that is not
publicly distributed. §7.1 specifies its required behaviour in full so a clone can build or
substitute an equivalent; §5.3 lists every other external capability, generically, with the source's
concrete choice preserved as an annotation.
