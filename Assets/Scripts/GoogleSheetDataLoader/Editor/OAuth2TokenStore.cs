using System;
using System.Globalization;
using UnityEditor;

namespace Jinhyeong_GoogleSheetDataLoader.Editor
{
    public static class OAuth2TokenStore
    {
        private const string PrefRefreshToken = "GoogleSheetDataLoader.OAuth.RefreshToken";
        private const string PrefAccessToken = "GoogleSheetDataLoader.OAuth.AccessToken";
        private const string PrefExpiresAtUtc = "GoogleSheetDataLoader.OAuth.ExpiresAtUtc";

        public static void SaveRefreshToken(string token)
        {
            EditorPrefs.SetString(PrefRefreshToken, token ?? string.Empty);
        }

        public static string LoadRefreshToken()
        {
            return EditorPrefs.GetString(PrefRefreshToken, string.Empty);
        }

        public static bool HasRefreshToken()
        {
            return string.IsNullOrEmpty(LoadRefreshToken()) == false;
        }

        public static void SaveAccessToken(string token, DateTime expiresAtUtc)
        {
            EditorPrefs.SetString(PrefAccessToken, token ?? string.Empty);
            EditorPrefs.SetString(PrefExpiresAtUtc, expiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
        }

        public static string LoadAccessToken()
        {
            return EditorPrefs.GetString(PrefAccessToken, string.Empty);
        }

        public static DateTime LoadAccessTokenExpiry()
        {
            string raw = EditorPrefs.GetString(PrefExpiresAtUtc, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return DateTime.MinValue;
            }
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                return parsed;
            }
            return DateTime.MinValue;
        }

        public static bool IsAccessTokenValid()
        {
            string token = LoadAccessToken();
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }
            return DateTime.UtcNow < LoadAccessTokenExpiry();
        }

        public static void Clear()
        {
            EditorPrefs.DeleteKey(PrefRefreshToken);
            EditorPrefs.DeleteKey(PrefAccessToken);
            EditorPrefs.DeleteKey(PrefExpiresAtUtc);
        }
    }
}
