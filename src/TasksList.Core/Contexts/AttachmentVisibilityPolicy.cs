using TasksList.Core.Models;

namespace TasksList.Core.Contexts;

public static class AttachmentVisibilityPolicy
{
    public static bool ShouldShow(Note note, ContextId activeContextId)
    {
        if (note.Attachments.Count == 0 ||
            note.Attachments.Any(attachment => attachment.Visibility == AttachmentVisibility.RemainVisible))
        {
            return true;
        }

        return note.Attachments.Any(attachment => attachment.ContextId == activeContextId);
    }
}
