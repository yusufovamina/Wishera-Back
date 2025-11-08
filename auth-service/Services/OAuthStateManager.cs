using System.Collections.Concurrent;

namespace auth_service.Services
{
    /// <summary>
    /// Shared state manager for OAuth flows to ensure state consistency across controllers.
    /// This prevents "Invalid state" errors when OAuth flow starts in one controller
    /// but the callback is handled by a different controller.
    /// </summary>
    public static class OAuthStateManager
    {
        // Thread-safe dictionary for storing OAuth state -> code verifier mappings
        private static readonly ConcurrentDictionary<string, string> GoogleStateToCodeVerifier = new();

        // Thread-safe dictionary for storing frontend origins by state GUID (for web clients)
        private static readonly ConcurrentDictionary<string, string> StateGuidToOrigin = new();

        /// <summary>
        /// Stores the code verifier for a given OAuth state.
        /// </summary>
        public static void StoreCodeVerifier(string state, string codeVerifier)
        {
            GoogleStateToCodeVerifier[state] = codeVerifier;
        }

        /// <summary>
        /// Retrieves and removes the code verifier for a given OAuth state.
        /// Returns null if the state is not found.
        /// </summary>
        public static string? TryGetAndRemoveCodeVerifier(string state)
        {
            if (GoogleStateToCodeVerifier.TryRemove(state, out var codeVerifier))
            {
                return codeVerifier;
            }
            return null;
        }

        /// <summary>
        /// Stores the frontend origin for a given state GUID (used for web clients).
        /// </summary>
        public static void StoreOrigin(string stateGuid, string origin)
        {
            StateGuidToOrigin[$"origin_{stateGuid}"] = origin;
        }

        /// <summary>
        /// Retrieves and removes the frontend origin for a given state GUID.
        /// Returns null if not found.
        /// </summary>
        public static string? TryGetAndRemoveOrigin(string stateGuid)
        {
            var key = $"origin_{stateGuid}";
            if (StateGuidToOrigin.TryRemove(key, out var origin))
            {
                return origin;
            }
            return null;
        }

        /// <summary>
        /// Clears all stored state (useful for testing or cleanup).
        /// </summary>
        public static void ClearAll()
        {
            GoogleStateToCodeVerifier.Clear();
            StateGuidToOrigin.Clear();
        }
    }
}

