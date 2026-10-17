---
id: TP-025
feature: FEAT-025
status: Approved
related: [ADR-0081, ADR-0071, ADR-0044, FR-038, FR-048]
---

# TP-025 — Test plan for FEAT-025

## Scope & objectives

Verify the signed-in storage choices and transitions, the owner release, the create-time storage
explanation and the named import prompt.

## Test matrix

| Item | Test case | Level | Automated? |
|------|-----------|-------|------------|
| Q31 | Share while signed in → owned (AdminAccount set, join token kept, no admin token) | Unit (client) | ✅ |
| Q31 | Stop sharing → online copy gone, local group left for the account store | Unit (client) | ✅ |
| Q31 | Owner releases a joined person; a non-owner can't; the entry goes too | Store + unit | ✅ |
| Q31 | Second device discovers the owned group; follows stop sharing and invite | Browser | manual ✅ |
| Q24 | Signed-in choices Device / Account / Account·shared; signed-out Device / Link / Account (disabled) | Component | ✅ |
| Q24 | First create shows the expanded explanation; no dialog on first visit | Component + browser | ✅ |
| Q35 | Prompt names the groups with a summary; unticked kept local; Later decides nothing; no discard | Component + browser | ✅ |
| Q15 | Prompts and labels in DE/EN/FR, no "admin link" | Parity test + review | ✅ |
| — | E2E storage-mode and claim-admin tests re-run | E2E | ❌ pending |

## Exit criteria

- [x] Automated cases above pass
- [x] Browser walkthrough clean
- [ ] E2E suite green
