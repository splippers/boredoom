# BoreDOOM! Roadmap

> **Boring jobs. Glorious gameplay.**

BoreDOOM! is a domestic chore game.

Its purpose is to explore a simple idea:

> **Can boring real-world work become genuinely enjoyable gameplay?**

The game begins with hoovering and other ordinary household jobs.

Long term, BoreDOOM! should be able to use scanned real environments as
persistent game worlds, support radically different player scales, generate
chore-based missions, enable cooperative multiplayer, and produce useful
session reports describing what the players attempted and what was actually
accomplished.

BoreDOOM! is not Virtual Network Admin.

VNA may eventually reuse BoreDOOM!'s ideas around spatial interaction,
missions, progression, multiplayer, scale, and reporting, but IT training and
the wider playable digital-twin infrastructure belong primarily to VNA.

BoreDOOM!'s centre of gravity remains wonderfully mundane:

> **There is always something that needs hoovered.**

---

# Design Principles

## The chore is the game

Do not reduce chores to:

```text
Hoover room
Press Complete
+50 XP
```

The objective is to find gameplay mechanics that make the work itself more
interesting.

---

## Fun comes first

If BoreDOOM! makes a chore more complicated without making it more enjoyable,
it has failed.

Gamification should reduce resistance to boring work rather than add another
layer of admin.

---

## The real world matters

A BoreDOOM! level can correspond to an actual room, home, workshop, garage,
garden, shed, attic, or other environment.

The virtual world should therefore remain meaningfully connected to the real
one.

---

## The game must not lie

Completing a symbolic action in VR does not mean the equivalent physical chore
was performed.

BoreDOOM! should distinguish between:

```text
Virtual action
Player-reported real action
Observed real-world change
```

where those distinctions are relevant.

---

## Scale creates gameplay

Human and Borrower-scale players should not merely see differently sized
versions of the same game.

They should experience different problems, routes, abilities, and
responsibilities.

---

## Cooperation beats grinding

Multiplayer should reward useful collaboration rather than four people racing
independently for XP.

---

## Maintenance never disappears

Progression must never imply:

> You are now too important to clean.

Even the most advanced player can still receive:

```text
MISSION

HOOVER THE OFFICE
```

---

# Milestone 1: Barebones AR Hoovering

## Goal

Prove the original BoreDOOM! idea with the smallest genuinely useful gameplay
loop.

Build:

- [ ] Quest 3 passthrough AR scene
- [ ] Basic path tracking of hoover movement
- [ ] Simple grid-based coverage calculation
- [ ] Session summary with coverage percentage
- [ ] Basic scoring
- [ ] Spatial mesh capture
- [ ] Spatial mesh export

The first experience does not need:

- whole-house reconstruction
- Borrower scale
- multiplayer
- object understanding
- VNA integration
- complicated progression
- AI
- elaborate procedural generation

It needs to answer one question:

> **Does turning hoovering into visible coverage and gameplay make hoovering
> more engaging?**

## First gameplay loop

```text
BEGIN SESSION
      |
      v
MAP FLOOR
      |
      v
START HOOVERING
      |
      v
TRACK COVERAGE
      |
      v
SHOW MISSED AREAS
      |
      v
COMPLETE SESSION
      |
      v
SUMMARY
```

## Success criterion

A user can hoover an actual room while BoreDOOM! tracks meaningful coverage
and produces a useful summary of the session.

---

# Milestone 2: Make Hoovering a Game

Coverage alone is instrumentation.

This milestone begins turning the instrumentation into actual gameplay.

Potential experiments include:

- [ ] Coverage targets
- [ ] Missed-area highlighting
- [ ] Efficient-route scoring
- [ ] Time-based challenges
- [ ] Perfect-coverage challenges
- [ ] Streaks within a session
- [ ] Room completion
- [ ] Optional visual effects
- [ ] Sound feedback
- [ ] Chore-specific achievements
- [ ] Safety-aware play boundaries

Avoid rewarding unsafe rushing.

The best player is not necessarily the player who violently swings a hoover
around the room fastest.

## Success criterion

> The player would choose to launch BoreDOOM! before beginning the chore.

---

# Milestone 3: Lawn Mowing Mode

## Goal

Prove that BoreDOOM!'s coverage mechanics generalise beyond hoovering.

Build:

- [ ] Outdoor-safe AR scene
- [ ] Larger-area path tracking
- [ ] Coverage visualisation overlay
- [ ] Separate scoring model for mowing
- [ ] Missed-strip detection
- [ ] Coverage history experiments
- [ ] Outdoor safety considerations

The point is not merely to add another chore.

The point is to discover which BoreDOOM! concepts are reusable:

```text
Area
Coverage
Path
Efficiency
Completion
Safety
Session
Report
```

and which are chore-specific.

## Success criterion

The same underlying gameplay philosophy successfully supports a substantially
different real-world job.

---

# Milestone 4: Chore Framework

Once two materially different chores work, extract the common concepts.

Potential shared model:

```text
Chore
Area
Objective
Tool
Path
Coverage
Progress
Completion
Session
Result
Report
```

Build only abstractions proven necessary by real chore modes.

Do not create a giant universal chore engine before two working chores expose
the actual common requirements.

## Candidate chore families

### Cleaning

- [ ] Hoovering
- [ ] Sweeping
- [ ] Mopping
- [ ] Dusting
- [ ] Surface cleaning

### Tidying

- [ ] Clear floor
- [ ] Clear desk
- [ ] Put objects away
- [ ] Prepare room for cleaning

### Sorting

- [ ] Keep
- [ ] Store
- [ ] Move
- [ ] Donate
- [ ] Recycle
- [ ] Review

### Searching

- [ ] Find misplaced objects
- [ ] Locate hidden clutter
- [ ] Inspect difficult areas

### Maintenance

- [ ] Routine inspection
- [ ] Equipment checks
- [ ] Consumable replacement
- [ ] Simple household maintenance

Not every chore needs to become a coverage game.

Different work should produce different gameplay.

---

# Milestone 5: Chorewars Meta

## Goal

Create continuity between sessions without turning housework into a
meaningless XP treadmill.

Build:

- [ ] Player profiles
- [ ] Session history
- [ ] Streaks
- [ ] Basic progression
- [ ] Achievements
- [ ] Chore statistics
- [ ] Export/share statistics
- [ ] Room records
- [ ] Optional household challenges

Progression may eventually unlock:

- better tools
- richer visual feedback
- additional mission types
- cosmetic rewards
- vehicles
- navigation aids
- team capabilities
- new areas
- additional perspectives

Progression should unlock possibilities rather than merely increasing a
number.

---

# Milestone 6: Session Reports

## Goal

Make BoreDOOM! remember useful outcomes rather than only scores.

A session report might contain:

```text
SESSION

Area:
Office

Chore:
Hoovering

Coverage:
[measured result]

Areas requiring review:
[if known]

Game objectives:
[results]

Real-world completion:
[user confirmation where appropriate]

Notes:
[optional]

Remaining work:
[optional]
```

The report should distinguish between what BoreDOOM! actually measured and
what the player confirmed manually.

## Success criterion

A completed session leaves behind useful information about the chore rather
than only points.

---

# Milestone 7: Better Spatial Understanding

## Goal

Move from treating a room as an anonymous patch of floor towards understanding
it as a persistent place.

Build progressively:

- [ ] Persistent room identity
- [ ] Stable room origin
- [ ] Repeated-session alignment
- [ ] Named areas
- [ ] Obstacles
- [ ] Furniture boundaries
- [ ] Areas beneath furniture
- [ ] Cleaning zones
- [ ] Persistent room history

This is the beginning of the real environment becoming a persistent game map.

---

# Milestone 8: Whole-House Reconstruction

## Goal

Build a persistent representation of multiple real spaces.

Existing direction:

- [ ] Persistent anchor `home origin`
- [ ] Multi-session alignment of scans
- [ ] Combined export for external reconstruction tooling
- [ ] OBJ export
- [ ] PLY export

Expand towards:

- [ ] Multiple rooms
- [ ] Room relationships
- [ ] Doors and transitions
- [ ] Floors/storeys
- [ ] Persistent named locations
- [ ] Changed-environment detection
- [ ] Selective rescanning
- [ ] Privacy controls
- [ ] Local-first world storage

Conceptually:

```text
HOME
├── Living room
├── Kitchen
├── Office
├── Bedroom
├── Garage
├── Attic
├── Garden
└── Shed
```

The aim is not perfect architectural surveying.

The aim is a persistent environment sufficiently faithful to support useful
gameplay.

---

# Milestone 9: The Home Becomes a World

Once spaces persist, stop treating each room as an isolated chore session.

The home becomes a connected game environment.

Potential features:

- [ ] Free movement between mapped areas
- [ ] Chores visible in-world
- [ ] Persistent changes
- [ ] Mission selection from the environment
- [ ] Area history
- [ ] Discovered/unmapped areas
- [ ] Home overview
- [ ] Environmental progression
- [ ] Chore hotspots

A familiar environment can become interesting because BoreDOOM! gives the
player new reasons to inspect it.

---

# Milestone 10: Borrower Scale Prototype

## Goal

Prove that radically different scale creates genuinely different gameplay.

Build a prototype in one scanned room.

Human-scale player:

```text
Room ≈ room
Desk ≈ desk
Cable ≈ cable
Chair ≈ chair
```

Borrower-scale player:

```text
Room        -> landscape
Desk        -> plateau
Cable       -> bridge
Chair leg   -> tower
Carpet      -> terrain
PC          -> industrial complex
Storage box -> building
```

## Initial requirements

- [ ] Miniature player scale
- [ ] Appropriate movement
- [ ] Collision suitable for the smaller player
- [ ] Traversable under-furniture areas
- [ ] Scale-aware camera
- [ ] Large-distance problem at miniature scale
- [ ] At least one Borrower-specific objective

## Success criterion

Borrower mode feels like a distinct way to experience the room rather than a
camera-scale gimmick.

---

# Milestone 11: Human + Borrower Multiplayer

## Goal

Put differently scaled players into the same underlying environment.

Potential asymmetric abilities:

### Human

- [ ] Move large objects
- [ ] Cross room quickly
- [ ] Operate ordinary domestic tools
- [ ] Reach high areas
- [ ] Open large routes for smaller players

### Borrower

- [ ] Enter tiny spaces
- [ ] Explore under furniture
- [ ] Inspect clutter closely
- [ ] Access miniature routes
- [ ] Locate things difficult for the human player to see

## First cooperative mission

```text
GOAL

Prepare office for hoovering.


BORROWER

Explore beneath desk and furniture.

Identify hidden clutter.

Mark areas requiring attention.


HUMAN

Move accessible obstacles.

Collect identified clutter.

Clear floor.


TEAM

Prepare entire room.

Hoover.
```

## Success criterion

Neither scale can complete the mission as effectively alone as the two can
together.

---

# Milestone 12: The Human Changes the Borrower World

The human player should become a dynamic force within the miniature world.

Examples:

```text
Human moves chair
      ↓
Borrower landscape changes

Human lifts box
      ↓
New region exposed

Human opens drawer
      ↓
New Borrower environment

Human runs hoover
      ↓
Major environmental event
```

This should create genuine interaction between player scales.

The Borrower does not merely inhabit a static shrink-ray version of the room.

Their world changes because human-scale activity changes the physical
environment.

---

# Milestone 13: Borrower Transport

At miniature scale, ordinary rooms become enormous.

Transport becomes meaningful.

Potential experiments:

- [ ] Tiny bicycle
- [ ] Service cart
- [ ] RC vehicle
- [ ] Miniature van
- [ ] Improvised vehicles
- [ ] Cable routes
- [ ] Lifts
- [ ] Player-built shortcuts

Vehicles should solve actual scale-related gameplay problems rather than being
added merely because vehicles are fun.

Although vehicles are also fun.

---

# Milestone 14: Procedural Domestic Missions

## Goal

Allow persistent real environments to generate repeated gameplay.

Mission generation can consider:

- known room geometry
- known chore areas
- recently completed work
- user-marked jobs
- changes since previous sessions
- available players
- player scale
- available tools
- session duration

Potential missions:

```text
Clear the route
Prepare the room
Find the missing item
Inspect under furniture
Sort the workbench
Clear the floor
Clean the high-traffic area
Tidy the shelf
Investigate the garage
Hoover everything
```

Generated missions must remain comprehensible.

Procedural does not mean random nonsense.

---

# Milestone 15: Attic Attack!

Take BoreDOOM! into awkward domestic spaces.

Potential themes:

```text
Attic Attack!
Basement Bust!
Garage Barrage!
Shed Dread!
Cupboard Catastrophe!
Spare Room Reckoning!
```

These can have genuine gameplay differences.

An attic may emphasise:

- navigation
- categorisation
- forgotten objects
- difficult access
- storage archaeology

A garage may emphasise:

- tool organisation
- large objects
- maintenance
- vehicles
- equipment

A shed may simply contain seventeen mysterious extension leads and something
wet nobody remembers buying.

---

# Milestone 16: Household Archaeology

Domestic tidying often encounters forgotten possessions.

Not everything old is rubbish.

BoreDOOM! should eventually support:

```text
Keep
Investigate
Store
Display
Donate
Recycle
Dispose
Unknown
```

The game should encourage uncertainty rather than force a binary:

```text
KEEP / BIN
```

A sorting mission may end with a review pile rather than an artificial
decision.

This shares philosophy with Charkive's preservation-first approach without
making BoreDOOM! responsible for Charkive's storage analysis.

---

# Milestone 17: Generalisation and Privacy

A scanned home can expose extremely sensitive information.

Before serious sharing or multiplayer outside trusted household users,
BoreDOOM! should support privacy-preserving representation.

Potential controls:

- [ ] Remove photographs/textures
- [ ] Generalise room names
- [ ] Hide selected rooms
- [ ] Hide specific objects
- [ ] Replace real textures
- [ ] Simplify geometry
- [ ] Remove textual labels
- [ ] Export gameplay geometry without unnecessary personal detail

Example:

```text
REAL

Jonathan's office
Specific desk contents
Personal photographs
Named devices


GENERALISED

Work Area A
Desk
Storage Zone
Computer
```

The gameplay survives.

Unnecessary private information does not.

---

# Milestone 18: Safe Multiplayer Worlds

Allow generalised/scoped environments to be shared deliberately.

Potential modes:

```text
Household multiplayer
Friends
Private invited session
Generalised environment
Synthetic BoreDOOM! world
```

The player should control which representation is shared.

A real scanned home must never casually become a public map.

---

# Milestone 19: Community and Polish

- [ ] Better HUD
- [ ] Better spatial interfaces
- [ ] Sound design
- [ ] Accessibility options
- [ ] Safety prompts
- [ ] Comfort options
- [ ] Additional chore modes
- [ ] More environments
- [ ] Better session history
- [ ] Better onboarding
- [ ] Community-created synthetic chore worlds
- [ ] Shareable challenges without exposing private home scans

---

# Milestone 20: BoreDOOM! as a Gameplay Language

Once the domestic game works, identify concepts worth sharing with other
projects.

Possible reusable concepts include:

```text
World
Player
Scale
Mission
Objective
Tool
Vehicle
Interaction
Progress
Result
Report
Multiplayer role
Generalisation
```

These should not automatically become a giant shared framework.

Extract them only where other real projects need them.

---

# VNA Integration

Virtual Network Admin is the primary future specialist application of the
BoreDOOM! philosophy.

VNA can reuse ideas such as:

- embodied work
- missions
- spatial navigation
- progression
- multiplayer
- asymmetric roles
- Oracles
- reporting
- procedural environments
- real-world outcomes

But the division remains:

```text
BOREDOOM!

Domestic chores
Real spaces
Human/Borrower gameplay
Household missions
Chore progression


VNA

IT learning
OSI model
Operating-system worlds
Diagnostics
Infrastructure
Vendor equipment
SysMedic
FileCity
Charkive
Technical Oracles
```

The OSI Tower and historic IT technology progression belong in VNA.

They are not required for BoreDOOM!'s domestic game.

---

# FileCity and Charkive Integration

FileCity and Charkive may eventually appear through VNA-powered experiences.

BoreDOOM! can contribute the interaction language.

It should not absorb their responsibilities.

```text
FileCity
owns storage geography

Charkive
owns storage health and archaeology

VNA
owns infrastructure context

BoreDOOM!
owns the original chore-game philosophy and spatial gameplay experiments
```

Keeping those boundaries protects every project from becoming one enormous
everything-repo.

---

# Reporting Philosophy

BoreDOOM! should distinguish carefully between:

```text
What the game asked the player to do

What the game measured

What the player says they did

What can actually be verified
```

For example:

```text
SESSION REPORT

Mission:
Hoover Office

Measured:
94% tracked floor coverage

Game objective:
Complete

Player confirmation:
Room hoovered

Unverified:
Area beneath shelving

Follow-up:
Review beneath shelving next session
```

Honest uncertainty is better than pretending the virtual world knows more
about reality than it does.

---

# Scoring Philosophy

Scoring should reward useful behaviour.

Possible dimensions include:

```text
Coverage
Completion
Care
Efficiency
Cooperation
Consistency
Discovery
Safety
```

Do not reward:

```text
recklessness
unsafe movement
meaningless grinding
repeated trivial actions
dishonest completion
```

The scoring model should adapt to the chore.

Hoovering, lawn mowing, sorting, searching, and organising do not necessarily
need identical scoring systems.

---

# Progression Philosophy

Progression should follow:

```text
PARTICIPATION
     |
     v
COMPETENCE
     |
     v
BETTER TOOLS
     |
     v
NEW POSSIBILITIES
     |
     v
BIGGER / DIFFERENT JOBS
```

rather than:

```text
REPEAT TASK 500 TIMES
        |
        v
NUMBER GETS BIGGER
```

The player should feel that BoreDOOM! is gradually giving them richer ways to
approach boring work.

---

# The Base Game

BoreDOOM! needs a synthetic handcrafted base environment even if room scanning
becomes excellent.

The base game provides:

- tutorial
- accessibility
- testing
- common multiplayer environment
- demonstrations
- chore training
- play without scanning a home

The base game should embody BoreDOOM!'s humour and priorities.

The opening tutorial can remain extremely simple:

```text
WELCOME TO BOREDOOM!

This room is a tip.

Sort it out.
```

---

# Delivery Horizons

## Horizon 1: Prove the Chore

```text
Quest 3 passthrough
Hoover path tracking
Coverage
Scoring
Session summary
Spatial capture
```

**Question:** Does BoreDOOM! make hoovering more engaging?

---

## Horizon 2: Prove the Model

```text
Lawn mowing
Second chore family
Shared chore abstractions
Profiles
History
Progression
Reports
```

**Question:** Is there a reusable game underneath the hoover experiment?

---

## Horizon 3: Build the Home

```text
Persistent rooms
Multi-session alignment
Whole-house reconstruction
Room history
Persistent home world
```

**Question:** Can a real home become an enduring game map?

---

## Horizon 4: Change the Scale

```text
Borrower prototype
Human-scale multiplayer
Asymmetric cooperation
Dynamic scale interactions
Miniature transport
```

**Question:** Does the same real room produce genuinely different gameplay at
different scales?

---

## Horizon 5: Make the Home Alive

```text
Procedural missions
Environment changes
Household archaeology
More chore modes
Attics
Garages
Sheds
Gardens
```

**Question:** Can the home keep generating interesting reasons to play?

---

## Horizon 6: Share Safely

```text
Generalisation
Privacy controls
Scoped multiplayer
Synthetic shared worlds
Community challenges
```

**Question:** Can the experience become social without casually exposing the
inside of someone's home?

---

## Horizon 7: Share the Philosophy

```text
Reusable gameplay concepts
VNA integration
Specialist worlds
Real-work reporting
```

**Question:** Which parts of BoreDOOM!'s domestic experiment are useful
outside domestic chores?

---

# Non-Goals

BoreDOOM! itself should not become:

- a network-management system
- an operating-system simulator
- an infrastructure monitor
- a storage manager
- a filesystem cleaner
- a computer-repair toolkit
- the VNA digital twin
- an enterprise administration product

BoreDOOM!'s domain is:

> **Turning boring real-world domestic work into enjoyable spatial gameplay.**

Other projects may borrow that idea.

They do not need to live inside this repo.

---

# North Star

BoreDOOM! starts with one question:

> **Can we make hoovering fun?**

Then another:

> **Can the same idea make other boring jobs fun?**

Then:

> **Can your actual home become the game world?**

Then:

> **Can people experience the same home at radically different scales and work
> together inside it?**

The player puts on the headset because they want to play.

They explore an environment they already know.

The ordinary becomes strange.

The floor becomes a mission.

Under the desk becomes a wilderness.

The garage becomes an expedition.

At Borrower scale, the sofa becomes a megastructure.

At human scale, somebody still needs to move it.

The game creates a reason to do the job.

The session records what happened.

And when the headset comes off, something in the real world is hopefully a bit
better than it was before.

That is BoreDOOM!'s job.

The OSI Towers, operating-system worlds, SysMedic investigations, FileCity,
Charkive emergency services, technical Oracles, vendor equipment, and
infrastructure teleportation can become spectacular VNA worlds built using the
same philosophy.

But BoreDOOM! itself should never forget where all this started:

> **There's always something that needs hoovered.**

**Boring jobs. Glorious gameplay.**
