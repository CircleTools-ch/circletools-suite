# Deployment — Mobile apps (Android / iOS)

> _Placeholder._ To be completed at the deployment milestone.

## Targets
- **Android** → Google Play (signed AAB), phone + tablet.
- **iOS** → Apple App Store (signed IPA), iPhone + iPad.
- Built from the MAUI Blazor Hybrid `AppSuite.<App>.App` projects.

## Prerequisites (already available locally)
- MAUI workloads: `android`, `ios`, `maccatalyst`, `maui-windows` (`dotnet workload restore`).
- Apple: developer account, provisioning profiles, a Mac/CI for iOS builds.
- Google: Play Console account, upload key.

## Store listings
- Localized listings in **DE / EN / FR**; screenshots per device class.

## Checklist (to build out)
- [ ] App signing & provisioning
- [ ] Versioning & release channels (internal → beta → production)
- [ ] Localized store metadata (DE/EN/FR)
- [ ] Privacy declarations
- [ ] Staged rollout & monitoring
