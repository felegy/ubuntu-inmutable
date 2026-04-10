## Implement Kubernetes Matrix Build For Container CI

Update the workflow in .github/workflows/container.yml to support Kubernetes matrix builds with a dynamic k3s version source.

### Goal
- Keep a baseline build with empty Kubernetes settings.
- Add k3s builds for the latest three stable k3s versions.
- Use a minor-only image tag suffix for k3s matrix entries, for example: k3s-v1.27.

### Requirements
1. Add a preparatory workflow job that fetches k3s release versions from the GitHub API using curl + jq:
- exclude prerelease, draft, and rc tags,
- keep only tags matching vX.Y.Z+k3sN,
- select latest patch/revision per minor,
- select latest 3 minors,
- output JSON array through GITHUB_OUTPUT.

2. Build a dynamic matrix payload from the fetched versions:
- include one baseline entry:
    - kubernetes_distro: ""
    - kubernetes_version: ""
    - image_extra_tag: ""
- include 3 k3s entries:
    - kubernetes_distro: "k3s"
    - kubernetes_version: full version string from API
    - image_extra_tag: derived minor tag in format k3s-v<major>.<minor>

3. Update build-and-publish job:
- use needs with the preparatory job,
- use strategy.matrix from the computed JSON payload,
- set env values from matrix:
    - KUBERNETES_DISTRO
    - KUBERNETES_VERSION
    - IMAGE_EXTRA_TAG

4. Keep existing checkout, dotnet, buildx, login, Trivy, and scripted CI steps unless changes are necessary for matrix support.

5. Ensure the build script can consume IMAGE_EXTRA_TAG and include it in published image tags when pushing.

### Expected Output
1. Workflow changes in .github/workflows/container.yml
2. Any required build script changes (for example in build.csx)
3. Short verification summary:
- matrix entry count
- baseline + k3s entries
- image tag suffix derivation logic

