# App Store listing — English (U.S.)

Paste into App Store Connect → *Secret Santa* → *Distribution* → the version page (and *App Information*
for name/subtitle). Limits are App Store Connect's; `README.md` explains the fields that are not per
language.

## Name (≤30) — App Information

```
Secret Santa - CircleTools
```

_The name the app record was created with (plain "Secret Santa" is taken on the App Store). It must be
identical in every locale ([ADR-0057](../../docs/adr/0057-app-store-naming-and-localization.md))._

## Subtitle (≤30) — App Information

```
Draw names, share secret links
```

## Promotional text (≤170) — version page, editable any time without review

```
Christmas is coming! Draw names in seconds, share a private link with everyone and keep the secret. Works offline, no account needed, free and without ads.
```

## Keywords (≤100, comma-separated, no spaces) — version page

```
gift exchange,name draw,kris kringle,christmas,xmas,wishlist,family,office,party,holiday,randomizer
```

_Do not repeat words from the name or subtitle (they are indexed anyway) and never use competitors'
names — Apple rejects trademark keywords._

## Description (≤4000) — version page

```
Secret Santa makes organizing a gift exchange effortless — for your family, friends, team or any group.

Create a group, add everyone and draw names fairly in seconds. Set exclusions so couples don't draw each other. Everyone gets a private link that reveals only who they've drawn — plus that person's wishlist — so the secret stays a secret.

No account needed. The app works offline, and the people you invite never have to install anything or sign up: a link is all it takes.

HOW IT WORKS
• Create a group and add the people taking part
• Plan the occasion: date, place and an optional budget
• Set who can't draw whom (exclusions)
• Draw names with a single tap
• Share each person's private link via WhatsApp, Messages, email or QR code
• Everyone opens their link to see who they've got, and that person's wishlist

FEATURES
• Works offline — no account required
• Free, with no ads and no tracking
• Wishlists — everyone can add their own wishes straight from their link
• Gift notes for allergies or things not to give
• Optional gift budget, as a range or just a maximum
• Exclusions for couples, families or repeat pairings
• Put a group online so everyone joins with a link
• Reminders before the big day, and add the date to your calendar
• Run it again next year from the same group — without repeating last year's pairs
• Optional account to sync between your phone, tablet and the web
• Sign in with a passkey or a password

PRIVATE BY DESIGN
Without an account, your groups stay on your device. Only what you choose to share — a group you put online, or your account if you create one — is stored, on servers in Switzerland. No ads, no trackers, no profiling. Export or delete your data any time, right in the app.

IN ENGLISH, GERMAN AND FRENCH
The app follows your device language, and you can switch at any time.

Whether it's Christmas, an office party or a family get-together, Secret Santa takes the hassle out of gift exchanges — so you can focus on the fun.
```

## What's New (≤4000) — per update; this is the next version after 1.0.10

```
• Password fields now have an eye icon to show what you typed.
• The list of signed-in devices updates right after you change your password.
• Screen readers now use the language the app is showing.
• Small corrections to the French texts.
```

## TestFlight — Beta App Description (≤4000) — TestFlight → Test Information

```
Secret Santa organizes a gift exchange for your family or friends: create a group, add everyone, set exclusions, draw names and share a private link with each person that reveals only who they've drawn, plus that person's wishlist.

The app works fully offline with no account. Signing in is optional and only needed to sync between devices or to join groups through an account.

Thank you for testing! Please try a complete round — create a group, draw, share the links, open one — and tell us anything that feels confusing, looks wrong or doesn't work.
```

## TestFlight — What to Test (≤4000) — per build

```
Please play through a whole round:
1. Create a group, add 4–6 people and an event (date, budget).
2. Add an exclusion (e.g. a couple), then draw names.
3. Share a few private links (WhatsApp, Messages, copy, QR) and open one on another phone.
4. From an opened link, add your own wishlist.
5. Optional: create an account, sign in on a second device and check that the group appears.
6. Optional: allow reminders and check that a notification arrives.

Tell us about anything confusing, any text that reads oddly in your language, and any crash (TestFlight → Send Beta Feedback, or a screenshot).
```
