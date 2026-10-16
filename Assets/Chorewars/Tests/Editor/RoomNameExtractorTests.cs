using Chorewars.HandsFree;
using NUnit.Framework;

namespace Chorewars.Tests
{
    /// <summary>What the player says when naming a room, and what the router makes of it.</summary>
    public class RoomNameExtractorTests
    {
        static string Extract(string said)
        {
            Assert.IsTrue(RoomNameExtractor.TryExtract(said, out var name),
                "expected \"" + said + "\" to name a room");
            return name;
        }

        [TestCase("this is the kitchen", "Kitchen")]
        [TestCase("This is the kitchen.", "Kitchen")]
        [TestCase("this is kitchen", "Kitchen")]
        [TestCase("call this the kitchen", "Kitchen")]
        [TestCase("call it the kitchen", "Kitchen")]
        [TestCase("name this the kitchen", "Kitchen")]
        [TestCase("this room is the kitchen", "Kitchen")]
        [TestCase("we call this the kitchen", "Kitchen")]
        [TestCase("this is our kitchen", "Kitchen")]
        [TestCase("this is the living room", "Living Room")]
        [TestCase("this is the spare bedroom", "Spare Bedroom")]
        [TestCase("this is the hall", "Hall")]
        public void TryExtract_UnderstandsThePhrasingsPeopleActuallyUse(string said, string expected)
        {
            Assert.AreEqual(expected, Extract(said));
        }

        [Test]
        public void TryExtract_StopsAtTheEndOfTheName()
        {
            Assert.AreEqual("Kitchen", Extract("this is the kitchen and the hall is through there"));
        }

        [Test]
        public void TryExtract_RefusesAStatementAboutTheFloor()
        {
            // "this is clean" is the player commenting on their cleaning, not naming a room.
            Assert.IsFalse(RoomNameExtractor.TryExtract("this is clean", out _));
            Assert.IsFalse(RoomNameExtractor.TryExtract("this is done", out _));
        }

        [Test]
        public void TryExtract_RefusesASentenceThatNamesNothing()
        {
            Assert.IsFalse(RoomNameExtractor.TryExtract("", out _));
            Assert.IsFalse(RoomNameExtractor.TryExtract("this is", out _));
            Assert.IsFalse(RoomNameExtractor.TryExtract("I am very tired", out _));
        }

        [Test]
        public void TryExtract_TakesAnOverlongUtteranceOutOfTheRunning()
        {
            Assert.IsFalse(RoomNameExtractor.TryExtract(
                "this is the supercalifragilisticexpialidocious", out _));
        }

        [Test]
        public void Router_NamesTheRoom_AndCarriesTheNameAsTheArgument()
        {
            Assert.IsTrue(VoiceCommandRouter.TryParse("this is the kitchen", null, out var request));
            Assert.AreEqual(ChorewarsIntent.NameRoom, request.Intent);
            Assert.AreEqual("Kitchen", request.Argument);
            Assert.AreEqual(IntentSource.Voice, request.Source);
        }

        [Test]
        public void Router_AddressedToOrac_StillNamesTheRoom()
        {
            // Saying "hey ORAC, this is the kitchen" is still naming a room; it must not be
            // swallowed as chatter for ORAC.
            Assert.IsTrue(VoiceCommandRouter.TryParse("hey ORAC, this is the kitchen", null, out var request));
            Assert.AreEqual(ChorewarsIntent.NameRoom, request.Intent);
            Assert.AreEqual("Kitchen", request.Argument);
        }

        [Test]
        public void Router_KeepsTheOldMeaningOfThisIsClean()
        {
            // The rooms feature must not steal an existing phrase: "this is clean" is still MarkClean.
            Assert.IsTrue(VoiceCommandRouter.TryParse("this is clean", null, out var request));
            Assert.AreEqual(ChorewarsIntent.MarkClean, request.Intent);
        }

        [Test]
        public void Describe_SaysWhatWasUnderstood()
        {
            Assert.AreEqual("call this the Kitchen", VoiceCommandRouter.Describe(ChorewarsIntent.NameRoom, "Kitchen"));
            Assert.AreEqual("name this room", VoiceCommandRouter.Describe(ChorewarsIntent.NameRoom, null));
        }
    }
}
