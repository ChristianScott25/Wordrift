# 📖 The Wordrift Encyclopedia

> **The game, not the code.** How Wordrift is played, what the rules are, and what every
> number currently is. `ARCHITECTURE.md` explains how it's built — this explains what it *is*.

**Last updated:** 2026-09-16 · the shop rebuilt — five slots, set prices, and **checkouts** (permanent run-wide perks)
**Status:** playable demo in active design — the loop works end to end; the content doesn't exist yet

### How to read this

| Marker | Meaning |
|:--:|---|
| 🚧 | **Temporary.** Exists only so something can be played and seen. It *will* be replaced, and nothing should be balanced around it. |
| ❓ | **Undecided.** A real design question, deliberately left open. |
| 🎯 | **Intent.** What a rule is *for* — the feeling or decision it's meant to create. |

Every number here is authored in an asset and tunable in the Inspector; the file that owns it
is named in *(italics)*.

### Contents

1. [The game in one minute](#1-the-game-in-one-minute)
2. [How it's meant to be played](#2-how-its-meant-to-be-played)
3. [Making words](#3-making-words)
4. [Tiles](#4-tiles)
5. [Bookmarks](#5-bookmarks)
6. [The run](#6-the-run)
7. [Money](#7-money)
8. [The shop](#8-the-shop)
9. [Checkouts](#9-checkouts)
10. [Modes](#10-modes)
11. [Every number, in one place](#11-every-number-in-one-place)
12. [Built · planned · open](#12-built--planned--open)

---

## 1. The game in one minute

You drag across a 5×5 grid of letters to spell words. A valid word explodes off the board and
new tiles fall in.

That's the arcade game. Wordrift wraps it in a **run**: a sequence of rounds, each one asking
for a score you have to reach inside a fixed number of words. Clear a round and you're paid;
spend the money in a shop; go again against a higher number. Miss it once and the run is over
and you start again from nothing.

The twist that makes it a roguelike rather than a word game with a timer: **you own your
letters.** A run gives you a bag of 104 tiles, and the shop lets you permanently improve
individual tiles inside it. The E you gild in round 1 is the same E that comes back to you in
round 5.

---

## 2. How it's meant to be played

### The moment-to-moment

You're looking at 25 letters and hunting for the best word you can *see*, not the best word
that exists. Every word you take rearranges the board underneath you, so the board you get
next is a consequence of the word you just played — take the long word across the bottom and
half the grid collapses; take a short one up top and almost nothing moves.

🎯 **The core tension is greed vs. progress.** You have 20 words (§9 can buy you more). A
three-letter word is always available; a big multiplied one usually isn't yet. Spending a word
on something small is spending a scarce resource on almost nothing.

### The round

Every round is the same question with a bigger number: **can you reach the target before you
run out of words?**

You will almost always clear round 1 without thinking. That's on purpose — the early rounds
are where you're supposed to feel comfortable and bank money, because the targets grow by half
again every round after the third while your ability to score doesn't grow on its own. The
run kills you somewhere in the middle, when the target has outrun the letters you own.

🎯 **The moment the target is hit, the round stops.** Unused words are worth money, so a round
isn't "score as much as possible" — it's "get there fast". Overshooting the target buys you
nothing. That's the decision the round hinges on: keep grinding safe little words, or hold out
for the one big word that ends it early and pays for it.

### The run

Rounds don't reset you. Money carries. Upgrades carry. **Your tile bag is the only thing
that grows**, so the whole run is a race between the target curve going up and your tiles
getting better.

🎯 **The shop is where a run is actually won or lost.** Points come from good words, but good
words come from good tiles — and tiles only get better if you paid for it. A run that spends
nothing dies around the point the targets start compounding.

### What a session looks like

```
   MAIN MENU
       │
       ▼
   ROUND 1  ──── cleared ────►  💰 PAID  ──►  🛒 SHOP  ──┐
       │                                                 │
     missed                                              │
       │                                              ROUND 2  ──── cleared ──► …
       ▼                                                 │
   RUN OVER  ◄─────────────── missed ────────────────────┘
       │
       └──►  PLAY AGAIN  =  a brand new run, round 1, $0, a stock bag
```

A run is currently open-ended: there is no boss round and no "you win" — you play until a
target beats you. ❓ That ending is undesigned.

### Where the fun is supposed to come from

- **Recognition** — spotting a word nobody would find, in a grid that only exists for a second.
- **Compounding** — a tile or bookmark you bought in round 1 paying off for the rest of the run.
- **Escalation** — the target curve going up faster than you're comfortable with.
- **Spending** — a shelf of five things you can afford two of. The shop stocks tile upgrades,
  bookmarks and checkouts against one wallet, so every visit makes you give something up.
  🚧 Six bookmarks and six checkouts is a start, not a space worth exploring yet — the
  content is still the thinnest part of the design.

---

## 3. Making words

**The board** is a 5×5 grid, always full at the start of a round. *(`Board_5x5.asset`)*

### Selecting tiles

**Two ways, same result.** Drag across touching tiles, or tap them one at a time. **Diagonals
count**, so a tile has up to eight neighbours, and a tile can't be used twice in one word.
Selected tiles are lit and boxed.

**Lifting your finger does nothing.** The selection stays on the board until you act on it —
that pause is the whole point, because it's where you decide between playing the word and
throwing it away.

| You do | What happens |
|---|---|
| Tap an empty tile next to your last one | It joins the end |
| Tap a tile that isn't touching your selection | The selection clears and starts again from that tile |
| Tap a tile already selected | It and everything after it drop off — tap the last one to undo one letter |
| Drag onto a tile that isn't adjacent | **Ignored.** A fast swipe skips tiles, and wiping your selection over a sampling gap would be maddening |
| Tap the board background | Nothing. Deselecting is done by tapping a tile, so a stray tap can't cost you a word |

**Tiles in motion can't be grabbed.** A tile has to settle before it will respond, so nothing
slides out from under your finger mid-word.

### The two buttons

They appear as soon as anything is selected, and they are the only way to act on it.

**ENTER** plays the selection as a word. It is **disabled unless the selection is a valid
word** — at least **3 letters** *(`minWordLength`)* — letters, so a `CH` tile and an `A` are
enough — and in the dictionary, about 178,800
English words. You can no longer submit a bad word at all, so nothing flashes red any more and
nothing is ever penalised for a wrong guess. *(`rejectedWordsCostMoves` is now unreachable.)*

The dictionary was **replaced on 2026-09-08**. The old one was Webster's Second (1934): it
knew `aalii` and `abacay` but refused `cats`, `houses`, `plays`, `boxes` — and `words`,
`tiles` and `points`. Regular plurals and verb endings simply weren't in it. The new one is a
Scrabble tournament lexicon, so **every inflection works**: if the singular is a word, the
plural is too. It's frozen in 2006, so a short hand-kept list *(`wordlist-extra.txt`)* carries
the modern words it never had — `internet`, `selfie`, `emoji`, `podcast`, `wifi`, `zen`. Add
to that file whenever a word you'd expect to work doesn't.

It also predates the 2020 removal of slurs from tournament word lists, so it arrived with them
all playable and scorable. **327 words are blocked** *(`wordlist-blocked.txt`)* — slurs and
profanity — and the block is applied last, so a blocked word is refused whatever the other two
lists say. Words with an innocent meaning were deliberately kept: `prick`, `tit` (the bird),
`boob` (a mistake), `slag`, `shag`, `spunk`, `snatch`, `hooker`, `bastard`, `dike`, `queer`,
`gypsy`, `idiot`, `moron`, `hell`, `damn`.

**DISCARD** throws the selected tiles off the board without scoring them. It shows what it
would cost and what you have left — `DISCARD 3   5 LEFT` — and is **disabled when you've
selected more tiles than you have discards remaining**.

**When tiles leave the board** — played or discarded — they demolish, everything above them
falls straight down, and new tiles drop from the top to fill the gaps, for as long as the bag
has tiles left to give.

### Discarding

You may throw away **5 tiles per round** *(`discardsPerRound`)*.

- It's counted in **tiles, not uses**. One five-tile discard and five single-tile discards
  both spend the whole allowance.
- **It costs no move.** The 20-word budget is for words; this is a separate resource, so
  discarding stays an escape hatch rather than a second tax.
- Discarded tiles are **spent exactly like played ones**: gone for this round, and back in
  the bag at the start of the next. Nothing is destroyed permanently.
- The allowance **refills every round** and never carries over. Unused discards are worth
  nothing — unlike unused moves, which pay $1 each.
- **A checkout can raise it, permanently.** Second Thoughts adds 2 tiles to every round for
  the rest of the run — see §9.
- **A librarian can take it away**, and it beats the checkout. The Redactor's whole rule is
  that this number is 0 for the round, whatever you own — see §6.

🎯 Discarding is what you do when the board won't give you a word: dump the four consonants
strangling a corner and let something else fall in. It's also the only partial answer to a
board full of tiles that spell nothing — see §6.

### How a word is scored

**A score is two numbers that multiply.**

```
        POINTS          ×          MULT
   what the tiles              how long the
     are worth                  word is
```

**POINTS** is each tile's base score put through its own letter multipliers (2L, 3L), summed,
then multiplied by every word multiplier (2W, 3W) in the word. A 2W is part of what the tiles
are worth, so it lives on this side.

**MULT** comes from **word length in LETTERS**, and nothing else to start with — letters, not
tiles, which stopped being the same number the day multi-letter tiles arrived (§4):

| Letters | 3 | 4 | 5 | 6 | 7 | each further letter |
|---|--:|--:|--:|--:|--:|--:|
| **Mult** | ×1 | ×1.5 | ×2 | ×2.5 | ×3 | +0.5 |

*(`lengthMultipliers` and `multiplierPerExtraLetter`.)*

🎯 This is the whole reason to reach for a longer word. Five letters is worth double what three
is before a single tile is upgraded, and because it's the **multiplier** side, length makes
every points-side upgrade you own worth more too.

**Both numbers are visible while you select**, updating with every tile — so you know exactly
what a word pays before you commit to it.

### Then your bookmarks, one at a time

When you press ENTER the two numbers are locked in, and each bookmark you own takes its turn
**in the order their cards sit, left to right**, pushing one side or the other. Each is called out as it
lands — `BOOKEND   ×2 MULT` — and the numbers move as you watch.

There are three shapes a bookmark can have, and the difference matters:

| Shape | Example | Note |
|---|---|---|
| **+Points** | Deja Vu, +10 | Lands *after* your 2W/3W has already been applied |
| **+Mult** | Vowel Fanatic, +4 | Additive |
| **×Mult** | Bookend, ×2 | Multiplies everything the +Mult bookmarks built up |

**This is why bookmark order matters.** `+4 Mult` then `×2 Mult` is not the same as `×2` then
`+4` — the first doubles the four, the second doesn't. They run left to right in the order the
cards sit below the board, and **you choose that order** by dragging them (§5).

**Finally**, `POINTS × MULT` is the score — always, with nothing applied afterwards. *(The
mode-wide `scoreMultiplier`, currently ×1, takes its turn as one more step, so what the readout
multiplies out is exactly what you're paid.)*

**A word can't score more than a billion, and can't score less than nothing.** Multipliers
stack without limit — nothing stops you buying a fourth 3W onto the same tile — so the numbers
are held to a ceiling rather than allowed to run off the end of the counter and come back
*negative*, which is what used to happen. Every intermediate number is capped the same way, so
the ceiling can be reached but never passed. 🚧 A billion is a safety rail, not a design
choice: if the game ever genuinely wants Balatro-scale numbers, this is the wall to move.

> **Worked example.** `EYE` = E(1) + Y(4) + E(1) = **5 points**, three letters so **×1**.
> Nothing owned: **5**.
> Now own Vowel Fanatic (2 vowels beats 1 consonant → **+4 Mult**) and Bookend (starts and ends
> with E → **×2 Mult**), bought in that order:
>
> ```
> 5 × 1                 base
> 5 × 5     VOWEL FANATIC  +4 MULT
> 5 × 10    BOOKEND        ×2 MULT     =  50
> ```
>
> Bought in the *other* order it would be `5 × 2` then `5 × 6` = **30**.

🎯 The walk-through only has beats if you own bookmarks, so early rounds resolve instantly and
the flourish grows as you earn things worth watching.

---

## 4. Tiles

A tile is **a letter, a base score, and any modifiers it carries** — and that identity belongs
to the tile itself, not to the letter. Two E's in your bag can be worth different amounts,
and only one of them may be gilded. This is what makes upgrading a *specific* tile meaningful.

**The catalog** *(`LetterSet_Scrabble.asset`)* defines every kind of tile that can exist, what
it's worth, and how common it is. The 26 letters are the Scrabble distribution — vowel-heavy,
one Q, one Z — but those numbers are a **ratio, not a count**: the bag is built by sharing them
out over however many tiles the bag is meant to hold (see §6). Alongside them sit the three
kinds the shop sells and the bag never deals on its own: **multi-letter tiles**, the **wild**,
and **choice tiles**.

### Modifiers

The badge on a tile. Four exist today:

| Badge | Effect | Shop price |
|:--:|---|--:|
| **2L** | doubles that tile's own letter score | $5 |
| **3L** | triples that tile's own letter score | $9 |
| **2W** | doubles the score of the whole word | $14 |
| **3W** | triples the score of the whole word | $22 |

The rules around them:

- **No tile ever spawns with a modifier.** The only way a tile has one is that you bought it,
  and it lasts the rest of the run.
- **A tile can carry up to three** *(`maxModifiersPerTile`)*. Letter multipliers stack in order
  (2L then 3L = ×6); word multipliers all multiply together, so a tile with 3W 3W 3W is ×27 on
  every word it appears in. A full tile stops being a target the shop can offer you, and if
  every tile in your bag fills up the upgrade rows disappear from the shop entirely.
- **A stacked tile draws one badge per modifier**, fanned right across the top of the tile.
  🚧 A first-pass visual, not the final treatment — three badges reach most of the way across
  and sit over the letter.
- **The number in a tile's corner is always its BASE score.** A 2L on an E still reads "1" —
  the badge is what tells you it doubles. This is deliberate: one number on the tile, one
  meaning.

🎯 Word multipliers are priced far above letter multipliers because they scale with the whole
word — a 3W on a common letter is the single most valuable thing in the shop.

### Multi-letter tiles

**Some tiles spell two letters.** A `CH` tile is one square on the board that puts *both*
letters into your word — so `CH·A·T` is the four-letter word CHAT off three tiles, and
`CH·A` is the perfectly legal three-letter word CHA.

They and the wild below are **what the shop sells outright**, one row a visit (§8), and you can
own as many as you like — buying a second CH just makes CH twice as likely to turn up.

| Tile | Worth | Price |
|:--:|--:|--:|
| **ER** | 3 | $8 |
| **IN** | 3 | $8 |
| **IE** | 3 | $8 |
| **ED** | 5 | $10 |
| **TH** | 8 | $14 |
| **SH** | 8 | $14 |
| **CH** | 11 | $17 |
| **QU** | 17 | $22 |

**What they're worth is a rule, not a list:** the two letters' own base scores added together
and multiplied by **1.5, rounded up** — because a tile you can only play where its pair fits is
harder to use than the two letters loose. `C`(3) + `H`(4) = 7, ×1.5 = **11**.

- **Word length counts LETTERS.** That is the whole reason to buy one: `CH` reaches a longer
  word off fewer tiles, and length is the multiplier side of the score. *(Discards still count
  **tiles** — that's a board cell, not a letter.)*
- **They never turn up on their own.** They sit in the catalog at spawn weight 0, so your
  starting bag of 104 is exactly what it always was. The only way to have one is to buy it.
- **They can be gilded like anything else.** A `QU` is a legitimate target for a 3W in a later
  visit, and a 3W'd QU is worth 51 points from one square. 🚧 That's the first combination
  likely to look silly once it's been played.
- **A librarian sees every letter on them.** The Censor can ban the `C` in your CH tile, and
  then the whole tile is unplayable for the round.

🎯 `ER` and `ED` are the cheap ones on purpose — they're not about points, they're about turning
a three-letter word into a four. `QU` is the opposite: it's the tile that makes your single Q
worth owning at all.

### Wild tiles

**A wild is a tile with no letter of its own.** It shows a **\*** and is worth **0 points**.
Drop it into a word and it becomes whichever single letter suits you best.

| Tile | Worth | Price |
|:--:|--:|--:|
| **\*** | 0 | $35 |

**It picks the best letter for you, every time.** The game works out every word your tiles
could spell, throws away the ones this round's librarian would refuse, and out of what's left
takes **the one that scores highest**. You never choose, and you never have to.

- **It shows `*` until the word is real.** Two tiles selected, or a combination that spells
  nothing — it stays a star. The moment the selection is a word, the tile's face flips to the
  letter it became, and flips back when you let go. The small **\*** in the corner, where a
  score would be, stays there always: that's how you know which tile it is.
- **It counts as one letter** toward the length multiplier, like any ordinary tile — so it
  lengthens a word without paying for it.
- **It beats a banned letter.** On a Censor round, a letter that would get the word refused is
  simply never chosen. If you're holding a wild, the Censor mostly can't touch you.
- **When nothing works, it still shows you a word.** If every letter that fits the dictionary
  would be refused by the librarian, the game shows the best of those anyway, in red, with the
  reason — rather than a star and no explanation.
- ⚠️ **It can't be upgraded.** The shop's tile-upgrade rows skip it: a 3W that fits into any
  word at all would be the strongest thing in the game by a distance.

🎯 **$35 is well above QU at $22 on purpose.** A wild is strictly more useful than any letter
pair — it fits everywhere, it dodges the boss, and it always takes the best answer. It's the
thing you save for, not the thing you buy on the way past.

❓ **You can't see why it chose what it chose.** With no bookmarks owned, every candidate scores
exactly the same, so it takes the alphabetically first — `C*T` is always CAT, never CUT. Once
you own Vowel Fanatic or Bookend the choice starts genuinely following your score, but nothing
on screen explains the difference. Letting you pick is the same piece of work as choosing which
tile an upgrade lands on.

### Choice tiles

**A choice tile becomes one of a few named letters, and nothing else.** An `A/E/I` tile is an
A, an E or an I — whichever makes the best word. A wild with a fence around it, which is why it
can be worth points and cost a fraction of one.

| Tile | Theme | Worth | Price |
|:--:|---|--:|--:|
| **R/S/T** | the common consonants | 0 | $24 |
| **L/N/R** | the common consonants | 0 | $22 |
| **B/C/P** | the hard consonants | 2 | $20 |
| **A/E/I** | the vowels | 0 | $18 |
| **F/H/W** | the 4-pointers | 3 | $18 |
| **K/V/Y** | the awkward ones | 3 | $15 |
| **D/G** | the 2-pointers | 1 | $12 |
| **J/X** | the 8-pointers | 6 | $12 |
| **Q/Z** | the 10-pointers | 7 | $12 |
| **O/U** | the other vowels | 0 | $10 |

**What they're worth is a rule, not a list** — the same shape as a pair's, pointing the other
way. A pair is *harder* to play than its letters loose, so it's worth them **summed and
multiplied by 1.5, rounded up**. A choice tile is *easier*, so it's worth its **cheapest option
times 0.75, rounded down**. `B/C/P`'s cheapest is 3, so it's 2. `Q/Z`'s is 10, so it's 7.

The groups are picked so their letters are worth about the same, which is what makes one number
honest for the whole group — and it means the roster doubles as a ladder of prices and scores
rather than ten versions of the same tile.

- **It plays as ONE letter.** This is the difference from a pair, and the thing to understand
  before buying: `CH` reaches a longer word off fewer tiles, a choice tile doesn't. It makes a
  word *possible*, not *longer*.
- **The four cheap-letter groups are worth nothing.** `R/S/T` at 0 points is not a mistake —
  what you're buying is that it always fits. At the other end, `Q/Z` at 7 points is the
  opposite trade: it barely opens up any new words, it just means your expensive letter is
  always playable.
- **It shows its letters until the word is real.** The tile reads `A/E/I`, and the moment the
  selection is a word its face flips to the letter it became — then back when you let go. Same
  behaviour as a wild's `*`, and the corner still shows what it's worth.
- **They never turn up on their own**, like the pairs and the wild: spawn weight 0, so the
  starting 104 is untouched. The only way to have one is to buy it.
- **They can be gilded — mostly.** A 3W on a `Q/Z` is 21 points from a tile that's always
  playable. But the shop will never offer a **2L or 3L for a 0-point tile**, because doubling
  nothing is nothing; those four groups can only take word multipliers.
- **A librarian sees every letter they offer**, but banning one doesn't kill the tile — an
  `A/E/I` with E banned is still an A or an I. That's the opposite of a `CH`, where banning
  either letter kills the whole tile for the round.

🎯 The prices run the other way to the scores on purpose. The flexible, worthless groups are the
expensive ones, because flexibility is what the tile is for. `Q/Z` is cheap because it's mostly
just seven points.

🚧 **The prices are a first pass**, like everything else on the shelf. Nothing here has been
balanced against a full run.

---

## 5. Bookmarks

**Bookmarks are the run's special abilities** — this game's version of Balatro's jokers. You
buy them in the shop, you keep them for the rest of the run, and they change how every word
scores from then on.

| Bookmark | What it does | Price |
|---|---|--:|
| **Bookend** | **×2 Mult** if the word starts and ends with the same letter | $12 |
| **Spine** | **×2 Mult** if no letter appears twice in the word | $16 |
| **Deja Vu** | **+10 Points** for a word you already spelled **this round** | $10 |
| **Vowel Fanatic** | **+4 Mult** if the word has more vowels than consonants | $14 |
| **Marginalia** | **+1 Mult** for every letter past the minimum word length | $13 |
| **Shorthand** | **+4 Mult** on a word of exactly the minimum length | $13 |

Every shape is represented, deliberately: Bookend and Spine are multiplicative because their
conditions are rare and hard to engineer; Vowel Fanatic, Marginalia and Shorthand are additive
because theirs are easy to hit; Deja Vu works on the points side entirely.

🎯 **Marginalia and Shorthand are opposites, and they share a pool on purpose.** One pays for
long words, the other only for the shortest legal one — so a shelf offering both is asking
which game you're playing. Owning both is close to owning neither.

**Spine is The Abridged, sold back to you.** That librarian bans repeated letters for a round;
this pays you for obeying the same rule forever. The boss is where you learn what it's worth.

The rules around them:

- **One of each, at most — and five in all.** The shop never offers a bookmark you already
  own. Once you're carrying five it still *offers* them: the row shows what it is, what it
  does and what it costs, and the buy button reads **BOOKMARKS FULL** and won't take your
  money. Being full and being broke are different problems, so they say different things.
  (With six bookmarks in the game the cap only ever blocks the sixth. It's built for a
  bigger roster than this one.)
- **They stack**, and the way they stack depends on their shapes — see the worked example
  in §3.
- **They fire left to right, and you choose the order.** Your bookmarks sit as cards below
  the board and again in the shop, and you **drag them to rearrange**. Vowel Fanatic before
  Bookend is `(×1 +4) ×2` = ×10; the other way round it's `(×1 ×2) +4` = ×6 — so where you
  slot a new one in is a real decision and not just where it landed. You can rearrange at
  any time, mid-round included.
- **Bookmarks die with the run**, like money and tile upgrades.

Details worth knowing:

- **Vowel Fanatic treats Y as a consonant.** `YOYO` is 2 vowels against 2 consonants, so it
  doesn't fire; `AREA` (3 v 1) does. It needs *strictly* more, so an even split pays nothing.
- **Deja Vu counts repeats within a round only** — the list resets when a new round starts.
  Nothing in the game stops you playing the same word twice, so this turns a quirk into a
  tactic: spell `EYE`, then spell it again for +10.
- **Each bookmark is named as it fires.** Pressing ENTER walks the two numbers forward one
  bookmark at a time — `BOOKEND   ×2 MULT` — rather than jumping to a total.

❓ **Editions** — Balatro's holographic / negative / foil upgrades applied to a joker — are
planned but not built. A bookmark you own is already stored as its own object rather than as a
pointer to the shop's copy, specifically so two copies can differ later.

## 6. The run

A **run** is a sequence of rounds. It starts fresh from the main menu, and it ends the first
time you fail a round. Everything you earn and upgrade lives for exactly one run — **nothing
carries between runs**, and there's no meta-progression.

### Your tile bag

You own a bag of **104 tiles** *(`tileBagSize`)* — a little over a Scrabble set, mixed in
Scrabble's proportions.

- Rounds draw from it **without replacement**. Once your E's are gone, no more E's arrive this
  round.
- **The opening board is paid for out of the bag** — 25 cells are dealt before your first
  move, leaving 79 tiles held back to refill with.
- **The full bag comes back at the start of every round.** Playing tiles doesn't lose them;
  the bag belongs to the run, not the round.
- When it runs dry, tiles stop falling and the board plays down toward empty.
- **Upgrades change the bag itself.** That's why a tile you gild in round 1 keeps coming back.

**How the mix is decided.** The letter catalog's weights are a *ratio*, and the bag size is a
separate number; the bag is built by sharing the ratio out over that many tiles, **with a
floor of at least one of every letter**. At 98 that reproduces Scrabble exactly. Above 98
the floor stops mattering and the mix simply tracks the ratio.

| | A | E | I | O | U | N | R | T | D L S | G | J K Q X Z |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| Scrabble (98) | 9 | 12 | 9 | 8 | 4 | 6 | 6 | 6 | 4 | 3 | 1 each |
| **Wordrift (104)** | **10** | **13** | **10** | **9** | **4** | **7** | **7** | **6** | **4** | **3** | **1 each** |

Vowel share: **44%**, against 43% for a real Scrabble bag. Slightly wetter, and the reason is
the rares — J K Q X Z are pinned at one apiece by the floor, so every extra tile above 98 goes
to a common letter and the vowels take their share of it.

⚠️ **Doubling the bag handed the round back to the move counter.** 104 tiles is roughly 34
three-letter words against a 20-word allowance, so a round now runs out of *moves* well before
it runs out of *tiles* — the reverse of where the 52-tile bag left it. Running dry is now the
exception rather than the norm. One Inspector field either way.

🎯 The bag is the run's real character sheet, and the reason the shop matters. Making it
*smaller* (fewer, better tiles) or *bigger* is an obvious future upgrade axis — `tileBagSize`
is exactly the number such an upgrade would turn, but nothing turns it yet.

### Your seed

Every run has one — eight characters, shown small in the bottom-left corner of the board and
the shop. It decides everything the game rolls: the order tiles come out of the bag, what the
shop offers, which tile an upgrade lands on.

- **It's fixed for the whole run.** Losing and starting again gives you a new one.
- **Each round deals independently.** Round 3's tiles are the same for a given seed however
  rounds 1 and 2 went, so a single round can be reproduced on its own.
- **Your choices aren't part of it.** Two runs on one seed diverge the moment you buy something
  different, because a purchase changes what there is to draw from.

🎯 It's there so a board can be reported and reproduced exactly — "seed 4K7PQW2M, round 3" — and
so two balance changes can be compared on identical tiles instead of by feel. ❓ There's nowhere
to type a seed in yet, so it's a developer tool for now rather than something you can share.

### Rounds and targets

Each round: reach the target within **20 words** *(`moves`)*.

| Round | 1 | 2 | 3 | 4 | 5 | 6+ |
|---|--:|--:|--:|--:|--:|--:|
| **Target** | 30 | 45 | 65 | 98 | 146 | ×1.5 each round |

*(`roundTargets` and `targetGrowth`. 🚧 Halved on 2026-08-25 and halved **again** on
2026-08-27 for testing — they are deliberately very soft right now, and a run should be
expected to go long.)*

**A round ends the instant the target is reached** *(`endOnTargetReached`)*, banking your
unused words rather than playing them out.

### Librarians

**Every third round, someone is watching.** *(`librarianEveryRounds`)* A librarian round plays
by one extra rule for that round only, announced on screen the whole time — their name, and
what they've decided. Clear it and it **pays double** *(`librarianPayoutMultiplier`)*.

| | Their rule | What it does to you |
|---|---|---|
| **The Grandiloquent** | Words must be **5 letters or longer** | Your reliable three-letter filler is gone. Every word has to be a real find. |
| **The Redactor** | **No discards** | Nothing changes about the scoring; the escape hatch is just shut. A bad board is yours to solve. |
| **The Insatiable** | **Score target ×3** | Nothing about how you play changes — only the bar. The one librarian that asks whether your run is actually scaling. |
| **The Conformist** | Your **first word sets the length**; every word after it must match | The whole round is decided by one choice made before you know what the board will give you. Open with a three and it's threes all round. |
| **The Abridged** | No word may use **the same letter twice** | Kills the words you reach for without thinking — doubles, most plurals. The letter is free again in your next word. |
| **The Censor** | **One letter is banned**, rolled when the round starts and named on screen | Weighted by what's actually in your bag, so it usually takes a letter you were counting on. |
| **The Critic** | Every word loses **25% of its Points and Mult** | Changes nothing about which words are legal — you play the round exactly as you would have, and come up short. |
| **The Dilapidated** | **Three spaces on the board are closed**, drawn when the round starts | The first librarian that changes the board rather than the rules. You route words around the holes, and tiles fall straight through them. |

**A word they won't take can't be played at all.** It doesn't score zero and it doesn't cost a
word — ENTER simply won't light up, and the reason is written under the word you selected
("Too short — 5 letters or longer"). Nothing is spent finding out.

**Which librarian turns up is part of your seed**, and **none repeats until you've met them
all.** With eight of them that means rounds 3 through 24 are all different, and the cycle starts
over at round 27 — where the same one *can* immediately reappear, since by then everyone has
been seen.

**The Censor's letter is part of the seed too**, and it's drawn from your bag one entry per
letter you own — so a banned E is far likelier than a banned Z, which is the whole point. Quit and resume
a Censor round and it's the same letter; it isn't stored anywhere, it's re-derived. **The
Dilapidated's holes work exactly the same way** — the same three spaces every time you come back
to that round.

**The holes can never wall a space off.** They're drawn so that **every remaining space keeps at
least two neighbours** *(`minNeighbours`)* — otherwise three of them around a corner would strand
the tile sitting there, where it could never be played or discarded.

**A closed space is closed for the whole round.** Nothing ever spawns in one, and no word can be
chained through one — two tiles either side of a hole are not neighbours, so you go around. When
the tiles below a hole are used up, the tiles above it **fall straight through** to fill the
space below and stop above it once that space is full. The hole never fills.

**The Critic is taken after your bookmarks**, not before — it taxes what you built. It also
doesn't show in the live POINTS × MULT preview; you see the cut land as its own beat in the
score walk-through after ENTER, the same way bookmarks do.

📕 **`Librarian_Dictionary.md` has all eight in full** — each one's numbers, how it plays, and
the details that don't fit a table. The rest of this section is what's true of them all.

❓ Nothing warns you before the round begins — you find out when you get there. ⚠️ The shop's
**NEXT TARGET** line therefore shows the round's *base* target, so an Insatiable round arrives
asking for three times what the shop just promised.

### How a round ends

| Ending | What happened | Result |
|---|---|---|
| ✅ **Cleared** | Target reached | Paid, then the shop. Run continues. |
| ❌ **Out of moves** | 20 words used, target missed | **Run over.** |
| ❌ **Out of tiles** | Bag empty *and* too few tiles left on the board to make any word | **Run over.** |

That last one exists so a round can't stall forever with moves left and nothing to spend them
on. ❓ The softer version — tiles remain but no word can be made from them — still isn't
*detected*, but it now has an exit: **discard your way out of it** (§3), up to five tiles a
round. Whether five is enough to unstick a genuinely dead board is untested.

⚠️ **Librarians make that softer version much likelier.** The Grandiloquent will refuse every
word on a board that can only manage threes and fours, The Conformist can lock you to a length
the board stops offering, The Censor can strike out the letter the board is full of, The
Dilapidated cuts the board down to 22 spaces and breaks up the paths between them, and The
Redactor takes away the discards that were the way out. On a librarian round with an empty bag
and no legal word left, there is currently no way to end the round — see §12.

**PLAY AGAIN starts a completely new run**: round 1, a stock bag, $0.

### Picking up where you left off

**Your run saves itself.** You are never asked, and there is no save button. Close the app —
or have the phone close it for you — and the main menu shows **CONTINUE** when you come back.

- **It resumes exactly, not approximately.** The same board, the same tiles left in the bag, the
  same score, moves and discards. Quit mid-round and you come back mid-round.
- **Quit in the shop and you come back to the shop**, with the same money and the same shelf at
  the same prices. Anything you already bought is still there, still reading SOLD.
- **It saves after every word, after every discard, and after every purchase.** The one thing you
  can lose is a word you played in the split second before the app died — the tiles were still
  falling, so that word is replayed rather than half-saved.
- **NEW RUN throws it away.** That's what the button means, which is why it no longer says PLAY.
  A run you lose is thrown away too — there's nothing to continue.

🚧 **TEMPORARY:** the save is also thrown away whenever the game's numbers are re-tuned — change
the bag size, the move count or a round target while a run is saved and CONTINUE simply stops
appearing. That's a development rule, not a game rule: it exists so a run in progress can't
quietly keep playing by yesterday's numbers, and it will go away once the numbers stop moving.

❓ There is one save slot and no way to name, browse, or export a run.

---

## 7. Money

Earned per cleared round, kept for the whole run, spent in the shop.

**A cleared round pays:**

```
        $1  per 10 points scored        (pointsPerCoin = 10)
   +    $1  per unused move             (coinsPerUnusedMove = 1)
   ×    the round's rate                (×2 on a librarian round)
   +    a payout bonus                  (Late Fees and Other Stories: +10%)
   +    interest on what you're holding (Great Expectations: $1 per $10, max $25)
```

> **Worked example.** You clear round 1 with 33 points, on your 4th word of 20.
> `33 ÷ 10 = $3`, plus `16 unused moves = $16`. **Payout: $19.**

The last two lines only exist if you've bought them (§9). **The bonus is a percentage of what
the round earned**, so it rides the librarian's multiplier — a doubled round pays a doubled
bonus. **Interest is not part of what the round earned**, so it lands afterwards and is never
multiplied.

**A round pays at most $200** *(`maxRoundPayout`)*, whatever it scored. The cap is applied
**last** — after a librarian round's doubling, after the bonus, and after interest — so a
librarian round that already earned $200 on its own is paid $200, not $400, and both money
checkouts quietly stop working at the top end. 🚧 The number is a ceiling put there to stop a runaway
round buying out the shop in one visit, not a tuned part of the economy; it should end up well
above anything a fair round can reach, and today it isn't far above one.

What that means in play:

- Because the round ends the moment you hit the target, the *points* half is roughly
  **target ÷ 10 every time**. The **unused-moves half is the part that varies** — and right
  now it's usually the bigger half.
- 🎯 So the economy pays for **efficiency, not for grinding.** Clearing in 6 words pays nearly
  double clearing in 15. Overshooting the target earns you nothing at all.
- 🚧 With the targets currently halved, the points half of the payout is small enough that
  money is *almost entirely* an unused-moves reward. That's a side effect of the test values,
  not the intent — expect to retune `pointsPerCoin` when the targets go back up.
- Money shows in the round HUD, but it can't change mid-round.
- Failing pays nothing, and the run's money dies with the run.

One shop visit is roughly **one bookmark or two tile upgrades** — or, for about two rounds'
worth, **one checkout**. That's the interesting part: better letters, permanent scoring
abilities and permanent run-wide perks all compete for exactly the same money, on a shelf where
everything is one purchase only.

**Interest exists now**, as a checkout rather than a rule — so saving is a *build*, not
something the game does for you. ❓ Still no per-round purse and no sink other than the shop.

🚧 **There is a test mode where money doesn't bind at all** — see §10. Nothing in the shop is
ever too expensive and the readout says `$∞`. Rounds still pay normally underneath, so it's also
the quickest way to watch what the payout formula actually hands out.

---

## 8. The shop

Between rounds. It shows the round you cleared, what it paid, the next target, and six things
for sale. **CONTINUE** starts the next round.

**The shelf is six slots, stocked when the shop opens and after that only by a reroll you
pay for:**

| Slot | What it sells |
|:--:|---|
| 1, 2 | **A tile upgrade** — a random badge (2L / 3L / 2W / 3W) for a random tile in your bag. A 2L or 3L never lands on a tile worth 0 |
| 3, 4 | **A bookmark** you don't own. The two are always different |
| 5 | **A checkout** you don't own (§9) |
| 6 | **A new tile** for your bag — a letter pair, a choice tile, or a wild (§4) |

```
   OWNED   DEJA VU · SENSE AND FRUGALITY      BAG 104

   ┌────────────────────────────────┐
   │  2L → E                   $4   │
   ├────────────────────────────────┤
   │  3W → A             $22   $18  │   ← struck-through: your discount
   ├────────────────────────────────┤
   ╎  SPINE                  SOLD   ╎   ← greyed, stays where it is
   ├────────────────────────────────┤
   │  MARGINALIA               $11  │
   ├────────────────────────────────┤
   │  ONE MORE CHAPTER         $28  │
   ├────────────────────────────────┤
   │  NEW TILE   CH            $17  │
   └────────────────────────────────┘

       [ CONTINUE ]   [ REROLL $5 ]
```

**Tap a row to read what it does.** The shelf is replaced by a description — what it is, what
it does, what it costs — with **BUY** and **BACK**. Nothing is bought until you press BUY.

🎯 That's the point of the two steps: **a row you can't buy is still worth tapping.** The
price is dimmed and BUY reads NOT ENOUGH, but you can read the thing and decide whether to save
for it. The old shop bought on the first tap, so anything you couldn't afford was also
something you could never find out about.

The button says which problem you have, because they have different answers: **NOT ENOUGH**
means come back with more money, **BOOKMARKS FULL** means you're carrying five already
(§5), and **SOLD** means you took it this visit.

The rules of the shelf:

- **Prices are set, and everything is one purchase.** Buy a row and it goes grey, reads
  **SOLD**, and stays exactly where it is. Nothing costs more the second time, because there
  is no second time.
- **A row stays put when it sells.** The shelf never re-orders itself mid-visit — your thumb
  is already moving when the screen updates.
- **The two upgrade rows always name different tiles.** They can offer the same badge, but
  never for the same tile: otherwise buying one could fill that tile up and take the *other*
  row off the shelf without you touching it.
- **A slot with nothing in it isn't drawn.** Own every bookmark and both bookmark rows are
  gone; fill every tile in your bag and the upgrade rows go. The shop carries on with what's
  left rather than back-filling, so the slots keep their meaning.
- **The tile row is the one that never runs out.** Everything else on the shelf is something
  you can only own once; a tile is something you can own six of, so the same pair — or another
  wild — can be offered again next visit. Every tile the shop sells sits in that row at the same
  odds: eight pairs, ten choice tiles and one wild, so any particular one is **one in
  nineteen**. Buying one shows up as the **BAG** count going up — which is the only visible
  sign, since the tile then waits for a round to deal it.
- **A new shelf every visit.** Stock doesn't carry over, and nothing you declined comes back
  except by chance.
- **You can pay to reroll the whole shelf.** See below.
- **Leaving and coming back finds the same shelf**, minus what you bought — the visit is saved
  the moment you arrive and again after every purchase.

🚧 **An upgrade still lands on a random tile from your bag.** The tile is rolled when the shop
opens and shown on the button, so you can see what you're buying — but you never *choose* it.
The description names what that tile already carries, which is the only way to tell a fresh E
from one you've gilded twice. Choosing needs a bag picker, and that's its own piece of work.

🚧 **The prices are a first pass.** A cleared round 1 pays about $19, so the shelf above is
roughly "two cheap things, or one permanent one". Nothing here has been balanced against a
full run.

### Rerolling

**REROLL replaces all six rows for a price that climbs each time you use it.** The first one
of a visit costs **$5**, and every reroll after it costs **1.5× the last**, rounded up:

| Reroll | 1st | 2nd | 3rd | 4th | 5th | 6th |
|---|--:|--:|--:|--:|--:|--:|
| **Costs** | $5 | $8 | $12 | $17 | $26 | $38 |

**The price resets the moment you walk into the next shop.** It is a per-visit ladder, not a
run-long one — so rerolling hard in round 2 costs you nothing in round 3.

🎯 That climb is the whole design. One reroll is cheap enough to be an obvious yes when the
shelf is bad; the fourth costs more than most of the things on it. You're meant to run out of
willingness before you run out of money.

What a reroll does and doesn't do:

- **Everything is re-stocked, including rows you already bought.** A sold row comes back
  available — possibly showing the same thing, since it's a fresh random roll.
- **The usual rules still hold.** A bookmark or checkout you now own won't be offered, so
  buying one and then rerolling permanently removes it from the pool.
- **Your money is spent either way.** Rerolling doesn't refund the row you bought.
- **It's one tap, with no confirmation.** Everything else in the shop makes you read a
  description and press BUY; a reroll has nothing to read, and the price is on the button.
- **Late in a run a reroll buys less.** Once you own every bookmark and checkout, those three
  rows are empty and you're paying full price to re-roll two upgrades and a tile. Nothing
  warns you.

**A Tale of Two Shelves** (§9) makes the first reroll of every shop **$2 cheaper**, and because
the climb compounds off that lower number, the whole ladder comes down with it: **$3 · $5 · $7
· $11 · $16**. It is the only thing that makes a reroll cheaper — Sense and Frugality's
percentage is for what's *on* the shelf and deliberately doesn't touch a reroll, so the two
can't stack into a free one.

🚧 **The price is a first guess.** $5 climbing at 1.5× against a round that pays about $19
allows two or three rerolls a visit. Both numbers are tuning knobs.

🚧 **There's still no skip**, and no way to pay to keep a row you liked through a reroll.

❓ Still open: whether you choose which tile gets upgraded, and whether you can sell or
*remove* tiles from your bag — adding to it is now answered, taking away isn't.

---

## 9. Checkouts

**A checkout is something you check out of the library and keep.** You buy one in the shop, it
takes effect immediately, and it lasts for the rest of the run. Balatro's vouchers, named like
books.

Where a bookmark changes what a *word* scores, a checkout changes the *run* — how many moves
you get, how much the shop charges, what a cleared round pays. They never touch a word's score.

| Checkout | What it does | Price |
|---|---|--:|
| **Sense and Frugality** | Everything **on the shelf** costs **20% less**, rounded up. Rerolls pay full price | $30 |
| **Second Thoughts** | Discard **2 more tiles** every round | $25 |
| **Late Fees and Other Stories** | Cleared rounds pay **10% more** | $20 |
| **One More Chapter** | **One more move** every round | $35 |
| **Great Expectations** | Earn **$1 per $10** you're holding when a round is cleared | $40 |
| **A Tale of Two Shelves** | Rerolling the shelf starts **$2 cheaper**, every shop (§8) | $20 |

The rules around them:

- **One of each, at most**, and the shop never offers one you already own. There's no limit on
  how many different ones you can hold.
- **They apply the moment you buy them** — including to the rest of the shelf you're standing
  in front of. Buy Sense and Frugality and the other five rows get cheaper before you've
  looked away; buy A Tale of Two Shelves and the REROLL button drops $2 on the spot.
- **They stack additively.** Two sources of a discount would add up, and the total is capped at
  90% so the shop can never be free.
- **Checkouts die with the run**, like money, bookmarks and tile upgrades.

Details worth knowing:

- ⚠️ **The Redactor beats Second Thoughts.** That librarian sets the round's discard allowance
  to a *limit*, so a no-discards round is a no-discards round no matter what you own. The boss
  beats the shop, which is what a boss is for. The same is true of anything else a librarian
  lowers.
- **Great Expectations charges interest on what you walked in with**, not on what the round
  just paid — so clearing a round never earns interest on its own winnings. It's capped at
  **$25 a round** *(`maxInterest`)*, which is reached at $250 held.
- **The $200 round cap is applied last**, after the librarian's multiplier, after Late Fees,
  and after interest. A round already paying the cap is paid the cap, so both money checkouts
  quietly stop working at the top end.
- 🎯 **Sense and Frugality is a bet on how long the run lasts.** It does nothing on its own and
  pays back over every later visit. Bought late it's worthless, which is the decision.
- ⚠️ **Sense and Frugality does NOT make rerolls cheaper.** Its percentage is for what's on
  the shelf. A Tale of Two Shelves is the one that touches a reroll, and it does it as a flat
  cut to the starting price — deliberately two different mechanisms, so a stack of both can
  never reach a free reroll.
- 🎯 **A Tale of Two Shelves compounds too, and harder than it looks.** The $2 comes off the
  price the 1.5× climb multiplies, so it's worth $2 on your first reroll of a visit and $10 on
  your fifth. It's also the only checkout that can be worth *nothing*: a player who never
  rerolls has bought a blank, and the shop has no way to tell them that. Priced at the bottom
  of the ladder for exactly that reason.
- 🎯 **Great Expectations is the only reason not to spend.** Everything else in the shop
  rewards emptying your wallet; this is the one thing that makes sitting on it a plan.
- 🚧 **Late Fees is the weakest of the six right now.** 10% of a $19 round is $1. It scales
  with the targets and it's the cheapest, but it wants either a higher rate or a lower price
  once a full run has been played.

❓ **Six is not a tree.** Balatro's vouchers come in tiers where buying one unlocks a stronger
version. Nothing here unlocks anything, and whether that's the shape this wants is undecided.

---


## 10. Modes

| Mode | What it is |
|---|---|
| **Rogue Demo** | Everything described in this document. The only mode you'd actually play. |
| 🚧 **Unlimited Money** | A TEST MODE. Identical to Rogue Demo except nothing is ever too expensive — buy every row, reroll as often as you like, stack checkouts in one visit. Both money readouts say `$∞`. |

*Timed and Overflow modes were cut on 2026-08-25, and Moves — the last arcade round — on
2026-08-28. This is a roguelike now, not an arcade collection.*

🚧 **About Unlimited Money.** It exists because the shop is the newest and least-played part
of the game, and seeing all of it the honest way means grinding rounds for money — clearing round
1 pays about $19, which buys one bookmark. The mode is a straight **copy** of Rogue Demo's asset
with one flag flipped, re-made by an editor menu item, so its targets and tile bag can't quietly
drift away from the real game's. It is **not a design idea** and nothing about the economy should
be judged from it. It also shares the one save slot, so starting a test run throws away a real
one. It comes out before release.

---

## 11. Every number, in one place

| | Value | Lives in |
|---|--:|---|
| Board | 5 × 5 | `Board_5x5.asset` |
| Minimum word length | 3 | `Mode_RogueDemo.asset` |
| Length multiplier | ×1 / ×1.5 / ×2, then +0.5 a letter (LETTERS, not tiles) | `Mode_RogueDemo.asset` |
| Score multiplier | ×1 | `Mode_RogueDemo.asset` |
| Words per round | 20 | `Mode_RogueDemo.asset` |
| Discards per round | 5 tiles | `Mode_RogueDemo.asset` |
| Invalid word costs a move | n/a — can't be submitted | `Mode_RogueDemo.asset` |
| Move counter turns red at | 3 left | `Mode_RogueDemo.asset` |
| Round targets | 30 / 45 / 65, then ×1.5 | `Mode_RogueDemo.asset` |
| Librarian every | 3 rounds | `Mode_RogueDemo.asset` |
| Librarian round pays | ×2 | `Mode_RogueDemo.asset` |
| The Grandiloquent's minimum | 5 letters | `Librarian_Grandiloquent.asset` |
| The Redactor's discard limit | 0 tiles | `Librarian_Redactor.asset` |
| The Insatiable's target factor | ×3 | `Librarian_Insatiable.asset` |
| The Critic's cut | 25% off Points and Mult, floor 1 | `Librarian_Critic.asset` |
| The Dilapidated's closed spaces | 3 | `Librarian_Dilapidated.asset` |
| Neighbours every space keeps | 2 | `Librarian_Dilapidated.asset` |
| Tile bag size | 104 tiles (~one Scrabble set) | `Mode_RogueDemo.asset` |
| Modifiers per tile | 3 (0 = no limit) | `Mode_RogueDemo.asset` |
| Letter values & mix | Scrabble proportions, floor of 1 each | `LetterSet_Scrabble.asset` |
| Points per $1 | 10 | `Mode_RogueDemo.asset` |
| $ per unused move | 1 | `Mode_RogueDemo.asset` |
| Max payout per round | $200 (0 = no cap) | `Mode_RogueDemo.asset` |
| Max interest per round | $25 (0 = no cap) | `Mode_RogueDemo.asset` |
| 🚧 Unlimited money | off | `Mode_RogueDemo.asset` — on in `Mode_RogueDemo_Unlimited.asset` |
| Shop slots | 2 upgrades · 2 bookmarks · 1 checkout · 1 new tile | `ShopScreen` (code, not an asset) |
| Modifier prices | 5 / 9 / 14 / 22 | each asset in `GameData/Modifiers/` |
| Bookmark prices | 10 / 12 / 13 / 13 / 14 / 16 | each asset in `GameData/Bookmarks/` |
| Bookmarks you may hold | 5 (0 = no limit) | `Mode_RogueDemo.asset` |
| Checkout prices | 20 / 20 / 25 / 30 / 35 / 40 | each asset in `GameData/Checkouts/` |
| Multi-letter tiles | ER IN IE ED TH SH CH QU | `LetterSet_Scrabble.asset` |
| Multi-letter tile worth | the two letters, ×1.5 rounded up | derived — `LetterSetSetup` |
| Multi-letter tile prices | 8 / 8 / 8 / 10 / 14 / 14 / 17 / 22 | `LetterSet_Scrabble.asset` |
| Wild tile | `*`, worth 0, $35 | `LetterSet_Scrabble.asset` |
| Choice tiles | R/S/T L/N/R B/C/P A/E/I F/H/W K/V/Y D/G J/X Q/Z O/U | `LetterSet_Scrabble.asset` |
| Choice tile worth | its cheapest option, ×0.75 rounded down | derived — `LetterSetSetup` |
| Choice tile prices | 24 / 22 / 20 / 18 / 18 / 15 / 12 / 12 / 12 / 10 | `LetterSet_Scrabble.asset` |
| Letter multiplier on a 0-point tile | never offered | `ShopScreen.RollTarget` (code, not an asset) |
| Shop discount | 20% off the shelf, rounded up (cap 90%) | `Checkout_ShopDiscount.asset` |
| Extra discards | +2 tiles a round | `Checkout_ExtraDiscards.asset` |
| Extra moves | +1 a round | `Checkout_ExtraMoves.asset` |
| Payout bonus | +10% | `Checkout_PayoutBonus.asset` |
| Interest rate | $1 per $10 held | `Checkout_Interest.asset` |
| Reroll, first of a visit | $5 (0 = no reroll button) | `Mode_RogueDemo.asset` |
| Reroll price climb | ×1.5 each time, rounded up, resets per visit | `Mode_RogueDemo.asset` |
| Reroll discount | $2 off the first one, floor $1 | `Checkout_RerollDiscount.asset` |
| Seed length | 8 characters | `Rng` (code, not an asset) |
| Deja Vu bonus | +10 Points | `DejaVu.asset` |
| Vowel Fanatic bonus | +4 Mult | `VowelFanatic.asset` |
| Bookend multiplier | ×2 Mult | `Bookend.asset` |
| Spine multiplier | ×2 Mult | `Spine.asset` |
| Marginalia bonus | +1 Mult a letter | `Marginalia.asset` |
| Shorthand bonus | +4 Mult | `Shorthand.asset` |
| Score walk-through pace | 0.45s a step, 0.35s to finish | `ScoreTallyTiming` (code, not an asset) |
| Score ceiling | 1,000,000,000 points, ×1,000,000 mult | `ScoreLimits` (code, not an asset) |

---

## 12. Built · planned · open

### ✅ Built and playable

The board, tap-or-drag selection and the ENTER / DISCARD buttons · scoring with stacking
multipliers · the run (rounds, escalating
targets, a persistent finite tile bag) · money · **multi-letter tiles** — eight of them, bought
outright, spelling two letters from one square (§4) · **wild tiles**, which become whichever
letter scores best and route around a banned one (§4) · **choice tiles** — ten of them, each
becoming one of two or three named letters (§4) · bookmarks (six of them, with a scoring
pipeline built to take many more) · a shop of six slots with set prices, one purchase each, and a
description you read before you buy (§8) · **paying to reroll the shelf**, at a price that
climbs within a visit and resets between them (§8) · **checkouts** — six permanent run-wide
perks, including interest on savings and a cheaper reroll (§9) · runs that save and resume themselves (§6) ·
**librarians** — rule-warping rounds every third round, eight of them, paying double (§6).

### 📋 Decided, not built

- **Tile bags with abilities**, boards of varying **size and shape**, and boards where **gravity
  flows differently**. All three have seams in the code; no content uses them.
- **Tile skins as a player-facing thing** — several looks can already share a board, but
  nothing decides which ones a player *has*.

### ❓ Open questions

- **Which letter a wild or a choice tile becomes is never explained, or yours to choose.** It
  takes the best-scoring option and falls back to alphabetical, which is most of the time (§4).
  Same missing piece as choosing which tile an upgrade lands on.
- **Taking tiles OUT of the bag.** The shop can add to it now (§4) but nothing removes, and a
  bag that only ever grows dilutes every good tile you buy. Selling tiles back, a smaller-bag
  upgrade, or both.
- **Gravity on a board with holes** — tiles currently fall *past* gaps instead of into them.
- **How a run ends** — librarians now give a run a rhythm, but there's still **no victory**, so
  every run ends in failure. Run length and whether winning stops the run are still open; the
  write-up is in `ROGUELIKE-IDEAS.md`.
- ⚠️ **A librarian round can lock.** If the bag is empty, no legal word is left on the board and
  the discards are gone (or The Redactor took them), nothing ends the round — moves only tick
  down when a word is played. The exits on the table are a forfeit button, counting a dead board
  as a loss, or detecting one. Undecided.
- **What a librarian is called** — "librarian" is a placeholder noun, held in one config field so
  it can become exams, critics or anything else without touching the game.
- **The shop promises a target it can't keep.** Its NEXT TARGET line reads the run's curve, but
  the next round's librarian isn't drawn until you press CONTINUE — so an Insatiable round shows
  up asking for ×3 what was advertised. Either the librarian gets drawn a round early (which
  would also let the shop announce *who* is next, Balatro-style), or the line stops claiming to
  know. Undecided.
- **Every librarian is a restriction.** All eight take something away; none gives anything back
  beyond the doubled payout. The Dilapidated is the first that *changes* something rather than
  forbidding it — but a smaller board is still a board with less on it. One that hands something
  over, or that plays by a different bag, is still missing.
- ❓ **How boxed-in should The Dilapidated be allowed to leave a space?** Every space is
  guaranteed **two** open neighbours *(`minNeighbours`)*, which is what stops one being walled
  off entirely — but two is still a near-dead corner of the board. Three would push the holes
  apart and make the round gentler.
- **Nothing scales a librarian to the round it lands on.** The Insatiable's ×3 is the same
  demand on round 3 as on round 30, and The Critic's 25% is flat. Whether a boss should get
  harder as the run goes on is undecided.
- ❓ **Reordering is free, unlimited, and allowed mid-round.** The cards can be dragged at any
  time, so in principle you can re-optimise before every single word — shuffle Shorthand to the
  front for a three-letter word, then Marginalia for a long one. That's a lot of fiddling for a
  little score, and nothing stops it. Locking the order once a round starts would make it a
  commitment instead; whether that's a better game or just a slower one is undecided.
- **You still don't choose which tile gets upgraded.** The shop rolls one and shows you what it
  already carries, which makes the offer readable but not a decision. A bag picker is the fix.
- **The shop has no skip, and no way to keep a row through a reroll.** Rerolling answers the
  badly-rolled visit (§8), but it's all-or-nothing: there's no paying to hold the one row you
  liked while the other five change. Whether that's a missing decision or one decision too
  many needs playing to find out.
- **Checkouts don't tier.** Balatro's vouchers unlock stronger versions of themselves; these
  six are flat. Whether a tree is the right shape here is undecided (§9).
- **Payouts reward speed, not scoring.** Unused moves pay far more than points do — clearing
  fast beats clearing big. Tied to the run-length question above.
- **The $200 payout cap is closer than it looks.** A librarian round cleared fast already pays
  around $70; two doublings of the current numbers would put ordinary rounds against the
  ceiling, at which point the cap stops being a safety rail and starts being balance. Worth
  re-checking whenever `pointsPerCoin` or `coinsPerUnusedMove` move — and note that both money
  checkouts are applied *under* the cap, so they're the first things it silently eats.
- **Entering a seed** — every run has one and shows it, but there's nowhere to type one in yet,
  so a run can be reported and reproduced by a developer but not replayed by a player.
- **A board that's playable-looking but dead** — full of tiles that spell nothing (see §5).
- **The HUD** — round, target, bag and money still share one shrunken line. Bookmarks used to
  share that corner too; they're cards below the board now, which is one readout's worth of
  pressure off it and no more.
- **Bookmark editions** — holographic / negative / foil equivalents are planned, undesigned.
- **Bookmark feedback** — the tally names each step as it walks, but the CARD that did it
  doesn't light up. Now that the cards are on screen while the tally runs, that's a gap with
  an obvious shape.
- **Save-scumming** — killing the app mid-round rewinds one word. Balatro has the same hole; it
  hasn't been decided whether it's worth closing.
