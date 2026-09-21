# ENOC Engineering Rule Catalog v1.0

This directory contains the foundational **Engineering Rule Schema and Catalog** for the ENOC AI Engineering Agent. These rules govern architectural constraints, security standards, code quality, observability, environments, and CI/CD parameters across all mobile platform development (Android, iOS, and Multiplatform).

The design philosophy ensures that every rule is structured enough to be deterministically validated by software gates, yet clear enough to be interpreted by Large Language Models (LLMs) during automated code generation and AI reviews.

---

## 📂 Directory Structure

```text
rules/
├── README.md             # This guide
├── config.yaml           # Global configurations (Severities, Enforcement Types, Lifecycles)
├── architecture.yaml     # MVVM, Clean Architecture, Unidirectional Data Flow rules
├── security.yaml         # Platform cryptography, secure storage, secret sanitization rules
├── testing.yaml          # Boundary mocking, branch coverage, Given-When-Then rules
├── observability.yaml     # Injected logger abstractions, redaction, structured metadata rules
├── environments.yaml     # Environment separation (DEV/QA/PROD) and debug protection rules
└── cicd.yaml             # CI pipeline gates, secure signing, build distribution rules
```

---

## ⚙️ Core Schema Components

As defined in `config.yaml`, the rule engine runs on standard dimensions:

### 1. Severity Levels
*   **`BLOCKER`**: Catastrophic compliance or security failure. Delivery pipelines are halted completely.
*   **`CRITICAL`**: Architectural or severe security violation. Automatically blocks Pull Request merges.
*   **`HIGH`**: Significant engineering standard deviation. Requires priority remediation.
*   **`MEDIUM`**: Code quality or structural improvements. To be resolved before a production release.
*   **`LOW`**: Best-practice recommendations or maintainability suggestions.
*   **`INFO`**: Contextual guidelines or informational tips.

### 2. Enforcement Mechanisms
Rules can combine multiple automated check vectors:
*   `AI`: Evaluated dynamically by the LLM Agent during design and code-review phases.
*   `STATIC`: Verified via deterministic static analysis tools (e.g., Linters, SAST).
*   `TEST`: Validated through automated unit, integration, or functional test execution suites.
*   `SECURITY`: Screened via dedicated security dependency scanners and secret verification tools.
*   `CI`: Enforced by mandatory platform gates in the CI/CD pipeline.

---

## 🏗️ Rule Structure & Schema

Every rule in the catalog follows a standardized YAML model. To reduce redundancy and improve readability, common properties are inherited from the `defaults:` section in `config.yaml`.

### 1. The Rule Model (Individual Files)
```yaml
id: "UNIQUE-ID"              # e.g., ARCH-001
name: "Descriptive Title"
category: "architecture"     # architecture, security, testing, etc.
severity: "CRITICAL"         # See severity levels above

description: >
  High-level overview of the rule's purpose.

requirements:                # Positive constraints (Must-Do)
  - "Requirement 1"

forbidden:                   # Negative constraints (Must-Not-Do)
  - "Anti-pattern 1"

enforcement: [AI, STATIC]    # Mechanism tags

ai_guidance:
  instructions: [...]        # Direct prompt injections for generation
  review_questions: [...]    # Questions for AI-led audit

evidence:
  reference: "Specific doc"  # Source material identifier
```

### 2. Global Defaults (`config.yaml`)
Rules automatically inherit these values unless explicitly overridden:
*   **`version`**: Default `1.0`
*   **`status`**: Default `active`
*   **`scope`**: Standard platforms (Android/iOS), project types (Mobile), and environments (Dev/QA/Prod).
*   **`evidence.source`**: Standardized source material (e.g., "AI-Assisted Mobile App Development").

---

## 📑 Rule Catalog Matrix (25 Core Rules)

| ID | Rule Name | Category | Default Severity | Enforcement |
| :--- | :--- | :--- | :--- | :--- |
| **ARCH-001** | MVVM with Clean Architecture | Architecture | `CRITICAL` | `AI`, `STATIC` |
| **ARCH-002** | Unidirectional Data Flow | Architecture | `HIGH` | `AI`, `STATIC` |
| **ARCH-003** | Passive View | Architecture | `HIGH` | `AI`, `STATIC` |
| **ARCH-004** | Dependency Injection | Architecture | `HIGH` | `AI`, `STATIC` |
| **SEC-001** | Platform Cryptography | Security | `CRITICAL` | `AI`, `SECURITY` |
| **SEC-002** | Secure Storage | Security | `CRITICAL` | `AI`, `SECURITY` |
| **SEC-003** | No Hardcoded Secrets | Security | `BLOCKER` | `STATIC`, `SECURITY`, `CI` |
| **SEC-004** | Secure Networking | Security | `CRITICAL` | `AI`, `STATIC`, `SECURITY` |
| **SEC-005** | Sensitive Data Logging Protection | Security | `CRITICAL` | `AI`, `STATIC`, `SECURITY` |
| **TEST-001** | Mock External Boundaries | Testing | `HIGH` | `AI`, `TEST` |
| **TEST-002** | Happy Path Coverage | Testing | `HIGH` | `TEST` |
| **TEST-003** | Boundary Case Coverage | Testing | `HIGH` | `TEST` |
| **TEST-004** | Failure and Timeout States | Testing | `HIGH` | `TEST` |
| **TEST-005** | Given When Then Structure | Testing | `MEDIUM` | `AI`, `TEST` |
| **TEST-006** | 100% Branch Coverage Target | Testing | `HIGH` | `TEST`, `CI` |
| **LOG-001** | Injected Logger Abstraction | Observability | `HIGH` | `STATIC`, `AI` |
| **LOG-002** | Structured Logging | Observability | `MEDIUM` | `AI`, `STATIC` |
| **LOG-003** | Log Redaction | Observability | `CRITICAL` | `AI`, `STATIC`, `SECURITY` |
| **LOG-004** | Production Log Routing | Observability | `HIGH` | `AI`, `CI` |
| **ENV-001** | Environment Separation | Environments | `CRITICAL` | `AI`, `STATIC`, `CI` |
| **ENV-002** | Externalized Configuration | Environments | `HIGH` | `STATIC`, `AI` |
| **ENV-003** | Production Debug Protection | Environments | `CRITICAL` | `STATIC`, `AI`, `CI` |
| **CICD-001**| Pull Request Quality Gate | CI/CD | `BLOCKER` | `CI` |
| **CICD-002**| CI Signing and Secrets | CI/CD | `BLOCKER` | `CI`, `SECURITY` |
| **CICD-003**| Environment Distribution | CI/CD | `HIGH` | `CI` |

---

## 🤖 How the Agent Utilizes These Rules

The AI Engineering Agent intercepts any development or review query through a two-step cycle:

1.  **Contract Retrieval**: When a task is requested (e.g., *"Create a registration feature"*), the Agent matches the context against the `scope` fields of the rule catalog and builds a localized **Engineering Contract**.
2.  **Dual Verification**:
    *   During **Generation**, the Agent maps `ai_guidance.instructions` into its system prompt instructions to synthesize compliant code natively.
    *   During **Review**, the Agent processes `ai_guidance.review_questions` to audit the code, creating a structured compliance report alongside automated tool gates (Linter, Unit Tests, Secret Scanners).

## ▶️ Generate an Engineering Contract

The .NET desktop application turns the catalog into a deterministic prompt contract for an AI coding or review agent. The catalog loader validates the schema, rejects duplicate or malformed rule IDs, and filters active rules by platform, language, environment, and project type. Launch it from the repository root:

```bash
dotnet run --project "AI Enoc Engineering Agent"
```

Use the task field, platform, language, environment, and output format selectors, then choose the rules to apply. The platform selects the development language automatically: Android uses Kotlin, iOS uses Swift, and Multiplatform uses Flutter (Dart). The environment represents the deployment target: Dev is local development, QA is shared testing, and Production is the release configuration; it provides context for rules covering debug protection, logging, signing, and distribution. The **Copy** and **Save as...** actions export the result for an AI coding or review agent. The generated contract includes only the selected applicable rules, catalog schema metadata, generation timestamp, task context, AI instructions, forbidden patterns, review questions, and completion gates.

Automated catalog and contract tests are in `AI Enoc Engineering Agent.Tests`; run them with:

```bash
dotnet test "AI Enoc Engineering Agent.Tests/AI Enoc Engineering Agent.Tests.csproj"
```
