using System.Text.Json;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.Services;

/// <summary>
/// Provides persistent storage and server synchronization for the Mobile app's wishlist.
/// Ensures wishlist items survive app restarts for both guest users and authenticated customers.
/// </summary>
public static class MobileWishlistSync
{
    private const string GlobalWishlistKey = "nube_mobile_wishlist";

    /// <summary>
    /// Singleton reference to WishlistService for easy, reliable resolution across MAUI views and controls.
    /// </summary>
    public static WishlistService? CurrentWishlist { get; set; }

    private static string UserWishlistKey(string email) =>
        $"nube_mobile_wishlist:{email.Trim().ToLowerInvariant()}";

    /// <summary>
    /// Synchronously restores wishlist from device local storage (Preferences).
    /// Safe to call immediately during app startup or ViewModel initialization.
    /// </summary>
    public static void RestoreLocal(WishlistService wishlist, string? email = null)
    {
        CurrentWishlist ??= wishlist;
        try
        {
            string? json = null;

            if (!string.IsNullOrWhiteSpace(email))
            {
                json = Preferences.Default.Get<string?>(UserWishlistKey(email), null);
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                json = Preferences.Default.Get<string?>(GlobalWishlistKey, null);
            }

            if (!string.IsNullOrWhiteSpace(json))
            {
                var ids = JsonSerializer.Deserialize<List<int>>(json);
                if (ids is not null)
                {
                    ApplyIds(wishlist, ids);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MobileWishlistSync] Failed to restore local wishlist: {ex.Message}");
        }
    }

    /// <summary>
    /// Synchronizes the in-memory WishlistService with target IDs using standard Toggle() method,
    /// eliminating the need to modify the shared WishlistService class.
    /// </summary>
    public static void ApplyIds(WishlistService wishlist, IEnumerable<int> targetIds)
    {
        CurrentWishlist ??= wishlist;
        var targetSet = new HashSet<int>(targetIds);
        foreach (var id in wishlist.Ids.ToList())
        {
            if (!targetSet.Contains(id))
            {
                wishlist.Toggle(id);
            }
        }

        foreach (var id in targetSet)
        {
            if (!wishlist.Contains(id))
            {
                wishlist.Toggle(id);
            }
        }
    }

    /// <summary>
    /// Saves the current wishlist to device storage immediately and, if logged in, syncs to the server.
    /// </summary>
    public static async Task SaveAsync(WishlistService wishlist, string? email = null, IAppDatabase? db = null)
    {
        CurrentWishlist ??= wishlist;
        try
        {
            var ids = wishlist.Ids.ToList();
            var json = JsonSerializer.Serialize(ids);

            // 1. Always persist to device storage for offline and guest persistence
            Preferences.Default.Set(GlobalWishlistKey, json);

            if (!string.IsNullOrWhiteSpace(email))
            {
                Preferences.Default.Set(UserWishlistKey(email), json);

                // 2. If user is authenticated, sync to cloud database
                if (db is not null)
                {
                    try
                    {
                        await db.SaveWishlistAsync(email, ids);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MobileWishlistSync] Server save failed: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MobileWishlistSync] Failed to save wishlist: {ex.Message}");
        }
    }

    /// <summary>
    /// Syncs local wishlist with server-stored wishlist when an account is active.
    /// Merges both lists so items saved as a guest or offline are not lost.
    /// </summary>
    public static async Task SyncWithServerAsync(WishlistService wishlist, string email, IAppDatabase db)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        try
        {
            // Ensure local items are loaded first
            RestoreLocal(wishlist, email);

            var serverIds = await db.GetWishlistAsync(email);
            if (serverIds is not null)
            {
                // Union local items with server items
                var merged = serverIds.Union(wishlist.Ids).Distinct().ToList();
                ApplyIds(wishlist, merged);

                var json = JsonSerializer.Serialize(merged);
                Preferences.Default.Set(GlobalWishlistKey, json);
                Preferences.Default.Set(UserWishlistKey(email), json);

                // If local had items not yet on the server, upload the merged list
                if (merged.Count != serverIds.Count)
                {
                    await db.SaveWishlistAsync(email, merged);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MobileWishlistSync] Server sync error: {ex.Message}");
        }
    }
}
