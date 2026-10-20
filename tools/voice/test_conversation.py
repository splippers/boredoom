#!/usr/bin/env python3
"""Tests for conversation.py. Run: python3 tools/voice/test_conversation.py

Written by: Claude (AI). Passenger: Jon Davis (ideas man; directed and tested, did not write this code).
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import conversation as c  # noqa: E402


class NoiseTests(unittest.TestCase):
    def test_breath_words_are_noise_but_answers_are_not(self):
        for t in ["", "you", "Oh.", "Thank you.", "Um..."]:
            self.assertTrue(c.is_noise(t), t)
        for t in ["yes", "No.", "okay", "scan the ceiling of the kitchen"]:
            self.assertFalse(c.is_noise(t), t)


class MergerTests(unittest.TestCase):
    def test_fragments_of_one_thought_are_joined(self):
        m = c.Merger(1.0)
        m.add("Right, so", 0.0)
        self.assertIsNone(m.due(0.5))
        m.add("I'm in the kitchen", 0.9)
        self.assertIsNone(m.due(1.5))
        self.assertEqual("Right, so I'm in the kitchen", m.due(2.0))
        self.assertIsNone(m.due(5.0))

    def test_nothing_pending_gives_nothing(self):
        self.assertIsNone(c.Merger(1.0).due(10.0))


class AttentionTests(unittest.TestCase):
    def test_talk_with_no_conversation_open_is_not_for_her(self):
        self.assertFalse(c.Attention(45).heard("Here you see this thing on my hand", 100.0))

    def test_her_name_opens_a_conversation(self):
        a = c.Attention(45)
        self.assertTrue(a.heard("Claudia, scan the ceiling", 100.0))
        self.assertTrue(a.heard("and then the floor", 120.0))
        self.assertFalse(a.heard("and then the floor", 200.0))

    def test_looking_at_her_or_her_speaking_opens_it(self):
        a = c.Attention(45)
        a.looked(10.0)
        self.assertTrue(a.heard("what now", 30.0))
        b = c.Attention(45)
        b.spoke(10.0)
        self.assertTrue(b.heard("okay", 40.0))
        self.assertFalse(b.heard("okay", 100.0))


if __name__ == "__main__":
    unittest.main()
