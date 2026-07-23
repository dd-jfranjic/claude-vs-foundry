using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClaudeCode.VisualStudio.Services
{
    public sealed class StoredMessage
    {
        public string Role { get; set; }   // "user" | "assistant"
        public string Text { get; set; }
    }

    public sealed class SessionRecord
    {
        public string SessionId { get; set; }
        public string Model { get; set; } = "default";
        public string Mode { get; set; } = "default";
        public string Effort { get; set; } = "none";
        public bool ShowThinking { get; set; } = true;
        public DateTime StartedUtc { get; set; }
        public List<StoredMessage> Messages { get; set; } = new List<StoredMessage>();
    }

    /// <summary>One line in the per-workspace session-history index (newest first).</summary>
    public sealed class SessionSummary
    {
        public string SessionId { get; set; }
        public string Title { get; set; }
        public DateTime LastUtc { get; set; }
        public int MessageCount { get; set; }
    }

    /// <summary>
    /// Persists a chat session (id + options + transcript) per working directory so the
    /// conversation can be restored when the tool window or Visual Studio is reopened.
    /// Stored under %LOCALAPPDATA%\ClaudeCodeVS\sessions, one file per cwd.
    /// </summary>
    public static class SessionStore
    {
        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeCodeVS", "sessions");

        private const int MaxMessages = 200;

        private static string FileFor(string cwd)
        {
            var key = (cwd ?? string.Empty).Trim().ToLowerInvariant();
            using (var sha = SHA1.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
                var sb = new StringBuilder();
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return Path.Combine(Dir, sb.ToString() + ".json");
            }
        }

        public static SessionRecord Load(string cwd)
        {
            try
            {
                var path = FileFor(cwd);
                if (!File.Exists(path)) return null;
                var json = ReadDecrypted(path);
                return json == null ? null : JsonSerializer.Deserialize<SessionRecord>(json);
            }
            catch { return null; }
        }

        public static void Save(string cwd, SessionRecord rec)
        {
            try
            {
                if (rec == null) return;
                Directory.CreateDirectory(Dir);
                if (rec.Messages != null && rec.Messages.Count > MaxMessages)
                    rec.Messages.RemoveRange(0, rec.Messages.Count - MaxMessages);
                WriteEncrypted(FileFor(cwd), JsonSerializer.Serialize(rec));
            }
            catch { }
        }

        public static void Clear(string cwd)
        {
            try { var p = FileFor(cwd); if (File.Exists(p)) File.Delete(p); }
            catch { }
        }

        // ---------------------------------------------------------------------------------
        // Session history (v0.4): "New session" no longer loses the old conversation — it is
        // parked in a per-workspace archive (same DPAPI encryption as the live session) and
        // can be listed / reopened / deleted from the panel's History popover.
        // ---------------------------------------------------------------------------------

        private const int MaxArchived = 20;

        /// <summary>True when the id came from the CLI (usable with <c>--resume</c>). Sessions
        /// archived before the CLI ever reported an id get a synthetic <c>local-</c> id: their
        /// transcript restores fine, but the CLI starts fresh on the next message.</summary>
        public static bool IsResumable(string id) =>
            !string.IsNullOrEmpty(id) && !id.StartsWith("local-", StringComparison.Ordinal);

        /// <summary>First user message, whitespace-collapsed and capped — the session title.</summary>
        internal static string DeriveTitle(SessionRecord rec)
        {
            const int Max = 60;
            try
            {
                if (rec?.Messages != null)
                {
                    foreach (var m in rec.Messages)
                    {
                        if (m == null || m.Role != "user" || string.IsNullOrWhiteSpace(m.Text)) continue;
                        var t = System.Text.RegularExpressions.Regex.Replace(m.Text.Trim(), @"\s+", " ");
                        return t.Length <= Max ? t : t.Substring(0, Max).TrimEnd() + "…";
                    }
                }
            }
            catch { }
            return "Untitled session";
        }

        private static string SafeId(string id)
        {
            var sb = new StringBuilder();
            foreach (var c in id ?? string.Empty)
                if (char.IsLetterOrDigit(c) || c == '-') sb.Append(char.ToLowerInvariant(c));
            return sb.Length > 0 ? sb.ToString() : "x";
        }

        private static string ArchiveFileFor(string cwd, string id) =>
            FileFor(cwd).Replace(".json", ".a-" + SafeId(id) + ".json");

        private static string IndexFileFor(string cwd) =>
            FileFor(cwd).Replace(".json", ".index.json");

        private static List<SessionSummary> LoadIndex(string cwd)
        {
            try
            {
                var p = IndexFileFor(cwd);
                if (!File.Exists(p)) return new List<SessionSummary>();
                return JsonSerializer.Deserialize<List<SessionSummary>>(File.ReadAllText(p))
                       ?? new List<SessionSummary>();
            }
            catch { return new List<SessionSummary>(); }
        }

        private static void SaveIndex(string cwd, List<SessionSummary> index)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(IndexFileFor(cwd), JsonSerializer.Serialize(index));
            }
            catch { }
        }

        /// <summary>Park the CURRENT session (if it has any messages) in the archive. The live
        /// file is left in place — callers that start fresh follow up with <see cref="Clear"/>.</summary>
        public static void ArchiveCurrent(string cwd)
        {
            try
            {
                var rec = Load(cwd);
                if (rec?.Messages == null || rec.Messages.Count == 0) return;
                if (string.IsNullOrEmpty(rec.SessionId))
                    rec.SessionId = "local-" + Guid.NewGuid().ToString("N");

                Directory.CreateDirectory(Dir);
                WriteEncrypted(ArchiveFileFor(cwd, rec.SessionId), JsonSerializer.Serialize(rec));

                var index = LoadIndex(cwd);
                index.RemoveAll(s => s != null && s.SessionId == rec.SessionId);
                index.Insert(0, new SessionSummary
                {
                    SessionId = rec.SessionId,
                    Title = DeriveTitle(rec),
                    LastUtc = DateTime.UtcNow,
                    MessageCount = rec.Messages.Count,
                });
                while (index.Count > MaxArchived)
                {
                    var drop = index[index.Count - 1];
                    index.RemoveAt(index.Count - 1);
                    try { File.Delete(ArchiveFileFor(cwd, drop.SessionId)); } catch { }
                }
                SaveIndex(cwd, index);
            }
            catch { }
        }

        public static List<SessionSummary> ListArchived(string cwd) => LoadIndex(cwd);

        /// <summary>
        /// Plain-Markdown mirror of the conversation ("dnevnik") — one readable .md per
        /// session under <c>~/.claude/vs-dnevnik</c>, rewritten after every turn. DELIBERATELY
        /// unencrypted: the point is that the user (and Claude itself, when asked "what did we
        /// do yesterday?") can open and read it. The encrypted per-workspace store stays the
        /// restore source of truth.
        /// </summary>
        public static void SaveJournal(string cwd, SessionRecord rec)
        {
            try
            {
                if (rec?.Messages == null || rec.Messages.Count == 0) return;
                var dir = JournalDir();
                Directory.CreateDirectory(dir);
                if (rec.StartedUtc == default(DateTime)) rec.StartedUtc = DateTime.UtcNow;

                var slug = DeriveTitle(rec);
                foreach (var c in Path.GetInvalidFileNameChars()) slug = slug.Replace(c, ' ');
                if (slug.Length > 40) slug = slug.Substring(0, 40).TrimEnd();
                var file = Path.Combine(dir,
                    rec.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HHmm") + " " + slug + ".md");

                var sb = new StringBuilder();
                sb.AppendLine("# " + DeriveTitle(rec));
                sb.AppendLine();
                sb.AppendLine("- Radni direktorij: " + (cwd ?? ""));
                sb.AppendLine("- Model: " + rec.Model + " · mod: " + rec.Mode + " · effort: " + rec.Effort);
                sb.AppendLine("- Početak: " + rec.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                              + " · poruka: " + rec.Messages.Count);
                sb.AppendLine();
                foreach (var m in rec.Messages)
                {
                    sb.AppendLine(m != null && m.Role == "user" ? "## 🧑 Korisnik" : "## 🤖 Claude");
                    sb.AppendLine();
                    sb.AppendLine(m == null ? "" : (m.Text ?? ""));
                    sb.AppendLine();
                }
                File.WriteAllText(file, sb.ToString());
            }
            catch { }
        }

        internal static string JournalDir() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "vs-dnevnik");

        public static SessionRecord LoadArchived(string cwd, string id)
        {
            try
            {
                var p = ArchiveFileFor(cwd, id);
                if (!File.Exists(p)) return null;
                var json = ReadDecrypted(p);
                return json == null ? null : JsonSerializer.Deserialize<SessionRecord>(json);
            }
            catch { return null; }
        }

        public static void DeleteArchived(string cwd, string id)
        {
            try
            {
                try { File.Delete(ArchiveFileFor(cwd, id)); } catch { }
                var index = LoadIndex(cwd);
                index.RemoveAll(s => s != null && s.SessionId == id);
                SaveIndex(cwd, index);
            }
            catch { }
        }

        // The transcript can contain anything discussed in chat (incl. secrets), so it is encrypted
        // at rest with DPAPI — per-user, machine-bound, no key management. A short magic prefix marks
        // an encrypted file; a file without it is read as legacy plaintext (written before encryption
        // was added) and silently re-encrypted on the next Save.
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("CCVS1\n");

        private static void WriteEncrypted(string path, string json)
        {
            try
            {
                var cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
                var buf = new byte[Magic.Length + cipher.Length];
                Buffer.BlockCopy(Magic, 0, buf, 0, Magic.Length);
                Buffer.BlockCopy(cipher, 0, buf, Magic.Length, cipher.Length);
                File.WriteAllBytes(path, buf);
            }
            catch
            {
                // DPAPI unavailable (rare) — fall back to plaintext so the conversation still persists.
                File.WriteAllText(path, json);
            }
        }

        private static string ReadDecrypted(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (HasMagic(bytes))
            {
                var cipher = new byte[bytes.Length - Magic.Length];
                Buffer.BlockCopy(bytes, Magic.Length, cipher, 0, cipher.Length);
                var plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            // Legacy plaintext JSON (pre-encryption). Read as-is; the next Save upgrades it.
            return Encoding.UTF8.GetString(bytes);
        }

        private static bool HasMagic(byte[] b)
        {
            if (b.Length < Magic.Length) return false;
            for (int i = 0; i < Magic.Length; i++) if (b[i] != Magic[i]) return false;
            return true;
        }
    }
}
