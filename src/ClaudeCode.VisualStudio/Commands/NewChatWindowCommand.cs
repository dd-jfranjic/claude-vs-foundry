using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace ClaudeCode.VisualStudio
{
    /// <summary>
    /// View &gt; Other Windows &gt; Claude Code - New Window. Opens an ADDITIONAL, independent
    /// chat window (its own CLI process and conversation) next to the primary one — e.g. one
    /// window analyzing while another refactors. Extra windows are scratch: they don't touch
    /// the persisted workspace session, so the primary window's restore never gets clobbered.
    /// </summary>
    [Command(PackageGuids.ClaudeCodeCmdSetString, PackageIds.NewClaudeChatWindow)]
    internal sealed class NewChatWindowCommand : BaseCommand<NewChatWindowCommand>
    {
        // Monotonic id per VS session; id 0 stays reserved for the primary window.
        private static int _nextId;

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            _nextId++;
            await ClaudeChatToolWindow.ShowAsync(_nextId, create: true);
        }
    }
}
