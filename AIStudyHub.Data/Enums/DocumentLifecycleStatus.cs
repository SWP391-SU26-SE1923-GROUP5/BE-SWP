namespace AIStudyHub.Data.Enums;

/// <summary>
/// Soft-delete state machine for documents.
/// Active: normal document visible in the main list.
/// Trashed: moved to trash bin, can be restored or purged.
/// Purged: permanently deleted, cannot be restored.
/// </summary>
public enum DocumentLifecycleStatus
{
    Active = 0,
    Trashed = 1,
    Purged = 2
}
