# What Dealcraft must do, as an evening of play

Status: **Awaiting the owner's approval.** Nothing is implemented against these
until he has read them and said yes.

These are written for a player, not for a programmer. No class names, no
addresses, no settings keys. If a line here cannot be checked by looking at the
screen or the phone, it does not belong in this file.

Every scenario names the switches first, then what happens, in order.

---

## 1. The best price, from the text message to the money

**My settings**

- When: Morning ✅ Afternoon ✅ Night ☐ Late Night ☐
- Negotiating: on — send only if *90% sure of a yes*
- Handing over: on — *use whole packages, wasting up to 0 units*
- OG Kush is listed at $52. **The listed price is the floor; there is no switch
  for it and there never was a reason for one.**

**What happens**

1. 10:15 in the morning. Beth Penn texts: she wants **8 OG Kush for $640**.
2. Dealcraft works out, without contacting her, that she would still say yes to
   **$760** with a 90% chance. It sends that, once.
3. Beth accepts.
4. The deal is set for the **afternoon** — the next window I allow that the game
   is offering. Not "the one whose turn it was": there is no turn.
5. At 4:30pm I walk to her with 8 packaged OG Kush in my pockets.
6. The handover screen opens, fills itself with the right 8, and finishes.
7. **$760 lands.** The app's handover row says the deal is done.

**What I must be able to see**

- On the Automation tab, the Negotiating block says `running`.
- Before I get there, Beth's row says how far away I am.
- If I am carrying nothing, her row says `carrying 0 of 8` — not "waiting".

**If Beth says no**

Nothing else is sent. She is left alone for that offer, and her row says so. I
am not asked again and Dealcraft does not try a second price.

---

## 2. A whole jar out of the door, with my price floor holding

**My settings**

- Negotiating: on — send only if *90% sure of a yes*
- Handing over: on — *use whole packages, wasting up to 4 units*
- OG Kush listed at **$52**.

**What happens**

1. Chloe Bowers texts: she wants **3 OG Kush for $120**. That is $40 each —
   below my floor.
2. Dealcraft will not answer at $40 a unit. And I am carrying a **jar of five**,
   nothing smaller, so three units cannot leave my pockets anyway.
3. It negotiates for **5 at $275** ($55 each), which clears my floor and which
   Chloe takes with a 90% chance. It sends that once.
4. Chloe accepts. The jar goes as a jar, and every unit in it was above my floor.

**Why this is not "aim for five"**

I never typed a five. The five is the jar in my pocket. The old design had a
target-quantity number and the owner deleted it:

> *"Was macht es denn überhaupt noch Sinn, auf Menge zu optimieren? Eigentlich
> schlägt doch Preisoptimieren alles."*

There is one goal — the most money — and the packaging rounds it to something
that can physically leave the bag.

**If the jar were a brick**

Twenty units against an order of three is seventeen wasted, far past my limit of
four. Dealcraft does not round up to it. It negotiates the three and, if it
cannot hand three over, says so on the row.

## 3. The same thing, when my floor makes it impossible

**My settings** — as scenario 2, but OG Kush is listed at **$80**.

**What happens**

1. Chloe texts wanting 3 at $120.
2. There is no price at or above $80 a unit that Chloe is 90% likely to accept.
3. **Dealcraft sends nothing.** It does not counter at a price she will refuse,
   because refusing costs me the $120 that was already on the table.
4. The Negotiating block says `nothing clears your floor`.

That one line is the whole point. I can see why nothing is happening without
opening a log file, and I can fix it by lowering the price or the confidence.

---

## 4. Handing over when I only have the bigger packaging

**My settings** — Handing over: on.

**Case A — *wasting up to 0 units* (exact only)**

Chloe's contract asks for **4**. I am carrying four baggies and one jar of five.
Dealcraft hands over the **four baggies**. Exactly four leave my pockets.

**Case B — *wasting up to 4 units***

Same contract, same pockets. Dealcraft hands over the **jar of five**. Five
leave my pockets, the contract is paid, and the row says
`5 units for 4` so I can see what it cost me.

**Case C — only the jar, in both settings**

I am carrying only the jar of five. Packaging cannot be split, so the jar goes
either way. The row says `5 units for 4`.

---

## 5. A request at night, when I do not deal at night

**My settings** — When: Morning ✅ Afternoon ✅ Night ☐ Late Night ☐

**What happens**

1. 11:40pm. Someone texts wanting a deal.
2. Dealcraft does **not** schedule it into the night.
3. It waits for a window I allowed and takes the next one the game offers.
4. Until then the Accept-offers block says
   `the game is offering none of the allowed windows right now` — so I know it
   is waiting, not broken.

**With all four windows off**

The block says `no window is allowed` and nothing is scheduled at all. Not
silence — a sentence.

---

## 6. A price that has quietly cost me customers

**What happens**

1. I have OG Kush listed at **$52**.
2. On the Products tab its row reads:
   `listed $52 · suggested $48 · clears $41 · priced out 3 of 14`
3. Three of my fourteen customers cannot afford a single unit at $52 per
   order, so they never text me about it at all.
4. I drop the price to $41 and the row reads `priced out 0 of 14`.

This is the scenario I would never have found by playing. Over-pricing does not
produce refusals — it produces silence, and the row is the only place that
silence becomes visible.

---

## 7. Everything off

A fresh install, nothing switched on. Dealcraft sends nothing, schedules
nothing, hands over nothing and writes no price anywhere. The Automation tab is
four lines and every block says `off`.

The app is the only thing that changed about my game.

---

## 8. My friends

**Rewritten. The old version of this scenario was false**, and the project's own
disassembly disproved it while it sat in the file the owner was being asked to
approve. It claimed a handover Dealcraft completed would pay a guest and show on
his screen. `docs/guest-handover-truth.md` established the opposite: cash is
local, so the payment lands wherever the handover runs, the completion popup is
welded to the same machine, and the broadcast daily summary would credit the
guest money he never received.

### 8a — Benni has not installed anything

I am the host. Benni joins.

1. Every offer that arrives is answered by **my** Dealcraft, at the price it
   worked out. Benni's deals are negotiated for him without him installing a
   thing, because the customer roster and the contract list are one shared list.
2. Deals land in the windows I allow.
3. **Benni hands over by hand**, exactly as he does today. Nothing takes his
   goods and nothing pays him money he did not earn.
4. He never sees the Dealcraft app and nothing about his game differs from
   vanilla.

### 8b — Benni installs it too

1. Everything in 8a still holds.
2. **Benni's own copy does Benni's own handovers**, out of his pockets, and the
   game pays him and shows him the popup — the same path his Done button takes.
3. Negotiating still runs on my machine only. Two installs must not answer one
   conversation twice.
4. If Benni hosts next time, his copy negotiates and mine stands down.

### 8c — What must never happen

No copy of Dealcraft writes into another player's inventory, cash or summary.
The rule in `CLAUDE.md` is not about who may install; it is that nobody fakes an
effect a player did not cause.

**This one is tested by the owner with real players.** Not claimed as passing
before that.

## What "passing" means

For 1 to 7: photographed, in a running game, on the screens named. A test that
only passes in code does not pass.

For 8: played with friends. Nothing else counts.
