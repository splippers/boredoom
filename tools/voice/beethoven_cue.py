# Ode to Joy (Beethoven, public domain), synthesised: plays a short cue on the PC speakers.
import math, struct, wave, sys
RATE = 22050
HZ = {"E":329.63,"F":349.23,"G":392.00,"D":293.66,"C":261.63}
# E E F G | G F E D | C C D E | E. D D
tune = [("E",1),("E",1),("F",1),("G",1),("G",1),("F",1),("E",1),("D",1),
        ("C",1),("C",1),("D",1),("E",1),("E",1.5),("D",0.5),("D",2)]
BEAT = 0.28
frames = bytearray()
for note, beats in tune:
    n = int(RATE * BEAT * beats)
    f = HZ[note]
    for i in range(n):
        t = i / RATE
        env = min(1, i / 300) * min(1, (n - i) / 1500)          # soft attack and release
        s = 0.6 * math.sin(2*math.pi*f*t) + 0.25 * math.sin(2*math.pi*2*f*t) + 0.1 * math.sin(2*math.pi*3*f*t)
        frames += struct.pack("<h", int(9000 * env * s))
with wave.open(sys.argv[1], "wb") as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(bytes(frames))
