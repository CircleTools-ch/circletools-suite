# App Store listing — English (U.S.)

Paste into App Store Connect → *Secret Santa* → *Distribution* → the version page (and *App Information*
for name/subtitle). Limits are App Store Connect's; `README.md` explains the fields that are not per
language.

## Name (≤30) — App Information

```
Secret Santa – Gift Swap
```

_From the next iOS version (backlog ASO-2, [ADR-0057, amendment 2026-11-17](../../docs/adr/0057-app-store-naming-and-localization.md)):
the brand first, then the search term. Until then "Secret Santa - CircleTools" (plain "Secret Santa" is
taken). Set it in all four English locales (U.S., U.K., Australia, Canada). "Secret Santa – Gift Exchange"
(the Play title) is taken; "Draw Names" is avoided because the market leader's brand is "Drawnames". The name
must be unique on the store; if App Store Connect refuses it, use `Secret Santa: Draw & Wishlist`._

## Subtitle (≤30) — App Information

```
Gift exchange & wishlists
```

_Was "Draw names, share secret links". "Gift exchange" — the Play title's term — moves here from the
keywords, so "gift swap" (name) and "gift exchange" (subtitle) are both in the strongest fields._

## Promotional text (≤170) — version page, editable any time without review

```
Christmas is coming! Draw names in seconds, share a private link with everyone and keep the secret. Works offline, no account needed, free and without ads.
```

## Keywords (≤100, comma-separated, no spaces) — version page

```
kris kringle,pollyanna,christmas,xmas,randomizer,generator,picker,name draw,family,office,party
```

_From the next iOS version (ASO-2): "gift exchange" and "wishlist" are now in the subtitle, so they leave
this field ("holiday" makes room); new: pollyanna (a U.S. name for the game), generator, picker.
Was: `gift exchange,name draw,kris kringle,christmas,xmas,wishlist,family,office,party,holiday,randomizer`._

_Do not repeat words from the name or subtitle (they are indexed anyway) and never use competitors'
names — Apple rejects trademark keywords._

## Description (≤4000) — version page

_Reworked for 1.0.14 (backlog PR-17): same text as the Play listing. Entered in App Store Connect for version 1.0.14 on 2026-11-10 (DE, FR and all four English locales)._

```
Secret Santa is the simple Secret Santa app for every gift exchange – with your family, friends, colleagues or club. Draw names fairly, give everyone a private link, and keep the secret a secret.

Nobody has to install anything or sign up. You organize everything in the app – it even works offline – and the people taking part only need the link you send them.

HOW IT WORKS
• Create a group and add the people taking part
• Plan the occasion: date, place and an optional budget
• Set who can't draw whom (exclusions)
• Draw names with a single tap
• Share each person's private link via WhatsApp, Messages, email or QR code
• Everyone opens their link to see who they've got, and that person's wishlist

KEY FEATURES
• Fair name draw, with exclusions for couples, families or repeat pairings
• Private links – each person sees only who they've drawn
• Wishlists – everyone adds their wishes straight from their link
• Gift notes for allergies or things not to give
• Optional gift budget, as a range or just a maximum
• Reminders before the big day, and add the date to your calendar
• Put a group online so everyone joins with a link
• Run it again next year from the same group – without repeating last year's pairs
• Works offline – no account required
• Free, with no ads and no tracking
• Optional account to sync between your phone, tablet and the web, with a passkey or a password

PERFECT FOR
• Family Christmas – the whole family draws names, even relatives who live far away
• Office and team parties – a gift exchange without a spreadsheet or a hat full of paper slips
• Friends and flatmates – set it up in minutes and send the links in your group chat
• Clubs, school classes and associations – larger groups, with exclusions that just work
• Any time of year – a name draw works for every gift exchange, not just in December

PRIVATE BY DESIGN
Without an account, your groups stay on your device. Only what you choose to share – a group you put online, or your account if you create one – is stored, on servers in Switzerland. No ads, no trackers, no profiling. Export or delete your data any time, right in the app.

QUESTIONS & ANSWERS
Does everyone need the Secret Santa app?
No. Only the person organizing uses the app. Everyone else opens their private link in any browser.

Do I need an account?
No. Without an account your groups stay on your phone. Create one only if you want to sync between devices.

Is it free?
Yes – no ads, no in-app purchases and no subscription.

Can we use it again next year?
Yes. Add a new event to the same group and carry last year's over – the draw then avoids last year's pairs.

IN ENGLISH, GERMAN AND FRENCH
The app follows your device language, and you can switch at any time.

Whether it's Christmas, an office party or a family get-together, Secret Santa takes the hassle out of gift exchanges – so you can focus on the fun.
```

## What's New (≤4000) — per update; this is the version after 1.0.14

```
Secret Santa is now open source: About & legal shows the license and a link to the source code. Plus security updates and small improvements.
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
