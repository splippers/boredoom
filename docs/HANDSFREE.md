# Hands-free input

The player is holding a hoover. Or a dishcloth, or a bin bag. Anything that asks them to
put that down and pinch, point or press has already failed.

So the rule is: **the chore loop needs no input at all.** Input exists only for the
exceptions, and it is layered, because the layers will disagree with each other.

## The layers, weakest first

| Rank | Source | What it is | Why it is here |
|---|---|---|---|
| 0 | `Auto` | the loop drives itself | the default state of a chore |
| 1 | `Gaze` | look at a thing and hold | silent; survives vacuum noise |
| 2 | `Nod` | nod for yes, shake for no | binary confirm with both hands full |
| 4 | `Voice` | say it, via ORAC | the only layer that can name anything arbitrary |
| 5 | `Controller` | legacy panels | kept for the editor and for accessibility |

`HandsFreeInput` is the single place requests arrive. It enforces the ranking: a weaker
source is suppressed for `SourceLockout` seconds after a stronger one fires, so an
automatic "the room stopped growing" can never override a player who has just spoken.

## Typed, not guessed

`ChorewarsIntent` is a closed vocabulary - scans, modes, mark-clean, undo, confirm,
cancel, ask-ORAC, take-a-note. Everything that can be asked for hands-free is on that
list; nothing reaches the device as free text to be interpreted by something clever.

## What is implemented

| Piece | File | State |
|---|---|---|
| The vocabulary and its sources | `ChorewarsIntent.cs` | done |
| Phrase to intent, pure and testable | `VoiceCommandRouter.cs` | done |
| Nod / shake recognition over head pitch and yaw | `NodShakeLogic.cs` | done |
| ...driven from the head transform | `NodShakeDetector.cs` | done |
| A large, world-anchored thing you can look at | `GazeDwellTarget.cs` | logic only |
| Look-and-hold selection | `GazeDwellSelector.cs` | done |
| The merged, ranked input queue | `HandsFreeInput.cs` | done |
| The self-driving scan | `AutoScanDirector.cs` | done |

Verified in the editor by feeding it sentences and synthetic head angles rather than by
shouting at a headset in a kitchen:

```
[p88] router [ORAC, finish the scan]->FinishScan  [that's clean]->MarkClean
      [play hoover]->SelectMode:hoover  [yes]->Confirm  [no]->Cancel
      [ORAC]->CallOrac  [smells nice in here]->NONE
[p88] synthetic nod -> Nod
[p88] synthetic shake -> Shake
[p88] slow turn -> None          <- walking about must never mean "yes"
```

## Deliberate design notes

- **A slow turn is not a gesture.** Turning to look at the next room exceeds the
  excursion threshold but has no oscillation, so it is rejected. Rate is checked as well
  as amplitude.
- **The nod wins a tie.** If a movement is ambiguous, "yes" is the safer mistake.
- **Quest 3 has no eye tracking** - that is Quest Pro. "Gaze" means where the head
  points, which is coarse, so targets must be large and few: two or three on screen is
  the budget, not a menu.
- **Addressed-but-unintelligible goes to ORAC**, rather than the game saying "I did not
  understand that". He is better company.

## Still to do

1. **Voice capture.** Android `SpeechRecognizer` on-device first (no network, no key);
   fall back to streaming the microphone to the PC relay and using Whisper there. Needs
   a device test to see what the Quest build actually offers.
2. **The dwell ring.** `GazeDwellTarget` exposes `Focused` and `Dwell` but draws nothing
   yet; the ring wants the same line-rendering machinery as the clutter outline.
3. **Wire the layers into the scene.** The bootstrapper builds the world in code, so the
   input rig, the targets and the director belong there.
4. **Route intents to the game.** The actions currently inside `RuntimeScanPanel` and
   `ModeQuickSwitchPanel` need a shared surface to call, instead of each owning its own.
5. **Retire `HandPinchInput` as a default.** It becomes the accessibility path.

## Related: Quest thinks we need controllers

The first device launch was blocked by a system dialog:

```
dialogId=common_system_dialog_app_launch_blocked_controller_required
```

The system is treating the build as a controllers-required application. That belongs
with the manifest declaration of hand-tracking support, and it needs Meta's exact key
confirmed before anything is changed - and the standing rule that `Packages/manifest.json`
is not committed still applies.
