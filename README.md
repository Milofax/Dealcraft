# Dealcraft

A Schedule I mod that negotiates your customer deals for you, schedules them
into the hours you actually play, fills in your handovers, and writes down every
number it used to decide, in a file you can read afterwards.

Built for **Schedule I 0.4.6f13** (Steam main branch, IL2CPP) with
**MelonLoader 0.7.3**. Two files, no custom assets, nothing written into your
save.

---

## Who has to install it

Short answer: **one person, and it does not have to be you.**

| | |
| --- | --- |
| **The host installs it** | Enough on its own. Everyone at the table gets the negotiated prices and the scheduled deals, whether or not they installed anything. |
| **A player also installs it** | That player additionally gets their *own* handovers filled in — their goods, their pockets, their money. |
| **A player installs nothing** | Joins and plays exactly as before. Nothing is missing, nothing breaks. |

Nobody is required to install anything. Unmodified vanilla clients can always
join a host running Dealcraft, and that is not going to change.

Why it is split that way: negotiating, scheduling and prices act on **one**
shared customer roster and **one** shared contract list, so they run on the host
only — six installs must not answer one offer six times. A handover is one
player's goods leaving one player's pockets, so it belongs to whoever is
standing there, and that player's own copy does it.

## What it does not do

- It does **not** walk to customers. You still go there.
- It does **not** carry goods for you. What is not in your pockets cannot be
  handed over.
- It does **not** touch your save. Remove the two files and the save loads in a
  vanilla game, unchanged.
- It does **not** show you numbers in its app. No customer list, no product
  list, no counters — the game already has the first two, and the third is a
  file rather than a screen. See
  [What its app shows](#what-its-app-shows-and-what-it-does-not).

---

# Installing

Two paths below. Both end in the same two files in the same two folders. Pick
one.

- **[With a coding agent](#with-a-coding-agent)** — paste one block and answer
  its questions. This is the path to take if you have one; it checks the
  prerequisites, puts each file in the right folder and reads the log back to
  you.
- **[By hand](#by-hand)** — download, copy, done. Five minutes.

## Before either path: MelonLoader, then launch the game once

This is the step that makes everything else fail confusingly if you skip it.

1. Install **MelonLoader 0.7.3** into Schedule I —
   <https://melonwiki.xyz/#/?id=automated-installation>.
2. **Start the game once and let it reach the main menu, then quit.**
   MelonLoader creates `Mods`, `UserLibs`, `UserData` and `MelonLoader/` on that
   first run. Until it has, there is nowhere to put the mod, and a folder you
   made yourself with the same name is not the same thing.

You will know it worked: the game folder now contains a `MelonLoader` folder
with a `Latest.log` in it.

## Where the game folder is

It is the folder that holds `Schedule I.exe` next to `Schedule I_Data`. After
MelonLoader has run once it looks like this — this listing is real, taken from a
working install:

```
Schedule I/
├── Schedule I.exe
├── Schedule I_Data/
├── GameAssembly.dll
├── MelonLoader/          <- Latest.log lives in here
├── Mods/                 <- Dealcraft.dll goes here
├── UserLibs/             <- Dealcraft.Core.dll goes here
├── UserData/
└── Plugins/
```

**Windows**, default Steam library:

```
C:\Program Files (x86)\Steam\steamapps\common\Schedule I
```

If you moved the game to another drive, Steam put it in a second library. The
libraries are listed in
`C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf`; each `"path"` in
that file has a `steamapps\common\Schedule I` under it if the game is there. Or
in Steam: right-click Schedule I → *Manage* → *Browse local files*.

**Linux**, played through Proton:

```
~/.steam/steam/steamapps/common/Schedule I
~/.local/share/Steam/steamapps/common/Schedule I
```

Both are usually the same folder — `~/.steam/steam` is a symlink. Flatpak Steam
puts it under `~/.var/app/com.valvesoftware.Steam/.local/share/Steam/...`
instead. Extra libraries are listed in `steamapps/libraryfolders.vdf`, same as on
Windows.

Note what is **not** here: the game folder is a Windows-shaped folder even on
Linux, because the game runs under Proton. The mod goes into that folder, not
into the Proton prefix. The prefix under `steamapps/compatdata/3164500/` holds
your saves; you never have to open it to install this.

## By hand

1. Download the two files from the latest release:
   **<https://github.com/Milofax/Dealcraft/releases/latest>**
   - `Dealcraft.dll`
   - `Dealcraft.Core.dll`

2. **Two files, two different folders.** This is the one thing that goes wrong:

   | file | goes into |
   | --- | --- |
   | `Dealcraft.dll` | `<game folder>\Mods\` |
   | `Dealcraft.Core.dll` | `<game folder>\UserLibs\` |

   Putting both into `Mods` is the obvious guess and it does not work — the mod
   does not load. `Dealcraft.dll` is the mod; `Dealcraft.Core.dll` is the
   library it needs, and MelonLoader loads libraries from `UserLibs`, which is
   what it is for.

3. **Upgrading from an older version?** Open
   `<game folder>\UserData\MelonPreferences.cfg`, copy it somewhere safe, and
   delete the block that starts with `[Dealcraft]` — down to, but not including,
   the next line starting with `[`. Leave the other mods' sections alone.
   MelonLoader keeps your settings in that file and reads them back over the
   defaults, so without this you carry your old switches into the new version,
   along with keys that no longer exist. A first install has nothing to do here.

4. Start the game and let it reach the main menu.

5. **Check it worked.** Open `<game folder>\MelonLoader\Latest.log` in any text
   editor and look for this line:

   ```
   [Dealcraft] Dealcraft loaded. Automation: counter-offer off, handover off.
   ```

   The words after `Dealcraft loaded.` change with your settings. **`Dealcraft
   loaded.` is the proof.** If it is not in the log, the mod is not running, and
   nothing else in this README applies yet — see
   [When it did not work](#when-it-did-not-work).

6. Open the phone in-game. Dealcraft is an app on the home screen.
   **Every automation switch ships off.** Nothing acts on its own until you turn
   it on.

## With a coding agent

Paste everything in the block below into your coding agent, verbatim. It is
written as instructions to the agent; it will ask you to start the game at the
right moment and tell you what the log said.

````
Install the Schedule I mod "Dealcraft" on this machine for me.

Rules. Do not break these, and do not work around them:
- Never delete, move, rename or overwrite anything except the two mod files
  named in step 4 and the one settings section named in step 5.
- Write nothing anywhere except the game's `Mods` and `UserLibs` folders, the
  one file named in step 5, and a temporary download folder.
- Create no folders. Every folder you need already exists if the prerequisites
  are met; if one is missing, that is a finding to report, not a thing to fix.
- Do not install, repair or configure MelonLoader, Steam, Proton or the game.
- Do not start or stop the game yourself. Ask me to do it.
- If any step below cannot be completed exactly as written, STOP, tell me what
  you checked and what you found, and wait. Do not improvise, do not guess a
  path, do not try a different install method.

1. Find the game folder. It is the folder containing `Schedule I.exe` next to a
   `Schedule I_Data` folder. Look here first:
   - Windows: `C:\Program Files (x86)\Steam\steamapps\common\Schedule I`.
     If it is not there, read
     `C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf` and try
     `<path>\steamapps\common\Schedule I` for each `"path"` in it.
   - Linux: `~/.steam/steam/steamapps/common/Schedule I`, then
     `~/.local/share/Steam/steamapps/common/Schedule I`, then
     `~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Schedule I`.
     If none exist, read `steamapps/libraryfolders.vdf` under each of those
     Steam roots and try `<path>/steamapps/common/Schedule I` for each `"path"`.
   Tell me the path you found, then continue. If you find none, STOP and ask me
   where the game is — do not search the whole disk.

2. Check the prerequisites in that folder. All four must be true:
   - a `MelonLoader` folder exists
   - a `Mods` folder exists
   - a `UserLibs` folder exists
   - `MelonLoader/Latest.log` exists and contains the text `MelonLoader v0.7.3`
   If any is false, STOP and tell me exactly this, plus which check failed:
   "Install MelonLoader 0.7.3 from https://melonwiki.xyz/#/?id=automated-installation,
   then start Schedule I once and quit it, then ask me to run this again."
   A different MelonLoader version in the log is also a stop: report the version
   you found and let me decide.

3. Download these two files from the latest release at
   https://github.com/Milofax/Dealcraft/releases/latest into a temporary folder:
   - `Dealcraft.dll`
   - `Dealcraft.Core.dll`
   Do not build anything from source. Do not fetch them from anywhere else.

4. Copy them into TWO DIFFERENT folders. This is the step people get wrong:
       Dealcraft.dll       ->  <game folder>/Mods/
       Dealcraft.Core.dll  ->  <game folder>/UserLibs/
   Both into `Mods` does not work. If a file of either name is already there,
   tell me its size and modification date and ask me before replacing it. Touch
   no other file in either folder.
   If `Dealcraft.Core.dll` is also sitting in `Mods` from an older install,
   delete that one copy — it is a library, MelonLoader tries to load anything in
   `Mods` as a mod, and a stale copy means two versions of it on disk.

5. Put the settings back to their defaults, which are all off. Skip this whole
   step if `<game folder>/UserData/MelonPreferences.cfg` does not exist, or has
   no `[Dealcraft]` section: then the defaults already are the defaults.
   Otherwise:
   - Copy that file to `MelonPreferences.cfg.bak` next to it first. If a `.bak`
     is already there, ask me before overwriting it.
   - In the original, delete the lines from `[Dealcraft]` up to but NOT
     including the next line that starts with `[`, or to the end of the file if
     there is none. Leave every other section byte for byte as it was — other
     mods keep their settings there and some of mine are customised.
   - Tell me how many lines you removed.
   Why this is part of installing: MelonLoader writes every setting into that
   file the first time it sees it and reads the file from then on, so a default
   in the code only ever reaches a machine that has never run the mod. Dropping
   the section makes MelonLoader write it again from the code, with every
   automation switch off, which is where a session should start.

6. Ask me to start Schedule I and to tell you when the main menu is up. Wait for
   my answer. Do not launch it yourself.

7. When I say the menu is up, read `<game folder>/MelonLoader/Latest.log` and
   look for a line containing:
       Dealcraft loaded.
   - If it is there: show me that line and tell me the install worked, and that
     every automation switch ships off and I turn them on in the Dealcraft app
     on the in-game phone.
   - If it is not there: show me the last 40 lines of the log, plus every line
     containing `Dealcraft`, `ERROR` or `Assembly`. Then STOP. Do not attempt a
     fix, do not move files around, do not reinstall anything.
````

## When it did not work

`MelonLoader/Latest.log` answers all of these. Open it and search.

| what the log shows | what to check first |
| --- | --- |
| no `MelonLoader v0.7.3` banner, or no log at all | MelonLoader is not installed, or the game has not been launched since it was. Start over at the top. |
| `Melon Assembly loaded: '.\Mods\Dealcraft.dll'` but no `Dealcraft loaded.` | `Dealcraft.Core.dll` — it is probably missing from `UserLibs`, or sitting in `Mods` instead. A working install logs `Melon Assembly loaded: '.\UserLibs\Dealcraft.Core.dll'` as well. |
| neither DLL mentioned anywhere | The files are in the wrong folder, or in a `Mods` folder you created next to the real one rather than the real one. Check you are in the folder with `Schedule I.exe`. |
| `Game Version:` says something other than `0.4.6f13` | This build targets 0.4.6f13 and has not been tried against anything else. |

The log rewrites itself on every launch; older ones are kept in
`MelonLoader/Logs/`.

## Removing it

Delete `Mods\Dealcraft.dll` and `UserLibs\Dealcraft.Core.dll`. That is all of
it. Dealcraft writes nothing into your save, so the save keeps loading in a
vanilla game. Your settings stay behind in `UserData\MelonPreferences.cfg` under
`[Dealcraft]` and do nothing; delete that section too if you want it gone.

---

# What a handover is actually worth

This is the part no wiki has, and it is the reason the mod exists.

The game pays five named bonuses on top of a contract's payment. **These figures
were measured, not guessed**: an evening of real play with every handover
recorded, twelve of them, and every bonus-bearing row reproduces the table
exactly. Two careful readings of the game's own machine code had one of these
rows *wrong*, in two different ways, before play settled it.

| bonus | what it pays | ceiling |
| --- | --- | --- |
| **Exceeded Quality** | **15% of the contract's payment per grade tier** above what it asked for | none — three tiers is 45% |
| **Quick Delivery** | 10% of the payment | — |
| **Generosity** | **$10 per extra unit** delivered | `(package size − 1) × $10` per contract line |
| **Curfew** | 20% of the payment | — |
| **Rain** | 20% of the payment | — |
| **A street sale** | **nothing at all** | — |

What that is worth knowing for:

- **Grade pays, quantity barely does.** On a $495 contract, one grade tier above
  what was asked is $74. One extra unit is $10, flat, whatever the product is.
  Put your good product on your big contracts.
- **Over-delivering on purpose is usually a loss.** An extra unit earns $10 and
  costs you what that unit sells for. Above $10 a unit you are paying to be
  generous. And the extra units have to be the *same* product at the *same*
  grade or better with every requested property — padding an order with cheap
  stock earns nothing and can cost you the bonus outright.
- **The Generosity bonus cannot be farmed.** The game counts at most
  `ceil(needed ÷ package size)` packages per stack. Baggies (1 unit) can never
  earn it; a jar (5) tops out at $40, a brick (20) at $190, and only on an order
  for a single unit. Dealcraft picks the smallest package that covers the order,
  which keeps forced overshoot — and its cost — as small as it can be.
- **Street sales pay no bonus** because there is no contract for the bonus to be
  a percentage of. Heavenly product sold on the street is worth exactly the
  same as it would be at Poor grade, as far as bonuses go.

Dealcraft hands over the cheapest grade that satisfies the contract and tells
you what the better grades would have paid, rather than spending your Heavenly
stock for you.

---

# What it does

- Finds the highest price a customer will still accept at the confidence you
  ask for — exactly, from the game's own success chance, not from a model of it
- Works out, per product, the price that takes the most money across all your
  customers and — if you ask it to — writes it into the game through the game's
  own price call
- Sends counter-offers, schedules deals into the hours you chose, and completes
  handovers, all through the game's own server calls
- Keeps a ledger of every handover it sees, including ones you did by hand, so
  the numbers above can be checked rather than believed
- Writes every decision it makes, and every time it decided to do nothing, to
  `UserData/Dealcraft/decisions.jsonl`. On by default, with no switch. Read it
  with `bin/read-ledger`

Every automation switch is **off by default**. Turn them on once you have
looked.

## What its app shows, and what it does not

One page of switches on the in-game phone: listed price, price negotiation, the
four hours-of-the-day boxes, and handover. Nothing else — **no numbers, no
lists, no status words.** The owner asked for the mod to be a set of decisions
he makes once, not a screen he has to read.

So in particular, and worth knowing before you install it:

- **There is no customer list and no product list in the app.** The game has
  both, and the mod's copies were deleted.
- **The listed price it works out is not shown anywhere in the game.** With
  `LISTED PRICE` on it writes the price and the game shows the result; the
  figure the mod arrived at, and what it thought of the old one, are in
  `decisions.jsonl` and nowhere else. If you have priced your book by hand and
  want to know what the mod would do differently, that file is the only place to
  look.

## Multiplayer, in more detail

**Players who do not install it can still join and play normally.** That is the
baseline and it does not move: with Dealcraft on the host alone, unmodified
clients see ordinary customers, ordinary messages and ordinary deals, and get
the negotiated prices.

- **Negotiating, scheduling and listed prices run on the host only**, once per
  contract, through the vanilla server calls.
- **Handovers run for whoever installed it.** A guest with Dealcraft gets their
  own deliveries filled in and is paid for them, by their own machine. A guest
  without it hands over by hand, exactly as today.
- **And a handover waits until you talk to the customer.** That is the default,
  and in a session it is what decides who serves a contract: the contract list
  is shared and a handover takes from your own pockets, so whoever walks over
  and presses `E` is the one who makes it. Talking to them is the only thing
  that settles it, and no message passes between the machines to settle it.
- **`Automated handover from anywhere` is the other answer, and in a session it
  is not a preference.** The host's copy sees the whole shared list, so with
  that answer on it completes every contract on it out of the host's own
  pockets, from across the map — **whatever the other players chose**. A guest
  on `Manual` who walks over to do it by hand finds the deal already gone. That
  is known, it is the owner's decision to keep the answer available with the
  warning on it rather than hide it, and the app says so on the line under the
  option. **Untested in multiplayer** in the other direction: whether a guest on
  it really spends and is paid from their own machine has been read out of the
  binary and never run. Leave it off in a session until someone has played it.

Dealcraft adds no networked objects, no message types and no assets of its own,
so there is nothing for a vanilla client to be missing.

## What has been tried, and what has not

Honest list. Nothing here is inferred.

- Loads into Schedule I 0.4.6f13 under MelonLoader 0.7.3 with no exception,
  **on Linux under Proton**.
- **Windows is untested.** The mod has no native code and nothing
  platform-specific in it, so there is every reason to expect it to work — but
  no one has yet run it on Windows, and this README will not claim otherwise
  until someone has. If you try it, say how it went.
- The handover bonus figures above were measured across twelve recorded
  handovers in one evening of play.
- The decision logic carries no game type and is covered by its own unit tests,
  which run without the game installed.
- **Not yet exercised: the automation itself in a live multiplayer session.**
  Every automation switch ships off for that reason.
- **Not yet exercised: `Automated handover from anywhere` with more than one
  player.** See the multiplayer section above for what is read and what is not.
- `Automated handover when I talk to them` hangs on the game's own method for
  opening a dialogue, so the mod holds no distance of its own at all. It used
  to hold one, and that was the only number in the feature the game had not
  supplied. **Not yet seen on a screen:** the handover finishes
  before the game builds the choice list, so `Complete Deal` is never offered
  and there is nothing left to click. Whether that is what a player sees has not
  been watched.

---

# Building it yourself

You do not need this to play. Skip to the release download above.

You need the .NET SDK and a Schedule I install that has been run at least once
with MelonLoader, so that `MelonLoader/Il2CppAssemblies` exists. Dealcraft
references those assemblies; it never vendors or redistributes them.

```
dotnet build Dealcraft.Mod -c Release
```

The build looks for the game in this order and takes the first hit that actually
contains `MelonLoader/Il2CppAssemblies`:

1. `-p:ScheduleOneDir="..."` on the command line
2. a `ScheduleOneDir` environment variable
3. the conventional Steam locations — `%ProgramFiles%\Steam`,
   `C:\Program Files (x86)\Steam`, `C:\SteamLibrary`, `D:\SteamLibrary`,
   `D:\Steam`, `~/.local/share/Steam`, `~/.steam/steam`, and the Flatpak Steam
   directory — each under `steamapps/common/Schedule I`

If your library is somewhere else, name it:

```
dotnet build Dealcraft.Mod -c Release \
  -p:ScheduleOneDir="/mnt/games/SteamLibrary/steamapps/common/Schedule I"
```

```
dotnet build Dealcraft.Mod -c Release ^
  -p:ScheduleOneDir="E:\SteamLibrary\steamapps\common\Schedule I"
```

Forward and backward slashes both work. If nothing resolves, the build stops and
tells you what to pass rather than failing later on a missing reference.

The two outputs are the two files you install:

```
Dealcraft.Mod/bin/Release/net6.0/Dealcraft.dll            -> <game>/Mods/
Dealcraft.Core/bin/Release/netstandard2.1/Dealcraft.Core.dll -> <game>/UserLibs/
```

The decision core builds and its tests run without the game installed:

```
dotnet test Dealcraft.Core.Tests -c Release
```

---

# Licence

Dealcraft is GPL-3. See [`LICENSE`](LICENSE).
