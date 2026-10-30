# BoreDOOM!

> **Boring work. Glorious gameplay. Real results.**

BoreDOOM! is an experiment in turning the boring, repetitive, educational,
and eventually useful parts of computing and everyday maintenance into an
explorable game world.

The premise is simple:

> **If work has to be done anyway, why shouldn't doing it be fun?**

BoreDOOM! begins with deliberately mundane chores and IT training. It grows
towards something much more ambitious: a playable digital twin in which real
system problems can become missions, players can investigate them together,
and a completed game session can produce a useful real-world report.

Gameplay is not intended to disguise work.

The ambition is to make useful work genuinely enjoyable.

And however experienced the player becomes, there will always be something
that needs hoovered.

---

## The player starts in 1960

The starting BoreDOOM! environment is deliberately primitive.

The player begins as a junior administrator in a 1960s-style Layer 7 office.

There is no teleportation.

There is no magical heads-up display.

There is no estate-wide telemetry.

There is certainly no button labelled:

```text
FIX NETWORK
```

Instead, there is:

- a desk
- a telephone
- filing cabinets
- paper records
- logbooks
- diagrams
- basic diagnostic equipment
- an elevator
- comfortable shoes

If something is broken somewhere else, the player has to go there.

As the player's knowledge and technology improve, so does the office.

Manual records become searchable.

Terminals arrive.

Computers appear.

Remote access becomes possible.

Telemetry becomes available.

The Oracle becomes more capable.

Transport becomes faster.

Eventually the player can move around an enormous digital estate almost
instantly.

Every convenience represents an abstraction the player has learned to
understand.

---

# The OSI Tower

BoreDOOM! turns the OSI model into architecture.

A computer or networked system can be represented as a seven-storey building.

```text
┌─────────────────────────────────────────┐
│ L7  APPLICATION                         │
│     Your office                         │
│     Applications, services, user needs  │
├─────────────────────────────────────────┤
│ L6  PRESENTATION                        │
│     Representation, encoding, crypto    │
├─────────────────────────────────────────┤
│ L5  SESSION                             │
│     Conversations and state             │
├─────────────────────────────────────────┤
│ L4  TRANSPORT                           │
│     Connections, TCP, UDP, ports        │
├─────────────────────────────────────────┤
│ L3  NETWORK                             │
│     Addressing and routing              │
├─────────────────────────────────────────┤
│ L2  DATA LINK                           │
│     Local links, switching, frames      │
├─────────────────────────────────────────┤
│ L1  PHYSICAL                            │
│     Physical communication              │
└────────────────────┬────────────────────┘
                     │
                OUTSIDE WORLD
```

The player normally inhabits Layer 7.

That is where human symptoms tend to appear:

```text
"The website isn't working."

"I can't reach the server."

"The application has stopped connecting."

"The cloud isn't syncing."

"Why is everything broken?"
```

The fault may be somewhere else entirely.

---

# The Elevator

The elevator is one of BoreDOOM!'s fundamental teaching mechanics.

A beginner experiencing a network problem does not simply select:

```text
GO TO FAULT
```

They investigate.

```text
Layer 7
   |
   v
Layer 6
   |
   v
Layer 5
   |
   v
Layer 4
   |
   v
Layer 3
   |
   v
Layer 2
   |
   v
Layer 1
```

Each floor can provide evidence about what is happening there.

The player gradually learns how problems at lower levels affect everything
above them.

---

# The Unplugged Cable

One of the earliest tutorial faults should be magnificently simple.

The user reports:

> **The application in the other office isn't working.**

The player begins at Layer 7.

The effect is visible throughout the stack:

```text
L7  Application unavailable
 ↑
L6  Communication unavailable
 ↑
L5  Session cannot establish
 ↑
L4  Transport cannot connect
 ↑
L3  Network unavailable
 ↑
L2  No working link
 ↑
L1  FAULT: PHYSICAL CONNECTION LOST
```

The player rides downward through the affected tower.

Eventually the elevator reaches Layer 1.

**DING.**

The cable is unplugged.

The beginner has just learnt something fundamental:

> **Where a problem is experienced is not necessarily where the problem
> originates.**

Once the connection is restored, health can propagate upwards through the
tower:

```text
L1  CONNECTED
      ↑
L2  LINK AVAILABLE
      ↑
L3  NETWORK AVAILABLE
      ↑
L4  TRANSPORT AVAILABLE
      ↑
L5  SESSION ESTABLISHED
      ↑
L6  DATA UNDERSTOOD
      ↑
L7  APPLICATION WORKING
```

A conceptual model has become a physical experience.

---

# Layer 1 Is the Outside World

Layer 1 is where machines meet reality.

When the player reaches the bottom of an OSI Tower, they may have to leave the
building and physically travel to another system.

Early in the game:

```text
SOURCE OFFICE
     |
     v
SOURCE OSI TOWER
     |
     v
LAYER 1
     |
     v
WALK
     |
     v
DESTINATION LAYER 1
     |
     v
DESTINATION OSI TOWER
     |
     v
DESTINATION OFFICE
```

The journey teaches the player that communication is not magic.

Information has to move.

---

# Technology Is Compressed Distance

Character progression in BoreDOOM! should not primarily consist of numerical
statistics.

The player earns better technology.

Early:

```text
Walk.
```

Later:

```text
Run.
```

Then:

```text
Bicycle.
```

Then:

```text
Car.
Bus.
Train.
High-speed rail.
Aircraft.
Remote terminal.
Remote presence.
Teleportation.
```

The precise mapping between game transport and technical media can evolve with
the game.

The principle remains:

> **The more the player understands, the less time they need to spend on
> mechanics they have already mastered.**

---

# Technology Progression

A possible progression is:

```text
WALK
  |
  v
RUN
  |
  v
BICYCLE
  |
  v
MOTORCYCLE
  |
  v
CAR
  |
  v
SERVICE VAN
  |
  v
BUS
  |
  v
TRAIN
  |
  v
HIGH-SPEED RAIL
  |
  v
AIRCRAFT
  |
  v
REMOTE TERMINAL
  |
  v
REMOTE CONSOLE
  |
  v
REMOTE PRESENCE
  |
  v
LAYER JUMP
  |
  v
SYSTEM TELEPORT
  |
  v
INCIDENT TELEPORT
```

Progression represents increasing abstraction.

The beginner has to travel.

The experienced administrator knows where to go.

---

# Teleportation Must Be Earned

Teleportation is not simply fast travel.

It represents expertise.

A beginner encountering a problem may need to inspect the entire stack.

An experienced administrator may examine the evidence and say:

> **That's Layer 3.**

At sufficiently advanced progression:

```text
TELEPORT
    |
    v
EDDIE
    |
    v
LAYER 3
```

The shortcut has been earned through understanding.

Teleportation should still respect evidence, permissions, and available
infrastructure.

If remote access is unavailable, telemetry is missing, or the route itself is
part of the problem, teleportation may fail.

Then the game hands the experienced administrator:

```text
Torch
Clipboard
Cable tester
Car keys
```

Back to Layer 1.

---

# The Office Evolves With the Player

The Layer 7 office acts as a physical representation of progress.

## Early office

```text
1960s
├── Filing cabinets
├── Paper logs
├── Wall diagrams
├── Rotary telephone
├── Teletype
└── Clipboard
```

## Developing office

```text
1970s / 1980s-inspired
├── Terminals
├── Electronic records
├── Better communications
├── Local diagnostics
└── Early computer systems
```

## Network operations

```text
├── Multiple displays
├── Network maps
├── Remote terminals
├── Event feeds
├── System status
└── Incident management
```

## Mature VNA environment

```text
├── Live digital twin
├── Cross-system telemetry
├── Historical state
├── AI Oracles
├── FileCity
├── Spatial incident mapping
└── Teleportation
```

The office tells the story of technological progress without requiring a
conventional skill-level number to do all the work.

---

# The Oracle

BoreDOOM! can use AI assistants as **Oracles**.

The Oracle should help the player reason about evidence rather than simply
solve the mission.

Oracle capability can itself progress.

## Beginner Oracle

```text
"Layer 1 does not appear healthy.
You may want to investigate the physical connection."
```

## Intermediate Oracle

```text
"Layers 1 and 2 appear healthy.
Where would you investigate next?"
```

## Advanced Oracle

```text
Evidence:

- interface: link up
- route: present
- connection attempts: timeout
- service state: available

What conclusion do you draw?
```

## Expert

The administrator gets the evidence.

The Oracle waits to be asked.

Difficulty therefore does not require inventing increasingly ridiculous
faults.

A difficult mission may simply provide less interpretation.

---

# T4: Explain the Same Truth Differently

Telemetry4Toddlers and its wider T4 family can provide explanations appropriate
to different players.

The same observation can support:

```text
T4Tots

"The computers can't talk because their connection is broken."


T4Teens

"The physical network connection is down, so the higher network layers cannot
communicate."


T4Techies

[technical evidence, interfaces, state, logs and measurements]
```

The explanation changes.

Reality does not.

---

# Operating-System Worlds

The OSI Tower provides a shared communications model, but operating systems
have their own internal worlds.

BoreDOOM! can eventually turn these into explorable environments.

## Windows World

Possible environments include:

```text
Services
Event logs
Registry
Updates
Drivers
Boot configuration
Storage
PowerShell
```

## Linux World

Possible environments include:

```text
systemd
journal
kernel
packages
mounts
filesystems
shell
configuration
```

## macOS World

Possible environments include:

```text
Unified logging
launchd
volumes
APFS
recovery
system configuration
security
```

The player should gradually learn that different operating systems often solve
related problems through different mechanisms.

Expertise is the ability to recognise the underlying concept despite the
different scenery.

---

# The Real Room

BoreDOOM! can also represent scanned real environments.

A room, office, workshop, server room, or other physical environment can become
a game level.

Different players may experience the same environment at radically different
scales.

---

# Human and Borrower Scale

One player might experience the room at approximately human scale.

Another player may experience it at "Borrower" scale.

For a miniature player:

```text
Desk       -> landscape
Cable      -> bridge
Server     -> enormous structure
PC         -> building
Rack       -> tower complex
Floor      -> terrain
```

The two players inhabit the same world but experience radically different
geographies.

This creates opportunities for asymmetric cooperative gameplay.

A human-scale player may be able to reach physical equipment easily.

A miniature player may be able to investigate representations of internal
systems and data that would be inaccessible at human scale.

---

# Boring Chores Still Matter

BoreDOOM! must retain its original purpose.

Not every mission is:

```text
CRITICAL INFRASTRUCTURE INCIDENT
```

Sometimes the mission is:

```text
Hoover the office.
```

Or:

```text
Put the equipment away.
```

Or:

```text
Sort the workshop.
```

Or:

```text
Document what you just changed.
```

The game should deliberately refuse the idea that seniority makes ordinary
maintenance beneath someone.

A legendary administrator with every teleportation ability unlocked can still
receive:

> **MISSION: Hoover Layer 7.**

Maintenance is part of engineering.

---

# FileCity

FileCity can eventually become an explorable data landscape within BoreDOOM!.

Storage becomes geography:

```text
Storage location -> District
Folder           -> Building
Subfolder        -> Floor or section
Capacity         -> Scale
Transfer         -> Traffic
```

Physical storage can occupy the ground.

Cloud storage can occupy the sky.

Movement can become visible:

```text
Ground -> Cloud      Rocket
Cloud -> Ground      Parachute
Ground -> Ground     Freight
Cloud -> Cloud       Air transport
```

These are playful representations of real or simulated storage operations.

---

# Charkive City Services

Charkive provides the storage-health and preservation layer.

Within BoreDOOM! or FileCity it can become:

```text
Sanitation Department
Police Department
Fire Department
Archaeology Service
SANitorium
```

Possible missions include:

- investigate storage pressure
- quarantine likely disposable data
- restore quarantined material
- investigate anomalies
- conduct archaeological surveys
- inspect Temporal Anomalies
- protect historically interesting collections

The visual metaphor must lead back to the actual finding and evidence.

---

# Data Archaeology

Not every mission should fix something.

Some should discover something.

Charkive may identify patterns of historical user activity within old storage.

BoreDOOM! can turn those findings into archaeological missions.

The player might explore an abandoned FileCity district and discover:

- old projects
- photographs
- document collections
- forgotten archives
- related historical material
- evidence of badly recorded dates

The mission ends not with destruction but with a report:

```text
Potential historical collection discovered.
Human review recommended.
```

---

# TEMPORAL ANOMALY

A Charkive **TEMPORAL ANOMALY (TA)** occurs where recorded timestamps appear
inconsistent with the surrounding evidence.

Within BoreDOOM!, a TA might appear as:

```text
A district that exists in the wrong historical era.

A building whose records claim it predates the city.

A set of photographs stranded on the wrong timeline.

A clock tower showing the wrong year.
```

The visual metaphor represents uncertainty.

The objective is to investigate the evidence.

The player may conclude:

> The device clock was probably wrong.

The original metadata remains evidence and should not be silently rewritten.

The gameplay result can become an annotation or recommendation rather than a
filesystem modification.

---

# SysMedic

SysMedic provides a bridge between BoreDOOM! and machines that are not
permanently connected to VNA.

A running or offline computer can produce diagnostic evidence.

Conceptually:

```text
Real machine
     |
     v
SysMedic
     |
     v
Encrypted snapshot
     |
     v
Analysis
     |
     v
Findings
     |
     v
VNA
     |
     v
Generalised world
     |
     v
BoreDOOM! mission
```

This creates the possibility of generating a playable scenario from a real
computer problem without giving the game direct control of that computer.

---

# Virtual Network Admin

VNA provides the wider reality model.

Its job is to understand real or simulated infrastructure.

BoreDOOM!'s job is to turn suitable parts of that model into an experience.

```text
REAL SYSTEMS
     |
     v
    VNA
     |
     v
GENERALISATION
     |
     v
BOREDOOM!
```

BoreDOOM! should not become the authoritative source of infrastructure state.

It is an interface to that state.

---

# Procedural Worlds

BoreDOOM! should support several kinds of world.

## Tutorial World

Handcrafted.

Designed to teach concepts clearly.

The 1960s office and first OSI Tower belong here.

## Synthetic World

Generated from deliberately simulated problems.

Useful for practice and assessment.

## Snapshot World

Generated from captured evidence from a real system.

For example, a SysMedic snapshot may produce a scenario based on an actual
machine.

## Live World

Eventually generated from authorised VNA telemetry.

The world reflects current infrastructure state.

These different sources must remain distinguishable.

A synthetic training scenario must never be presented as a live production
problem.

---

# Multiplayer Mini

BoreDOOM! can eventually allow real teams to investigate shared scenarios.

Potential roles include:

```text
Network engineer
Systems administrator
Storage engineer
Field engineer
Archivist
Incident coordinator
Trainee
```

These are responsibilities, not fantasy character classes.

Players might:

- divide investigations
- inspect different OSI layers
- visit different systems
- share evidence
- consult Oracles
- propose diagnoses
- review one another's conclusions
- produce a joint report

---

# Scoring

BoreDOOM! should not reward reckless speed.

Possible scoring dimensions include:

```text
Diagnostic accuracy
Quality of evidence
Safety
Communication
Documentation
Correct escalation
Unnecessary intervention avoided
Successful verification
Teamwork
```

A player who restarts everything until the problem disappears should not beat
a player who correctly identifies the root cause.

---

# Game Achievements and Professional Evidence

BoreDOOM! can have ridiculous achievements.

For example:

```text
Storage Fire Marshal
Layer 1 Detective
SANitorium Porter
Temporal Archaeologist
Hoovering Under Pressure
```

These are game achievements.

Professional assessment is different.

A future assessment mode could record structured evidence that a player
successfully demonstrated particular skills under controlled conditions.

Game scores and verified competence should remain distinct.

---

# Vendor Worlds

Hardware and software vendors may eventually provide authoritative virtual
equipment.

A vendor package might contribute:

```text
3D equipment
Component locations
Interfaces
Telemetry definitions
Firmware simulation
Normal behaviour
Failure scenarios
Approved procedures
Assessment criteria
```

This could allow players to gain practical familiarity with equipment they
cannot physically access.

The same equipment might eventually appear in:

- synthetic training scenarios
- captured SysMedic scenarios
- authorised live VNA environments

---

# Gameplay and Reality

BoreDOOM! begins as a read-only or symbolic representation.

Completing a mission does not automatically mean a real system was changed.

Initial flow:

```text
REAL OR SIMULATED FINDING
          |
          v
      GAME MISSION
          |
          v
 PLAYER INVESTIGATION
          |
          v
      CONCLUSION
          |
          v
     SESSION REPORT
```

If controlled real-world operations are ever introduced, they belong behind a
separate authority boundary:

```text
PLAYER REQUEST
      |
      v
PROPOSED ACTION
      |
      v
VALIDATION
      |
      v
AUTHORISATION
      |
      v
EXECUTION
      |
      v
VERIFICATION
      |
      v
AUDIT
```

Gameplay is not authorisation.

---

# The Report Is the Product

A useful BoreDOOM! session should be capable of ending with something more
valuable than a score.

For example:

```text
MISSION COMPLETE

Source:
Real / Synthetic / Snapshot / Live

Problem:
[identified condition]

Evidence examined:
[recorded evidence]

Diagnosis:
[player conclusion]

Confidence:
[assessment]

Recommended action:
[recommendation]

Real-world changes:
NONE

Escalation:
[if required]
```

If the mission represents a real issue, that report can become useful real
work.

The game has not wasted the player's time.

---

# First Playable Slice

The first BoreDOOM! implementation should be deliberately small.

Do not begin with FileCity, live VNA, procedural corporate estates, cloud
rockets, AI Oracles, vendor equipment, or teleportation.

Build:

```text
1960s Layer 7 office
        |
        v
Seven-floor OSI Tower
        |
        v
Working elevator
        |
        v
Layer 1 exit
        |
        v
Walk through physical world
        |
        v
Second OSI Tower
        |
        v
Reach destination Layer 7
```

## Mission 1

The application in the other office is unavailable.

The player investigates.

The cause is:

> **An unplugged cable at Layer 1.**

The player restores the connection symbolically.

The tower recovers upwards from Layer 1.

The player returns to the office.

A report is completed.

### Success Criterion

The player should finish the mission understanding:

> **A symptom can appear at Layer 7 even when the root cause is at Layer 1.**

No lecture is required.

They experienced it.

---

# Mission 2

Begin exactly like Mission 1.

The application does not work.

The player confidently travels to Layer 1.

The cable is connected.

Nothing is wrong there.

Welcome to troubleshooting.

---

# Long-Term Technology Progression

The overall progression should feel approximately like:

```text
1960s OFFICE
     |
     v
WALK EVERYWHERE
     |
     v
BASIC TOOLS
     |
     v
BICYCLE / BUS
     |
     v
CAR / SERVICE VAN
     |
     v
TERMINALS
     |
     v
NETWORK OPERATIONS
     |
     v
REMOTE ACCESS
     |
     v
WINDOWS / LINUX / macOS MASTERY
     |
     v
LIVE TELEMETRY
     |
     v
T4 + ORACLE
     |
     v
FILECITY
     |
     v
VNA DIGITAL TWIN
     |
     v
LAYER JUMP
     |
     v
SYSTEM TELEPORT
     |
     v
MULTIPLAYER MINI
```

Progression is not:

```text
GRIND
  |
  v
BIGGER NUMBER
```

It is:

```text
UNDERSTANDING
     |
     v
TRUST
     |
     v
BETTER TOOLS
     |
     v
FASTER MOVEMENT
     |
     v
GREATER RESPONSIBILITY
```

---

# Public Language Goal

BoreDOOM! should make technical language useful enough that players adopt it
naturally.

A player should eventually understand statements such as:

```text
"Layer 1 is down."

"Layer 2 looks healthy."

"This smells like Layer 3."

"Layer 4 can't reach the service."

"Layer 7 is where we're seeing the symptom."
```

The ambition is not to make players memorise seven definitions for an exam.

It is to make them experience the stack often enough that the terminology
becomes ordinary language.

The OSI model should become something players have:

- walked through
- ridden through
- diagnosed
- repaired
- become stranded inside
- argued about
- eventually learnt to teleport through

---

# Design Rule

> **Never teach through a diagram when the player could experience the diagram.**

If the concept is distance, make them travel.

If the concept is bandwidth, give the route capacity.

If the concept is congestion, let them encounter traffic.

If the concept is a dependency, let its failure affect everything above it.

If the concept is abstraction, let expertise remove repetition.

If the concept is maintenance, hand them the hoover.

---

# North Star

BoreDOOM! succeeds when somebody puts on a headset because they want to play a
game and takes it off having:

- understood something better
- investigated something real
- completed something useful
- or learned how computers actually fit together

The player begins in a 1960s office with a clipboard.

They have to walk everywhere.

They ride elevators through the OSI model.

They travel between machines.

They inspect logs.

They learn operating systems.

They acquire tools.

They gain remote access.

They meet Oracles.

They explore FileCity.

They respond to Charkive incidents.

They diagnose captured SysMedic machines.

They collaborate on VNA estates.

Eventually they earn the ability to teleport directly to the problem.

But expertise never exempts them from reality.

Sometimes remote access fails.

Sometimes the Oracle is wrong.

Sometimes the cable is unplugged.

And sometimes the most senior administrator in the building still has to
hoover the bloody office.

> **Boring work. Glorious gameplay. Real results.**
