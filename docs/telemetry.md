# Telemetry: telling WORKING from BROKEN from WAY OFF

The slice builds exist to answer one question per component without a person reading a log.
That needs three things: somewhere to put numbers that does not distort them, a stated
expectation to judge them against, and the losses admitted.

## The writer

`TelemetryWriter` owns the file. Lines go into a ring buffer and a worker thread drains it to
`Application.persistentDataPath/MQ3.log`, flushing about once a second. Nothing on the calling
side touches the disk, so a burst of events cannot stall the frame that produced them.

The buffer is a **ring**, not a queue, and that is the point: when it fills, the oldest line is
overwritten and the loss is counted. Telemetry must never stall the game, so losing old lines is
the right failure — but a report has to say it happened. `DroppedLines` is non-zero exactly when
the run outran the writer, and then the report is missing the *oldest* part of the burst. A
report that presents that gap as a lull is worse than one that admits it.

`TelemetryLogger` (the human log — a few lines an hour, each a sentence) now writes through the
same writer. It used to open, append and close the file per event, which is fine for a handful of
lines and ruinous at frame rate.

## The numbers

`TelemetrySamples` holds named series in memory and reports statistics:

```csharp
TelemetrySamples.Add("frameMs", dt * 1000f);
```

Percentiles come from a bounded ring of the most recent `Window` (4096) samples per series, so
memory is fixed whatever the run length. That means p95 answers *"what is bad like, lately"*;
lifetime min and max answer *"how bad did it get"*; and the count is lifetime, so a series that
stopped feeding shows up as a count that stopped growing.

Non-finite readings are **counted but excluded** from the arithmetic. A NaN that arrived is worth
knowing about; a NaN in the sum poisons min, max and mean for the rest of the run and hides
everything after it. `Count` and the numeric count are therefore kept apart.

## The verdicts

`ExpectationRegistry` is where a component says what it should do:

```csharp
ExpectationRegistry.Expect("posedRate", hz, ExpectationRange.Between(60f, 120f),
                                           ExpectationRange.Between(30f, 360f), "Hz");
```

Two ranges, because a single threshold cannot distinguish the two ways a component fails:

| Verdict | Meaning |
| --- | --- |
| `OK` | inside the range it has to be in |
| `BROKEN` | outside that range, but recognisably a measurement — the component ran and produced something wrong |
| `WAY_OFF` | NaN, infinite, zero where zero is impossible, or outside the plausible range — the number did not come from the component at all |
| `UNKNOWN` | nothing was reported. Not a pass |

Broken and WayOff send you to different places, which is why they are not the same verdict. A
framerate of 18 where 30 was expected is Broken: the code runs, the device is slow. A framerate of
`NaN` is WayOff: the sampler is not wired up, or the frame counter is being read before anything
writes it. `ExpectationRange.ExcludesZero` carries the other common case — a mesh count, a
framerate, a rate per second all have a floor above zero, and exactly zero means the number never
arrived rather than that the value is low.

A check bound to a series reports from that series rather than a single reading:

```csharp
ExpectationRegistry.ReportSamples("photoPerSec", "photoRate", SampleStatistic.P95);
```

The two names are separate because a check reads as *photoRate* and the series it is drawn from
reads as *photoPerSec*, and forcing them to agree makes both worse. Binding a check to a series
that never reported is a wiring mistake, so it is warned about once, by name, instead of sitting
at `UNKNOWN` forever looking like a slow start.

`Report()` gives one JSON line per section, `Summary()` gives the human line
(`fps OK, fps-low BROKEN (3 fps), meshCount WAY_OFF (0 meshes)`), which is what the in-headset
panel and the one-page report are both built from.

## What was checked

In the editor, driven directly rather than waiting for frames:

- 201 lines written and read back, each balanced JSON, with an embedded quote and tab surviving
  the round trip.
- Ring overflow, made deterministic by stopping the worker first: 8199 offered, exactly 8192 held
  and 7 counted as dropped; written + dropped equals what was offered, so nothing is lost
  silently.
- Count, min, max, mean, p50, p95 and p99 on a known series (1..1000) land exactly where they
  should.
- Faults injected to prove the verdicts fire: zero where zero is impossible, NaN, an order of
  magnitude out, just outside the range, inside the range, never reported, and a check bound to a
  series that never reported. Verdicts came out 2 OK / 2 BROKEN / 3 WAY_OFF / 2 UNKNOWN, and both
  report lines are balanced JSON.

## Not yet

- The run header (slice, git sha, device, OS, Unity version, OpenXR features, permissions, UK and
  UTC time) — §2.
- Per-frame sampling: frame time p50/p95/p99, dropped frames, GC allocation, memory, CPU/GPU level,
  thermal and battery at 1 Hz, guarded so the calls are absent off-device.
- The slice builds themselves: one entry point per slice with its own `applicationId` and
  scripting define, and `QuestBuild -slice <name>`.
- The in-headset panel: translucent, world-anchored, green/amber/red per component, toggled by a
  controller button, never opaque fullscreen.
- `tools/pull-telemetry.sh` and `tools/summarise-telemetry.py`.
- Privacy: numeric statistics only. Never write passthrough images to disk or network by default.
  A debug flag may store a few thumbnails **locally**, off by default — they show Jon's home.
