# Security Policy

## Supported Versions

| Version | Supported | Notes |
| :--- | :---: | :--- |
| `1.0.x` | :white_check_mark: | Current GA release line; receives security patches. |
| `< 1.0.0` (Previews) | :x: | Unsupported. Pre-release users must upgrade to 1.0.0 or later. |

> **Note:** This policy formally takes effect with the v1.0.0 GA release. Until then, please report security issues against the latest available build.

## Reporting a Vulnerability

**Do not report security vulnerabilities through public GitHub issues, pull requests, or comments.**

<!-- MAINTAINER: Ensure Private Vulnerability Reporting is enabled in repository Settings -> Security -> Code security and analysis, or specify security contact email below prior to merging -->
Please report vulnerabilities privately via [GitHub Security Advisories](https://github.com/mrehman29/AutoMapSharp/security/advisories/new).

When reporting, please include as much of the following as you can:

- The affected package(s) and version(s).
- The target framework and runtime environment.
- A description of the issue and its potential impact.
- Steps to reproduce, or a minimal proof of concept.

## Response Expectations

- We aim to acknowledge your report within **3 business days**.
- We will keep you informed with status updates as the investigation and remediation progress.
- Once a fix is ready, we will coordinate disclosure through a published GitHub Security Advisory and a patched NuGet release.

## Scope

**In scope** — the code and published packages of AutoMapSharp: `AutoMapSharp`, `AutoMapSharp.Generators`, `AutoMapSharp.Analyzers`, `AutoMapSharp.MigrationAnalyzer`, `AutoMapSharp.Collections`, `AutoMapSharp.Extensions.DependencyInjection`, and `AutoMapSharp.Cli`. For example:

- Remote code execution risks.
- Denial of service or memory exhaustion in the mapping engine.
- Malicious code injection via the source generator or analyzers.
- Secrets or credentials exposed by AutoMapSharp code or packages.

**Out of scope:**

- Vulnerabilities in external third-party runtime dependencies that are not authored or bundled by AutoMapSharp (please report those to the relevant project).
- Attacks that require prior administrative or root access to the host.
