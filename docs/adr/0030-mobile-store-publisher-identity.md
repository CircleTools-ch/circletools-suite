---
status: Accepted
date: 2026-07-29
deciders: [Adrian]
tags: [deployment, distribution, mobile, legal, privacy, process]
---

# ADR-0030 — Store publisher identity and account ownership

## Context and problem statement

Shipping the [MAUI](../arc42/05-building-block-view.md) heads means enrolling in two developer
programmes: the **Apple Developer Program** and the **Google Play Console**. Both ask, at enrolment,
a question that is easy to answer carelessly and expensive to change later — *who is the publisher?*
Both offer an **individual** and an **organization** enrolment, and both publish parts of that answer
on the store listing where every user can read it.

The answer also decides which **account** owns the apps. An App Store app belongs to an Apple ID; a
Play app belongs to a Google account. Neither is a login detail — losing access to that account, or
having it belong to the wrong identity, is losing the ability to update the app.

This is a hobby suite for families and friends, currently free, with no legal entity behind it. The
brand and domain are still deliberately deferred ([naming](../process/naming.md)), so a role address
on the suite domain does not exist yet.

**Who publishes the apps, and which accounts own them?**

## Decision drivers

- **Cost and time.** An organization enrolment needs a legal entity and a D-U-N-S number (Apple) and
  a D-U-N-S number plus website verification (Google). A hobby project with no revenue should not be
  paying for a company to publish a Secret-Santa app.
- **Privacy.** Both stores publish publisher contact details. Whatever is chosen becomes public.
- **Reversibility.** The brand is not chosen yet, so anything tied to the not-yet-existing domain
  must be deferrable without blocking enrolment.
- **Account survivability.** The owning account must outlive a forgotten password, a lost phone, and
  a change of email provider.
- **Data minimisation** in spirit with [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md):
  do not expose more personal data than the distribution channel forces.

## Considered options

1. **Individual enrolment on dedicated, project-owned accounts.**
2. **Individual enrolment on the existing personal Apple ID and Gmail.**
3. **Organization enrolment behind a Swiss Einzelfirma.**

## Decision outcome

**Chosen: option 1 — enrol as a private individual, on accounts created for the project.**

Two parts, and the second matters as much as the first:

**Individual, not organization.** Apple and Google both charge and verify individuals with far less
ceremony: no legal entity, no D-U-N-S number, no company website to verify. For a free app with no
revenue, an Einzelfirma would be overhead bought for the sole benefit of a nicer seller name.

**Dedicated accounts, not the daily-driver ones.** A new Apple ID and a new Google account are
created for the project and used for nothing else. They get their own strong passwords in a password
manager, their own 2FA with **recovery codes stored offline**, and a recovery email that is not the
same account. When the brand lands, both are pointed at a role address on the suite domain
(`dev@<suite-domain>` or similar) rather than at anyone's personal inbox.

The reason for the second part is that store accounts are long-lived infrastructure that happen to
look like logins. Publishing from the personal Gmail means the app's future is coupled to a mailbox
that is also used for everything else, cannot be handed to anyone, and cannot be renamed onto the
suite domain later without an account migration.

**What becomes public, and is accepted:** the App Store shows the developer's **legal name** as the
seller; Google Play shows a contact **email address and a physical address** on the listing. This is
the price of individual enrolment and it is paid knowingly. A P.O. box or a `c/o` business address is
the usual mitigation for the Play address if the home address is unacceptable.

## Consequences

- **Positive:** enrolment can start immediately — nothing here waits on the brand, the domain, or a
  company. Apple's individual enrolment is normally approved in 24–48 hours; Play's identity check in
  hours to days.
- **Positive:** cheapest possible path. USD 99/year (Apple) and USD 25 once (Google), with no
  registry fees, no D-U-N-S wait, and no accounting obligations.
- **Negative / accepted:** Adrian's legal name is published on every App Store listing, and a contact
  address on every Play listing. There is no individual enrolment without this.
- **Negative / accepted:** if the suite ever earns money or wants a company face, moving is real
  work. Google Play supports an **app transfer** between accounts. Apple's equivalent is an account
  or app transfer that is possible but tedious, and cannot be done while an app has certain
  entitlements or open agreements. The cost of switching is accepted as a future problem in exchange
  for shipping now.
- **Follow-ups:**
  - Create the two accounts *before* enrolling, not during — the enrolment flow will happily attach
    itself to whatever account is signed in.
  - Point both accounts at a suite-domain role address once [naming](../process/naming.md) lands.
  - Declare **DSA trader status** in App Store Connect; it is mandatory for every developer and
    un-declared apps are removed from the EU App Store
    ([NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)).
- **New risks:** the project's ability to ship updates now rests on two accounts. Losing 2FA access
  to either one is an outage with no fast remedy — hence the offline recovery codes, recorded as
  risk **R7** in [arc42 ch. 11](../arc42/11-risks-and-technical-debt.md).

## Pros and cons of the options

### 1. Individual on dedicated accounts (chosen)
- 👍 Cheapest and fastest; no legal entity, no D-U-N-S, no website verification.
- 👍 The accounts are project assets from day one — transferable, renameable onto the suite domain,
  and not entangled with anyone's personal mail.
- 👎 Personal legal name (Apple) and contact address (Play) are published.
- 👎 Two more accounts to secure and keep alive.

### 2. Individual on existing personal accounts
- 👍 Nothing to create; enrol in minutes.
- 👎 Couples the apps' future to a personal mailbox; the daily-driver Gmail becomes production
  infrastructure.
- 👎 Cannot be moved to a suite-domain address later without migrating the account itself.
- 👎 Store notifications, review rejections and security alerts drown in personal mail.

### 3. Organization behind an Einzelfirma
- 👍 Company name instead of a personal name as seller; a business address instead of a home one.
- 👍 The right answer *if* the suite ever sells anything.
- 👎 Requires forming and maintaining a legal entity for a free hobby app.
- 👎 D-U-N-S number plus website verification adds weeks before the first submission is even possible.

## Links

- Related ADRs: [ADR-0031](0031-ios-build-host.md),
  [ADR-0022](0022-account-deletion-and-data-export.md)
- Requirements: [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md),
  [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md)
- arc42: [07 Deployment view](../arc42/07-deployment-view.md),
  [11 Risks and technical debt](../arc42/11-risks-and-technical-debt.md)
- Process: [naming](../process/naming.md)
- Runbook: [Deployment — mobile apps](../deployment/mobile-apps.md)
