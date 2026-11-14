# Security policy

CircleTools stores small but sensitive data — who drew whom, wishlists, family members' names — so we take
reports seriously and answer them quickly.

## Reporting a vulnerability

**Please do not open a public issue for a security problem.**

- Preferred: GitHub's **[private vulnerability reporting](https://github.com/CircleTools-ch/circletools-suite/security/advisories/new)**
  for this repository.
- Alternatively: email **dev@circletools.ch** with "Security" in the subject.

Please include what you found, how to reproduce it, and what an attacker could do with it. You will get an
answer within **7 days**. We will tell you when it is fixed and, if you like, credit you in the release notes.

## What is in scope

- The code in this repository: the API, the web apps, the Android and iOS apps, and the deployment files in
  `deploy/`.
- The hosted service at `circletools.ch` and its subdomains — but test only against **your own** accounts and
  groups, never other people's data, and no denial-of-service or load testing.

Self-hosted servers are run by their operators; report problems in **our code** to us, and problems in a
particular installation to its operator.

## Supported versions

Only the latest release is supported: the current hosted service, the latest app version in the stores and
the latest container images. Fixes are not back-ported.

## How we handle a report

The design decisions behind authentication, encryption and data retention are public in
[`docs/adr/`](docs/adr/) and the [security & privacy guidelines](docs/guidelines/security-privacy-guidelines.md),
because security must not depend on the code being secret ([ADR-0093](docs/adr/0093-publish-the-repository.md)).
A confirmed issue is fixed on a private branch, released, and then disclosed in a GitHub security advisory.
