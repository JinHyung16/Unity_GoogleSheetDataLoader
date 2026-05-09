using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Jinhyeong_GoogleSheetDataLoader.Editor
{
    public static class OAuth2Authenticator
    {
        private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private const string Scopes = "https://www.googleapis.com/auth/spreadsheets";
        private const int RefreshSafetyMarginSeconds = 60;
        private const int RequestTimeoutSeconds = 30;

        public static async Task AuthorizeAsync(string clientId, string clientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("Client ID가 비어있습니다");
            }
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new ArgumentException("Client Secret이 비어있습니다");
            }

            string verifier = GenerateCodeVerifier();
            string challenge = ComputeCodeChallenge(verifier);
            int port = GetFreeLoopbackPort();
            string redirectUri = $"http://127.0.0.1:{port}";

            var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri + "/");
            listener.Start();

            try
            {
                string authUrl = BuildAuthUrl(clientId, redirectUri, challenge);
                Application.OpenURL(authUrl);

                HttpListenerContext ctx = await listener.GetContextAsync();

                string code = ctx.Request.QueryString["code"];
                string oauthError = ctx.Request.QueryString["error"];

                await SendBrowserResponseAsync(ctx, oauthError);

                if (string.IsNullOrEmpty(oauthError) == false)
                {
                    throw new Exception($"OAuth 거부됨: {oauthError}");
                }
                if (string.IsNullOrEmpty(code))
                {
                    throw new Exception("Authorization code 누락");
                }

                await ExchangeAuthCodeAsync(clientId, clientSecret, code, verifier, redirectUri);
            }
            finally
            {
                if (listener.IsListening)
                {
                    listener.Stop();
                }
                listener.Close();
            }
        }

        public static async Task<string> EnsureAccessTokenAsync(string clientId, string clientSecret)
        {
            if (OAuth2TokenStore.IsAccessTokenValid())
            {
                return OAuth2TokenStore.LoadAccessToken();
            }

            string refreshToken = OAuth2TokenStore.LoadRefreshToken();
            if (string.IsNullOrEmpty(refreshToken))
            {
                throw new InvalidOperationException("OAuth 인증이 필요합니다 ('인증' 버튼을 먼저 눌러주세요)");
            }

            await RefreshAccessTokenAsync(clientId, clientSecret, refreshToken);
            return OAuth2TokenStore.LoadAccessToken();
        }

        private static async Task ExchangeAuthCodeAsync(string clientId, string clientSecret, string code, string codeVerifier, string redirectUri)
        {
            var form = new WWWForm();
            form.AddField("client_id", clientId);
            form.AddField("client_secret", clientSecret);
            form.AddField("code", code);
            form.AddField("code_verifier", codeVerifier);
            form.AddField("grant_type", "authorization_code");
            form.AddField("redirect_uri", redirectUri);

            TokenResponse resp = await PostTokenRequestAsync(form);

            if (string.IsNullOrEmpty(resp.refresh_token))
            {
                throw new Exception("응답에 refresh_token이 없습니다 (Google Cloud Console에서 'Desktop' 타입 client인지 확인)");
            }
            OAuth2TokenStore.SaveRefreshToken(resp.refresh_token);
            OAuth2TokenStore.SaveAccessToken(resp.access_token, ComputeExpiry(resp.expires_in));
        }

        private static async Task RefreshAccessTokenAsync(string clientId, string clientSecret, string refreshToken)
        {
            var form = new WWWForm();
            form.AddField("client_id", clientId);
            form.AddField("client_secret", clientSecret);
            form.AddField("refresh_token", refreshToken);
            form.AddField("grant_type", "refresh_token");

            TokenResponse resp = await PostTokenRequestAsync(form);
            OAuth2TokenStore.SaveAccessToken(resp.access_token, ComputeExpiry(resp.expires_in));

            if (string.IsNullOrEmpty(resp.refresh_token) == false)
            {
                OAuth2TokenStore.SaveRefreshToken(resp.refresh_token);
            }
        }

        private static async Task<TokenResponse> PostTokenRequestAsync(WWWForm form)
        {
            using (UnityWebRequest req = UnityWebRequest.Post(TokenEndpoint, form))
            {
                req.timeout = RequestTimeoutSeconds;
                UnityWebRequestAsyncOperation op = req.SendWebRequest();
                while (op.isDone == false)
                {
                    await Task.Yield();
                }

                string body = req.downloadHandler != null ? req.downloadHandler.text : null;

#if UNITY_2020_1_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"토큰 요청 실패: {req.error} ({req.responseCode}) {body}");
                }
#else
                if (req.isNetworkError || req.isHttpError)
                {
                    throw new Exception($"토큰 요청 실패: {req.error} ({req.responseCode}) {body}");
                }
#endif

                TokenResponse resp = JsonUtility.FromJson<TokenResponse>(body);
                if (resp == null)
                {
                    throw new Exception("토큰 응답 파싱 실패");
                }
                if (string.IsNullOrEmpty(resp.error) == false)
                {
                    throw new Exception($"토큰 에러: {resp.error} {resp.error_description}");
                }
                if (string.IsNullOrEmpty(resp.access_token))
                {
                    throw new Exception("응답에 access_token 없음");
                }
                return resp;
            }
        }

        private static string BuildAuthUrl(string clientId, string redirectUri, string codeChallenge)
        {
            var sb = new StringBuilder(AuthEndpoint);
            sb.Append("?client_id=").Append(UnityWebRequest.EscapeURL(clientId));
            sb.Append("&redirect_uri=").Append(UnityWebRequest.EscapeURL(redirectUri));
            sb.Append("&response_type=code");
            sb.Append("&scope=").Append(UnityWebRequest.EscapeURL(Scopes));
            sb.Append("&code_challenge=").Append(codeChallenge);
            sb.Append("&code_challenge_method=S256");
            sb.Append("&access_type=offline");
            sb.Append("&prompt=consent");
            return sb.ToString();
        }

        private static int GetFreeLoopbackPort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        private static string GenerateCodeVerifier()
        {
            byte[] buf = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buf);
            }
            return Base64UrlEncode(buf);
        }

        private static string ComputeCodeChallenge(string verifier)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(verifier));
                return Base64UrlEncode(hash);
            }
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static DateTime ComputeExpiry(int expiresInSeconds)
        {
            int safe = Math.Max(0, expiresInSeconds - RefreshSafetyMarginSeconds);
            return DateTime.UtcNow.AddSeconds(safe);
        }

        private static async Task SendBrowserResponseAsync(HttpListenerContext ctx, string oauthError)
        {
            string title = string.IsNullOrEmpty(oauthError) ? "인증 완료" : "인증 실패";
            string body = string.IsNullOrEmpty(oauthError)
                ? "Unity 에디터로 돌아가세요. 이 창은 닫아도 됩니다."
                : $"OAuth 에러: {WebUtility.HtmlEncode(oauthError)}";
            string html =
                "<!DOCTYPE html><html><head><meta charset='utf-8'><title>" + title + "</title>" +
                "<style>body{font-family:-apple-system,Segoe UI,sans-serif;padding:48px;text-align:center;color:#222}h2{margin-bottom:8px}</style>" +
                "</head><body><h2>" + title + "</h2><p>" + body + "</p></body></html>";

            byte[] buf = Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = buf.Length;
            await ctx.Response.OutputStream.WriteAsync(buf, 0, buf.Length);
            ctx.Response.OutputStream.Close();
        }

        [Serializable]
        private class TokenResponse
        {
            public string access_token;
            public string refresh_token;
            public int expires_in;
            public string token_type;
            public string scope;
            public string error;
            public string error_description;
        }
    }
}
