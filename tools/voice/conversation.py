"""Turns raw transcripts into a conversation: fragments of one thought are joined, noise is dropped,
and each line is marked as probably meant for Claudia or probably for someone else in the room.

Written by: Claude (AI). Passenger: Jon Davis (ideas man; directed and tested, did not write this code).
"""
import re

# Words Whisper invents from breath, a click or silence; real short answers (yes, no, okay) are kept.
NOISE = {"you", "uh", "um", "mm", "hmm", "ah", "oh", "huh", "thank you", "thanks", "thanks for watching", "bye", "the"}

NAME = re.compile(r"\b(claudia|claude|claudio|cloudia)\b", re.I)


def clean(text):
    return re.sub(r"\s+", " ", text or "").strip()


def is_noise(text):
    t = re.sub(r"[^a-z' ]", "", clean(text).lower()).strip()
    return not t or t in NOISE


class Merger:
    """Holds a transcript for `wait` seconds so a thought broken by a pause goes out as one line."""

    def __init__(self, wait=1.2):
        self.wait = wait
        self._text = ""
        self._at = 0.0

    def add(self, text, now):
        text = clean(text)
        if not text:
            return
        self._text = (self._text + " " + text).strip()
        self._at = now

    def due(self, now):
        """The merged line once nothing new has arrived for `wait` seconds, else None."""
        if not self._text or now - self._at < self.wait:
            return None
        text, self._text = self._text, ""
        return text

    @property
    def pending(self):
        return bool(self._text)


class Attention:
    """Is the player talking to Claudia? Yes while a conversation is open: after they look at her, say her
    name, or she has just spoken. Otherwise it is probably said to someone else in the room."""

    def __init__(self, window=45.0):
        self.window = window
        self._until = 0.0

    def looked(self, now):
        self._until = now + self.window

    def spoke(self, now):
        self._until = max(self._until, now + self.window)

    def heard(self, text, now):
        """Returns True if the line is for Claudia. Her name, or an open conversation, keeps it open."""
        named = bool(NAME.search(text))
        addressed = named or now < self._until
        if addressed:
            self._until = now + self.window
        return addressed
