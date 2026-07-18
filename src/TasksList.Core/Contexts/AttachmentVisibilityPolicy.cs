using TasksList.Core.Models;

namespace TasksList.Core.Contexts;

public static class AttachmentVisibilityPolicy
{
    public static bool ShouldShow(Note note, ContextId activeContextId)
        => ShouldShow(note, activeContextId, new HashSet<ContextId> { activeContextId });

    public static bool ShouldShow(
        Note note,
        ContextId activeContextId,
        IReadOnlySet<ContextId> presentApplicationContexts)
    {
        if (note.Attachments.Count == 0 ||
            note.Attachments.Any(attachment => attachment.Visibility == AttachmentVisibility.RemainVisible))
        {
            return true;
        }

        return note.Attachments.Any(attachment => attachment.Visibility switch
        {
            AttachmentVisibility.ForegroundOnly or AttachmentVisibility.SleepUntilReturn =>
                attachment.ContextId == activeContextId,
            AttachmentVisibility.WhilePresent =>
                presentApplicationContexts.Contains(attachment.ContextId),
            _ => false,
        });
    }
}
