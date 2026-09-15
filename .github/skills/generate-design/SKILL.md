---
name: generate-design
description: "Use when generating or updating DESIGN.md, logo.svg, or style-guide.html from REQUIREMENTS.md and available project design input. Produces implementation-neutral visual and frontend guidance with a dependency-free reference page, and asks for input when no usable requirements are available."
---

# Generate Design

Create or update the repository-root `DESIGN.md`, `logo.svg`, and `style-guide.html`. These artifacts define the product's visual language and frontend behavior without selecting a UI framework or external dependency.

## Input Gate

1. Read the repository-root `REQUIREMENTS.md` first when it exists. Treat it as the source of truth for the product, users, workflows, data, and supported capabilities.
2. Read explicit design notes, existing brand guidance, and relevant repository documentation only after `REQUIREMENTS.md`.
3. If neither usable requirements nor design input is available, stop and ask the user for source material. Request, at minimum:
   - product purpose and primary audience
   - core workflows or screens that need design support
   - platform targets
   - known brand direction or existing assets
   - light/dark mode expectations
   - accessibility or regulatory constraints
4. If the inputs are incomplete, produce only what they support and expose material gaps in `## Open Questions`.

## Source and Scope Rules

- Do not invent product features, user types, workflows, brand claims, or visual direction unsupported by the source documents.
- Use `UNKNOWN` for facts the available documents do not provide.
- Use `TO BE DECIDED` for design decisions that remain open.
- Never turn `UNKNOWN` or `TO BE DECIDED` into an unmarked final decision.
- A provisional default may be rendered in `style-guide.html` when needed to make the page usable, but label it clearly as provisional and keep the decision open in `DESIGN.md`.
- Keep architecture, implementation topology, framework selection, deployment, vendor choice, and product behavior out of this work unless the requirements explicitly make them design constraints.
- Do not use external fonts, image URLs, icon packages, CSS frameworks, JavaScript libraries, or build steps. The logo and style guide must work offline.

## Workflow

### 1. Establish the design inputs

Fill the required input fields exactly once. Derive `Primary audience` from `REQUIREMENTS.md` when present; do not infer it from an imagined market. Carry forward relevant requirements and existing assets without broadening scope.

### 2. Define a restrained visual system

Document a coherent but modest brand direction, color tokens, typography, spacing, layout, responsive behavior, and broadly useful components. When the source does not establish a strong brand personality, choose a neutral readable provisional direction and mark the broader decision `TO BE DECIDED`.

Color tokens MUST include `primary`, `secondary`, `background`, `surface`, `text`, `error`, and `success`, with hex values and logo colors documented. Check stated text/background pairs against WCAG 2.2 AA targets: 4.5:1 for normal text and 3:1 for large text.

Typography MUST name primary and fallback families, supported weights, and a practical type scale. Prefer system or open-licensed families available without a network request.

### 3. Create the logo

Create a simple, original `logo.svg` with one primary mark, a small deliberate palette, and no external assets. It must work on light backgrounds at minimum, scale cleanly, and remain recognizable at small sizes. Prefer shapes and paths over text. Do not copy or imitate copyrighted or trademarked marks.

If the product name is unknown, use an abstract or neutral mark and state the naming gap in `DESIGN.md`. Document clear space, minimum size, colors, and a few incorrect-usage examples.

### 4. Create the reference page

Create one self-contained `style-guide.html` with inline CSS and no framework or external asset. It must render:

- the logo;
- every color token with a labeled swatch and hex value;
- the type scale and supported weights;
- the spacing scale;
- representative buttons, inputs, links, focus states, and feedback/error states supported by the design guidance; and
- hover and visible keyboard-focus states.

Where `DESIGN.md` says `TO BE DECIDED`, render a usable provisional default and label it as provisional. Keep the page compact and make it the reference implementation of the documented tokens.

### 5. Validate the artifacts

Before finishing, verify that:

- `DESIGN.md`, `logo.svg`, and `style-guide.html` exist at the repository root;
- `DESIGN.md` contains exactly these sections in order: `## Required Design Inputs`, `## Brand and Logo`, `## Color Palette`, `## Typography`, `## Layout and Spacing`, `## Components`, `## Accessibility`, `## Open Questions`;
- each required design input appears exactly once;
- every stated requirement is supported by `REQUIREMENTS.md` or explicit design input;
- all unknowns and pending decisions use the required markers;
- all required color tokens are present, named, and used consistently in the logo and reference page;
- the page is self-contained and contains no external asset, framework, font, or service dependency;
- focus visibility, keyboard access, reduced-motion behavior, and contrast targets are documented and represented where applicable; and
- changes to `DESIGN.md` and `style-guide.html` remain synchronized.

If the artifacts already exist, update them in place, preserve supported existing decisions, and remove only content contradicted by newer explicit input. Do not rewrite unrelated files.

## Required Output

### DESIGN.md

Use exactly these sections in this order:

## Required Design Inputs

Each field appears exactly once:

- Brand personality: `TO BE DECIDED` unless explicitly supported
- Primary audience: derived from `REQUIREMENTS.md`, otherwise `UNKNOWN`
- Platform targets (web / mobile / both): `TO BE DECIDED` unless explicitly supported
- Light / dark mode: `TO BE DECIDED` unless explicitly supported
- Existing brand assets: `UNKNOWN` unless explicitly supported

## Brand and Logo

Document supported brand direction, the logo concept, what the mark represents, and usage guidance for `logo.svg`.

## Color Palette

Document the required named tokens, hex values, contrast expectations, and colors used by the logo.

## Typography

Document primary and fallback families, type scale, supported weights, and rationale.

## Layout and Spacing

Document implementation-neutral spacing, grid assumptions, and responsive breakpoints.

## Components

Document behavior and visual intent for buttons, inputs, links, focus states, and form feedback/error presentation. Include only components supported by the known product.

## Accessibility

Target WCAG 2.2 AA with concise, behavior-oriented guidance for contrast, visible focus, keyboard operation, semantics, and reduced motion.

## Open Questions

Include only unresolved decisions that materially affect branding, UX direction, accessibility, or implementation. Use stable IDs `DQ-1`, `DQ-2`, and so on.

### logo.svg

A plain, original SVG with no external assets, suitable for light backgrounds and consistent with the documented palette.

### style-guide.html

A single offline-capable HTML page with inline CSS and working examples of all documented tokens and components. Provisional choices must be visibly labeled.
