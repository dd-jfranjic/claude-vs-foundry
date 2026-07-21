using ClaudeCode.VisualStudio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClaudeCode.VisualStudio.Tests
{
    [TestClass]
    public class ClaudeSessionTests
    {
        [DataTestMethod]
        [DataRow(null, 0)]
        [DataRow("", 0)]
        [DataRow("none", 0)]
        [DataRow("low", 4096)]
        [DataRow("medium", 10000)]
        [DataRow("high", 16000)]
        [DataRow("extrahigh", 24000)]
        [DataRow("max", 31999)]
        [DataRow("ultracode", 31999)]
        [DataRow("bogus", 0)]
        public void ThinkingTokensForEffort_MapsLevels(string effort, int expected)
        {
            Assert.AreEqual(expected, ClaudeSession.ThinkingTokensForEffort(effort));
        }

        [TestMethod]
        public void ThinkingTokensForEffort_IsCaseInsensitive()
        {
            Assert.AreEqual(31999, ClaudeSession.ThinkingTokensForEffort("ULTRACODE"));
            Assert.AreEqual(4096, ClaudeSession.ThinkingTokensForEffort("Low"));
        }

        [DataTestMethod]
        [DataRow(null, null)]
        [DataRow("", null)]
        [DataRow("none", null)]
        [DataRow("bogus", null)]
        [DataRow("low", "low")]
        [DataRow("medium", "medium")]
        [DataRow("high", "high")]
        [DataRow("extrahigh", "xhigh")]
        [DataRow("max", "max")]
        [DataRow("ultracode", "ultracode")]
        public void EffortFlagFor_MapsComposerChoiceToCliLevel(string effort, string expected)
        {
            Assert.AreEqual(expected, ClaudeSession.EffortFlagFor(effort));
        }

        [TestMethod]
        public void EffortFlagFor_IsCaseInsensitive()
        {
            Assert.AreEqual("xhigh", ClaudeSession.EffortFlagFor("ExtraHigh"));
            Assert.AreEqual("ultracode", ClaudeSession.EffortFlagFor("ULTRACODE"));
        }
    }
}
