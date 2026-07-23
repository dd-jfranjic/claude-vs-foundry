using System.Threading.Tasks;
using ClaudeCode.VisualStudio.Services;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace ClaudeCode.VisualStudio
{
    /// <summary>
    /// Editor context menu: "Send to Claude Code". Takes the current selection (or just the
    /// active file when nothing is selected), shows the chat window and drops a ready-made
    /// reference + snippet into the composer — the user only adds the question and sends.
    /// </summary>
    [Command(PackageGuids.ClaudeCodeCmdSetString, PackageIds.SendSelectionToChat)]
    internal sealed class SendSelectionCommand : BaseCommand<SendSelectionCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var ide = new IdeContextService();
            SelectionContext sel = null;
            try { sel = await ide.GetActiveSelectionAsync(); } catch { }

            var pane = await ClaudeChatToolWindow.ShowAsync();
            var control = pane?.Content as ClaudeChatControl;
            if (control == null) return;

            string text;
            if (sel == null || string.IsNullOrEmpty(sel.FilePath))
                text = "";
            else if (sel.HasSelection && !string.IsNullOrEmpty(sel.Text))
                text = "@" + sel.FilePath + " (linije " + sel.StartLine + "-" + sel.EndLine + "):\n```" +
                       (sel.LanguageId ?? "") + "\n" + sel.Text + "\n```\n";
            else
                text = "@" + sel.FilePath + " ";

            control.InsertIntoComposer(text);
        }
    }
}
