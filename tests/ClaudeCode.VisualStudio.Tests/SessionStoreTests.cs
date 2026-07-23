using System;
using ClaudeCode.VisualStudio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClaudeCode.VisualStudio.Tests
{
    [TestClass]
    public class SessionStoreTests
    {
        private static string NewCwd() => @"C:\unit-test\" + Guid.NewGuid().ToString("N");

        [TestMethod]
        public void SaveThenLoad_RoundTrips()
        {
            var cwd = NewCwd();
            try
            {
                var rec = new SessionRecord { SessionId = "sid-123", Model = "opus", Mode = "plan", Effort = "high" };
                rec.Messages.Add(new StoredMessage { Role = "user", Text = "hi" });
                rec.Messages.Add(new StoredMessage { Role = "assistant", Text = "hello there" });

                SessionStore.Save(cwd, rec);
                var got = SessionStore.Load(cwd);

                Assert.IsNotNull(got);
                Assert.AreEqual("sid-123", got.SessionId);
                Assert.AreEqual("opus", got.Model);
                Assert.AreEqual("plan", got.Mode);
                Assert.AreEqual("high", got.Effort);
                Assert.AreEqual(2, got.Messages.Count);
                Assert.AreEqual("user", got.Messages[0].Role);
                Assert.AreEqual("hello there", got.Messages[1].Text);
            }
            finally { SessionStore.Clear(cwd); }
        }

        // --- Session history / archive (v0.4) --------------------------------------------

        [TestMethod]
        public void DeriveTitle_UsesFirstUserMessage_CollapsedAndCapped()
        {
            var rec = new SessionRecord();
            rec.Messages.Add(new StoredMessage { Role = "assistant", Text = "ignored" });
            rec.Messages.Add(new StoredMessage { Role = "user", Text = "  Fix   the\n\nsync   bug  " });
            Assert.AreEqual("Fix the sync bug", SessionStore.DeriveTitle(rec));

            var longRec = new SessionRecord();
            longRec.Messages.Add(new StoredMessage { Role = "user", Text = new string('a', 200) });
            var title = SessionStore.DeriveTitle(longRec);
            Assert.IsTrue(title.Length <= 61, "title should be capped, was " + title.Length);
            Assert.IsTrue(title.EndsWith("…"));

            Assert.AreEqual("Untitled session", SessionStore.DeriveTitle(new SessionRecord()));
            Assert.AreEqual("Untitled session", SessionStore.DeriveTitle(null));
        }

        [TestMethod]
        public void ArchiveCurrent_ThenListLoadDelete_RoundTrips()
        {
            var cwd = NewCwd();
            try
            {
                var rec = new SessionRecord { SessionId = "sid-arch-1", Model = "opus" };
                rec.Messages.Add(new StoredMessage { Role = "user", Text = "analyse the Depots sync" });
                SessionStore.Save(cwd, rec);

                SessionStore.ArchiveCurrent(cwd);

                var list = SessionStore.ListArchived(cwd);
                Assert.AreEqual(1, list.Count);
                Assert.AreEqual("sid-arch-1", list[0].SessionId);
                Assert.AreEqual("analyse the Depots sync", list[0].Title);
                Assert.AreEqual(1, list[0].MessageCount);

                var loaded = SessionStore.LoadArchived(cwd, "sid-arch-1");
                Assert.IsNotNull(loaded);
                Assert.AreEqual("opus", loaded.Model);
                Assert.AreEqual("analyse the Depots sync", loaded.Messages[0].Text);

                SessionStore.DeleteArchived(cwd, "sid-arch-1");
                Assert.AreEqual(0, SessionStore.ListArchived(cwd).Count);
                Assert.IsNull(SessionStore.LoadArchived(cwd, "sid-arch-1"));
            }
            finally
            {
                SessionStore.DeleteArchived(cwd, "sid-arch-1");
                SessionStore.Clear(cwd);
            }
        }

        [TestMethod]
        public void ArchiveCurrent_EmptyOrMissingSession_IsANoOp()
        {
            var cwd = NewCwd();
            SessionStore.ArchiveCurrent(cwd);                       // nothing saved at all
            Assert.AreEqual(0, SessionStore.ListArchived(cwd).Count);

            try
            {
                SessionStore.Save(cwd, new SessionRecord { SessionId = "sid-empty" });   // no messages
                SessionStore.ArchiveCurrent(cwd);
                Assert.AreEqual(0, SessionStore.ListArchived(cwd).Count);
            }
            finally { SessionStore.Clear(cwd); }
        }

        [TestMethod]
        public void ArchiveCurrent_WithoutSessionId_GetsLocalIdAndIsNotResumable()
        {
            var cwd = NewCwd();
            try
            {
                var rec = new SessionRecord();                       // CLI never reported an id
                rec.Messages.Add(new StoredMessage { Role = "user", Text = "hi" });
                SessionStore.Save(cwd, rec);

                SessionStore.ArchiveCurrent(cwd);

                var list = SessionStore.ListArchived(cwd);
                Assert.AreEqual(1, list.Count);
                StringAssert.StartsWith(list[0].SessionId, "local-");
                Assert.IsFalse(SessionStore.IsResumable(list[0].SessionId));
                Assert.IsTrue(SessionStore.IsResumable("abc-123"));
                Assert.IsFalse(SessionStore.IsResumable(null));
            }
            finally
            {
                foreach (var s in SessionStore.ListArchived(cwd)) SessionStore.DeleteArchived(cwd, s.SessionId);
                SessionStore.Clear(cwd);
            }
        }

        [TestMethod]
        public void Load_Missing_ReturnsNull()
        {
            Assert.IsNull(SessionStore.Load(NewCwd()));
        }

        [TestMethod]
        public void Clear_RemovesRecord()
        {
            var cwd = NewCwd();
            var rec = new SessionRecord { SessionId = "x" };
            rec.Messages.Add(new StoredMessage { Role = "user", Text = "m" });
            SessionStore.Save(cwd, rec);
            Assert.IsNotNull(SessionStore.Load(cwd));

            SessionStore.Clear(cwd);
            Assert.IsNull(SessionStore.Load(cwd));
        }

        [TestMethod]
        public void Save_CapsTranscriptToLast200()
        {
            var cwd = NewCwd();
            try
            {
                var rec = new SessionRecord();
                for (int i = 0; i < 250; i++)
                    rec.Messages.Add(new StoredMessage { Role = "user", Text = "m" + i });

                SessionStore.Save(cwd, rec);
                var got = SessionStore.Load(cwd);

                Assert.AreEqual(200, got.Messages.Count);
                Assert.AreEqual("m50", got.Messages[0].Text);    // first 50 dropped
                Assert.AreEqual("m249", got.Messages[199].Text);
            }
            finally { SessionStore.Clear(cwd); }
        }

        [TestMethod]
        public void DifferentCwds_DoNotCollide()
        {
            var a = NewCwd();
            var b = NewCwd();
            try
            {
                SessionStore.Save(a, new SessionRecord { SessionId = "A" });
                SessionStore.Save(b, new SessionRecord { SessionId = "B" });
                Assert.AreEqual("A", SessionStore.Load(a).SessionId);
                Assert.AreEqual("B", SessionStore.Load(b).SessionId);
            }
            finally { SessionStore.Clear(a); SessionStore.Clear(b); }
        }
    }
}
