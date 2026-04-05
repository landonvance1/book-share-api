using BookSharingApp.Models;

namespace BookSharingApp.Services;

public static class UserPiiScrubber
{
    /// <summary>
    /// Irreversibly scrubs PII from a user account and permanently locks it.
    /// </summary>
    /// <param name="user">The user to scrub (modified in-place).</param>
    /// <param name="label">
    /// A short label used to construct the tombstone display name, formatted as "[{label} User]".
    /// Use "Deleted" for self-initiated account deletion and "Banned" for admin bans,
    /// so the two cases are distinguishable in the database.
    /// </param>
    public static void Scrub(User user, string label)
    {
        user.Email = $"deleted-{user.Id}@deleted.invalid";
        user.NormalizedEmail = $"DELETED-{user.Id.ToUpper()}@DELETED.INVALID";
        user.UserName = $"deleted-{user.Id}";
        user.NormalizedUserName = $"DELETED-{user.Id.ToUpper()}";
        user.FirstName = $"[{label}";
        user.LastName = "User]";
        user.PhoneNumber = null;
        user.PasswordHash = null;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
    }
}
