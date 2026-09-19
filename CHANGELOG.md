# Changelog

Every released version, newest first, in the format of
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow
[Semantic Versioning](https://semver.org/).

Entries say what changed for a player. Where a fix came out of a real session,
the figure that gave it away is quoted, because a claim with a number behind it
can be checked and one without cannot.

## [1.2.0] — 2026-09-19

### Changed

- **A released build writes no diagnostic files.** `decisions.jsonl` recorded
  every decision on every install, which is right for the machine the mod is
  worked on and wrong for a machine someone plays a game on. Whether a build
  records is now decided by how it was built rather than by a setting, so there
  is nothing to leave in the wrong position before a release. The handover
  ledger follows the same rule and keeps its file-only key, so it can still be
  switched on for a bug report.
- The line the mod writes when it loads now says which kind of build it is, so a
  bug report carries the answer without anyone having to ask.

## [1.1.3] — 2026-09-19

### Added

- **The log says what time it is in the game.** Every deal is placed by counting
  from the window the clock is in, and that window was only ever reported inside
  the sentence that named it — never the time it was read from. An argument
  about the time of day ran for an hour with no way to tell who was right.

## [1.1.2] — 2026-09-19

### Fixed

- **Deals were scheduled into the window the clock was already in.** The rule is
  that the current window is skipped, and it was being applied against the wrong
  reading: the mod asked the game to look up a *window's start time* and handed
  it the *running clock* instead. Three deals taken in the afternoon were all
  recorded as going into "the next window you allow today" — the answer the walk
  gives when it believes the clock is still in the morning. The window a time
  falls inside is now worked out from the window bounds the game reports, and
  where the two readings disagree the log says so.

## [1.1.1] — 2026-09-19

### Fixed

- **Negotiating found nothing, whatever the price.** Asking a customer for
  nothing at all was scored as a certain refusal — a customer turning down a
  gift. The search checks the two ends of its range first, and a refusal at both
  ends reads as proof there is nothing between them. There was: one customer had
  offered $260 a unit against a listed price of $250, and a counter at that
  listed price was a better deal for her than her own. It was never asked.

## [1.1.0] — 2026-09-19

### Added

- **A floor under the listed price: never below the product's suggested price.**
  Keeping every customer is the rule, and without a floor it ratchets — one
  customer who barely likes the product sets the price for everybody, and the
  next one like them sets it lower again. Below the suggested price nothing is
  bought anyway: the game's acceptance is already at its limit there, so a
  cheaper listing only hands out more units for the same money.

### Fixed

- **The negotiation's log line lost its figures in 1.0.0.** Four refusals in a
  row read *"no price at any quantity reached the confidence the search asked
  for"* and nothing else. Every price point is written down again, and a refusal
  now names what it was working from: what the customer offered, the suggested
  price, the listed price it may not go under, and what one of their orders is
  worth.

## [1.0.0] — 2026-09-19

### Changed

- **The negotiation asks the method that actually decides.** It used to search
  over a related figure, on the reasoning that the real one rolls a die and so
  cannot be searched. The roll is even, so the chance of clearing it can be
  worked out exactly — which is the chance the confidence setting was always
  meant to be a floor on.
- Two refusals the search used to walk into are now walls it never reaches: a
  total at or above three times what the customer spends on one order, and a
  unit price steep enough to fail the game's value floor.

### Removed

- The credits naming other mods' authors. Dealcraft is a rebuild of their ideas
  and ideas are not owned. It remains GPL-3.

## [0.9.0] — 2026-09-19

### Changed

- **The listed price is set where nobody goes quiet.** A customer stops ordering
  entirely — silently, with no refusal and no message — once the price climbs
  far enough above the product's suggested price, and how far depends on how
  much they like it. The price is put at the highest figure where none of them
  cross that line. Raising it further never earns more: a customer spends their
  own budget either way, so a higher price only sends less stock out for the
  same money.

## [0.8.1] — 2026-09-18

### Fixed

- **A refusal reported 0% for offers the customer would have taken half the
  time.** It now names the price the confidence floor cost you, and at what
  chance — which is the figure worth having, because it is what lowering the
  floor would buy.

## [0.8.0] — 2026-09-18

### Fixed

- **A handover holds four stacks, not four packages.** Five baggies out of one
  inventory slot cross as one stack. Counting them as five refused contracts a
  player could have completed by hand: one customer asked for five units with
  thirteen in the pocket and was told no.

## [0.7.0] — 2026-09-18

### Changed

- **The confidence setting steers the search instead of vetoing its answer.**
  It used to find the best-paying offer knowing nothing of the setting, and that
  answer sits near half confidence every time — so any floor above that refused
  everything, however good an offer sat higher up. Lowering the floor from 90%
  to 70% changed nothing at all. Now the search looks only at confidences at or
  above the setting and takes the best-paying of those.

## [0.6.0] — 2026-09-18

### Fixed

- **A reading that could not take every customer now prices nothing.** It used
  to step over the customers it could not read and carry on with the rest, which
  wrote $2 for a product listed at $160 and $7 for one at $280. The game builds
  its offers from the listed price, so customers then began asking for
  forty-five units at a few hundred dollars. A written price cannot be undone.

## [0.5.0] — 2026-09-18

### Fixed

- **Deals went through at the customer's own price with no negotiation at all.**
  Accepting ran before countering, so the offer was taken before the negotiation
  ever saw it — and because a customer with nothing on the table is not worth a
  log line, the feature that never worked was also the quietest thing in the log.
- **The window the clock is already in is no longer used.** A deal taken at
  14:25 went into that same afternoon. Where the only allowed window is the one
  the clock is in, nothing is scheduled until it ends: the game is told a time of
  day and decides the date itself, so there is no way to ask for tomorrow's.

## [0.4.0] — 2026-09-18

### Fixed

- **Every negotiation answered zero, at every price, for four sessions.** The
  offer was spelled out as unpackaged product, and no customer takes that at any
  price — including free. It is put in the smallest packaging the game allows for
  it before the question is asked.
- **Handing over refused while the goods were in the pocket.** A fresh install
  delivered only the exact grade ordered, so a stock of Heavenly against a
  contract for Standard was refused. Using a higher grade is what a fresh
  install does now, which is what the app always showed.
- The settings page could run up into the map's heading. What the app could not
  measure is now named in the log.

## [0.3.0] — 2026-09-18

### Added

- **Installing resets the settings.** Preferences are written to a file the
  first time the mod runs and read back over the defaults for ever after, so
  switches from weeks-old sessions survived every upgrade. Both install paths in
  the README say how to clear them.

### Fixed

- `Dealcraft.Core.dll` is installed to `UserLibs`, and a stale copy left in
  `Mods` is removed.

## [0.2.0] — 2026-09-17

First published release.
