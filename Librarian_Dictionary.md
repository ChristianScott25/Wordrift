# 📕 The Librarian Dictionary

> **Every librarian, in full.** `Wordrift_Encyclopedia.md` explains what a librarian *is* and
> how they fit into a run (§6); this is the per-character reference — what each one does, what
> its numbers are, how it actually plays, and the things about it that aren't obvious.
>
> **Keeping this current is a standing job.** A librarian added, retuned or cut is a change to
> this file in the same turn as the code — same rule as the Encyclopedia.

**Last updated:** 2026-09-03 · eight librarians
**Status:** the roster is a first pass. Every one of them is a *restriction* (see [Open](#open))

### How to read this

| Marker | Meaning |
|:--:|---|
| 🎯 | **Intent.** What the rule is *for* — the decision or feeling it's meant to create. |
| ⚠️ | **Watch out.** A known sharp edge, usually about a round that can't be finished. |
| ❓ | **Undecided.** A real design question left open. |
| 🚧 | **Temporary.** A placeholder value, not to be balanced around. |

Every number lives in that librarian's own asset in `Assets/GameData/Librarians/` and is
tunable in the Inspector. Each one **writes its own description from those numbers**
(`Librarian.PowerText`), so retuning a value can never leave the on-screen text describing the
old rule. `powerOverride` on any asset replaces the wording if you want to hand-write it.

### The rules that apply to all of them

- **Every third round is a librarian round** *(`librarianEveryRounds`)*, and clearing one
  **pays double** *(`librarianPayoutMultiplier`)*.
- **Which one turns up is part of the run's seed**, and **none repeats until all eight have been
  seen.** So rounds 3, 6, 9 … 24 are all different, and the cycle restarts at 27.
- **A word a librarian won't take can't be played at all.** It doesn't score zero and it doesn't
  cost a move — ENTER simply won't light up, and the librarian's reason is written under the
  selected word. Nothing is spent finding out.
- **A librarian is only ever asked about words the dictionary already accepted**, so a reason
  never ends up captioning a plain non-word.
- **Nothing warns you before the round begins.** You find out when you get there. ❓

### Contents

| | Librarian | In one line | Seam it uses |
|:--:|---|---|---|
| 1 | [The Grandiloquent](#1-the-grandiloquent) | Words must be 5+ letters | `Refuse` |
| 2 | [The Cataloguer](#2-the-cataloguer) | Every word a different length | `Refuse` |
| 3 | [The Conformist](#3-the-conformist) | Every word the *same* length | `Refuse` |
| 4 | [The Abridged](#4-the-abridged) | No letter twice in one word | `Refuse` |
| 5 | [The Censor](#5-the-censor) | One letter banned, rolled per round | `Apply` + `Refuse` |
| 6 | [The Redactor](#6-the-redactor) | No discards | `Apply` |
| 7 | [The Insatiable](#7-the-insatiable) | Score target ×3 | `Apply` |
| 8 | [The Critic](#8-the-critic) | −25% Points and Mult | `Score` |

---

## 1. The Grandiloquent

**Words must be 5 letters or longer.** *(`minimumLength`, default 5 — `Librarian_Grandiloquent.asset`)*

Refusal reads: *"Too short — 5 letters or longer."*

Your reliable three-letter filler is gone; every word has to be a real find. On a 5×5 board a
five-letter word is a genuine hunt, so this round is usually decided by two or three big words
rather than a steady drip.

**It does NOT raise `ModeConfig.minWordLength`**, and that's deliberate: that number is the zero
point of the length-multiplier curve, so moving it would silently re-price every word in the
game as well as banning the short ones. The Grandiloquent refuses words; it doesn't redefine
what a word is worth.

⚠️ The likeliest of all eight to deadlock a round — see [Open](#open).

---

## 2. The Cataloguer

**Every word must be a different length from every word before it.**

Refusal reads: *"Already played a 4-letter word."*

One 3, one 4, one 5, one 6 — and it tightens from both ends as you go. Early words are free;
by your fourth you are hunting a specific length rather than the best word on the board.

Stateless, and it needs no save support: it reads the words already played this round, which
are in the round snapshot already.

🎯 It rewards *range*. A run that only ever finds four-letter words scores once here.

---

## 3. The Conformist

**Your first word sets the length. Every word after it must be that same length.**

Refusal reads: *"Locked to 4-letter words this round."*

The exact inverse of The Cataloguer, and the pair is deliberate — they teach the same board in
opposite directions, so a run that has learned to hunt one is worst-prepared for the other.

🎯 The whole round is decided by one choice made before you know what the board will give you.
Open with a three and it's threes all round; open with a six and you have promised to keep
finding sixes.

**Implementation note worth keeping.** It never records *which* length was chosen, because under
its own rule every word already played has that length — so any of them answers the question.
That isn't just tidiness: `WordCheck.WordsThisRound` is a **set** and has no order, so "the first
word" is not something it could read even if it wanted to. "Every word so far agrees" is the same
rule, and it's order-free.

---

## 4. The Abridged

**No word may use the same letter twice.**

Refusal reads: *"No repeated letters — E twice."*

**Within one word only** — a letter refused here is completely free in your next word. So this
narrows every word without ever spending anything, which makes it the mildest of the word-rule
librarians.

🎯 It takes out the words you reach for without thinking: doubles (LETTER, BOOKS), and most
plurals where the S repeats a letter already used. The board reads differently rather than
merely worse.

The cheapest librarian there is — it doesn't even read the round, only the word in front of it.

---

## 5. The Censor

**One letter is banned for the round**, rolled when the round starts and **named on screen**:
*"The letter E may not be used."*

Refusal reads: *"E is banned this round."*

**⭐ The banned letter is weighted by what's in your bag.** It's drawn from a pool holding **one
entry per tile**, not one entry per distinct letter — so with a standard bag a banned E (13
tiles) is thirteen times likelier than a banned Z (1 tile). That is the entire point: a banned Z
is a shrug, and a boss round shouldn't be a shrug. It also means the letter tracks *your* bag —
if a run has been stacking vowels, the vowels are what's at risk.

**The letter is part of the seed, and it is never saved.** It's drawn from a stream keyed to the
round number, so quitting and resuming a Censor round re-derives exactly the same letter with
nothing written to disk. The draw happens once, in `Apply`, and nowhere else — drawing it in
`Refuse` would re-roll it on every keystroke.

**This is the only librarian that chooses anything**, and it's why `RoundRules` carries a random
stream, a letter pool, and a `Note` for the answer. Anything else that needs a per-round choice
uses the same three fields rather than inventing its own.

⚠️ It can strike out the letter the board happens to be full of, which is another route into the
dead-board problem — see [Open](#open).

---

## 6. The Redactor

**No discards.** *(`discards`, default 0 — `Librarian_Redactor.asset`)*

Nothing changes about scoring or about which words are legal; the escape hatch is simply shut. A
bad board is yours to solve.

It's a **limit, never a grant** — it takes the minimum of your allowance and its own number, so
setting it to 3 on a round that only had 2 discards leaves you with 2.

⚠️ Discards are the intended way out of a board that won't spell anything, so this is the
librarian that most directly removes the answer to the dead-board problem.

---

## 7. The Insatiable

**Score target ×3.** *(`targetMultiplier`, default 3 — `Librarian_Insatiable.asset`)*

The only librarian that changes nothing about *how* you play — every word you could have played,
you still can. It only moves the bar.

🎯 The clean test of whether a run's scoring is actually scaling. Restrictions ask whether you
can adapt; this asks whether you have built anything.

It multiplies a **factor**, never sets a number, so "three times whatever this round was going to
ask for" stays meaningful on round 3 and on round 30.

⚠️ **The shop's NEXT TARGET line doesn't know about it.** The next round's librarian isn't drawn
until you press CONTINUE, so the shop advertises the base target and the round then opens asking
for three times that. Known, undecided — see [Open](#open).

---

## 8. The Critic

**Every word loses 25% of its Points and its Mult.** *(`penalty` 0.25, `floor` 1 —
`Librarian_Critic.asset`)*

🎯 Like The Insatiable, it changes nothing about which words are legal — you play the round
exactly as you would have, and come up short. Unlike The Insatiable, it scales with what you've
built rather than against it.

Four things about how the cut is taken:

- **It runs after your bookmarks**, not before. It taxes what you built, so a run with strong
  bookmarks loses more in absolute terms and the same amount proportionally — which is what a
  percentage is for.
- **Points rounds UP.** A 10-point word keeps 8, not 7.
- **Mult is not rounded**, only floored. The length curve deals in 1.5s, and rounding the
  multiplier to whole numbers would quietly flatten the very curve this is meant to be taxing.
  ❓ Whether it should round is open.
- **Neither number goes below 1** *(`floor`)*, so a word is always worth something.

**It is not previewed.** The live POINTS × MULT readout shows the word untaxed; the cut lands as
its own named beat — **THE CRITIC −25%** — in the score walk-through after ENTER. That's the same
bargain bookmarks make, but ❓ it's a more questionable one for a *penalty*: you plan every word
against a number that is always 25% too high. See [Open](#open).

**The first librarian to use the scoring hook**, and the reason `Librarian.Score(ScoringContext)`
exists. Anything else that wants to change what a word is worth uses that same turn.

---

## Open

Questions that belong to the roster as a whole rather than to any one librarian.

- ⚠️ **A librarian round can lock.** If the bag is empty, no legal word is left on the board, and
  the discards are gone, nothing ends the round — moves only tick down when a word is played.
  The Grandiloquent, The Conformist and The Censor all make it likelier by refusing words, and
  The Redactor removes the discards that were the way out. The exits on the table are a forfeit
  button, counting a dead board as a loss, or detecting one. **Undecided.**
- ❓ **The Critic isn't previewed.** Bookmarks aren't previewed because the reveal is the payoff;
  a penalty has no payoff, and planning against a number that's always wrong is a different
  thing. Showing the round's rule in the live preview (while still hiding bookmarks) is a
  defensible split — the librarian's tax is imposed on you, your bookmarks are yours.
- ❓ **The shop promises a target it can't keep.** Its NEXT TARGET line reads the run's curve, but
  the next round's librarian isn't drawn until CONTINUE. Drawing it a round early would fix the
  line *and* let the shop announce who's next, Balatro-style; the alternative is for the line to
  stop claiming to know.
- ❓ **Nothing scales a librarian to the round it lands on.** The Insatiable's ×3 is the same
  demand on round 3 as on round 30, and The Critic's 25% is flat.
- ❓ **Every one of them is a restriction.** All eight take something away; none gives anything
  back beyond the doubled payout. A librarian that *changes* the game rather than narrowing it —
  a different board, a different bag, tiles that behave oddly — is the obvious missing shape.
  `RoundRules` already has room for it; the board's refill policy is the nearest untouched lever.
- ❓ **"Librarian" is a costume.** The noun lives in one config field *(`librarianLabel`)* and
  each name in its own asset, so the whole cast could become exams, critics or editors without
  touching the game. Nothing about the roster below assumes the library.

## Pitched, not built

Kept here so they don't get re-invented from scratch:

- **The Overdue** — your discard allowance isn't refilled; you start with whatever you had left.
  Needs discards to carry between rounds, which they currently don't.
- **The Miser** — the round pays nothing. A pure tax: clear it and you gain only survival.
  Interesting because it inverts the "librarians pay double" promise.
- **The Levelled** — every word scores its base points; the length multiplier is ignored. Guts
  long words specifically. Wants the `Score` hook.
- **The Closed Stacks** — the board never refills. Twenty-five tiles and that's the round. Wants
  `IRefillPolicy` on `RoundRules`, which is the seam that already exists and is unused.

---

*Adding one: `ARCHITECTURE.md` → "A librarian (a boss round)". Subclass `Librarian`, add a line
to `Assets/Editor/LibrarianSetup.cs`, run `Word Crush → Create Librarian Assets`, then write it
up here.*
