# "Choose your weapon!" (module design)

Written by: Claude (AI). Passenger: Jon Davis (directed and tested; did not write this code).
Status: sound learning is built and unit tested (`Assets/Chorewars/Scripts/Weapon/ApplianceSound.cs`). The rest is design.

Jon's brief: scan your hoover, name it, then the headset tracks the machine using the noise it makes to monitor its activity.

## What each sense can and cannot do
| Sense | Tells us | Cannot tell us |
|---|---|---|
| Microphone (this module) | the hoover is running, and for how long | where it is in the room |
| Passthrough camera + detector (object recognition plan) | what it is and where it is on the floor | whether it is switched on |
Sound and sight are used together: the camera gives position, the microphone gives activity, so the game can credit cleaning only while it is actually running.

## Flow
1. **Scan.** Player looks at the hoover; the app saves a few passthrough frames of it (local only, on the headset then LAVE for training). Needs camera access (probe pending).
2. **Name it.** Player types a name with the system keyboard (TouchScreenKeyboard) or picks a suggestion ("Dysonius", "Sir Suckalot") by gaze. Stored in the player profile with the learned sound.
3. **Teach the sound.** App asks: "Keep it switched off... now switch it on" and listens for about 5 s each (microphone permission). `ApplianceSoundProfile` learns the two sounds and refuses if they are too alike.
4. **Play.** `ApplianceActivity` turns the live microphone into Running / Stopped with debounce, and counts running time. The hoover slice cleans floor only while Running and the camera sees the hoover.

## Limits to be straight about
- Sound gives no position. Without the camera detector the game knows the hoover runs but not where.
- A second noisy thing (TV, dishwasher) can fool it; the learned profile helps but does not remove this.
- The learned sound belongs to one room and one machine; re-teach if it stops working.
- Raw audio is never saved or sent; only the learned band energies are kept.

## Built
`ApplianceSoundProfile` (7-band FFT features, learn quiet/running, separation check, score) and `ApplianceActivity` (debounced state, active seconds). Tests: `ApplianceSoundTests`.

## Still to build
Microphone capture component, the screens (scan, name, teach), profile storage, camera scan, and wiring into `HooverSlice`.
