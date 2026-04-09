# Ubuntu Inmutable OS image

This repository contains the initial planning and scaffolding direction for building an Ubuntu immutable OS image with container-based workflows. The current focus is a shared CI/CD approach for GitHub Actions and Gitea Actions, using a `Containerfile`, publishing to `ghcr.io`, and including a medium security baseline with SBOM, vulnerability scanning, and provenance.

Build orchestration is planned as a C# Bullseye build project (inspired by bullseye-sample), so CI logic is defined in strongly typed targets instead of shell scripts being the primary control layer.

See the implementation prompt here: [.github/prompts/plan-container-build.prompt.md](.github/prompts/plan-container-build.prompt.md)