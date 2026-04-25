using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Jinhyeong_GoogleSheetDataLoader.Editor
{
    public class GoogleSheetDataLoaderWindow : EditorWindow
    {
        private const string PrefKeyUrl = "GoogleSheetDataLoader.Url";
        private const string PrefKeyClientId = "GoogleSheetDataLoader.OAuth.ClientId";
        private const string PrefKeyClientSecret = "GoogleSheetDataLoader.OAuth.ClientSecret";
        private const string PrefKeyEnumSheetName = "GoogleSheetDataLoader.EnumSheetName";
        private const string PrefKeySelectedTab = "GoogleSheetDataLoader.SelectedTab";

        private const string DefaultEnumSheetName = "_Enum";

        private const float SIZE_W = 600f;
        private const float SIZE_H = 420f;

        private const int TabAuth = 0;
        private const int TabSync = 1;

        private static readonly string[] TabLabels = new[] { "구글 연동", "DB 로드" };

        private string _sheetUrl;
        private string _clientId;
        private string _clientSecret;
        private string _enumSheetName;
        private string _lastMessage;
        private MessageType _lastMessageType = MessageType.Info;
        private bool _busy;
        private Vector2 _scroll;
        private int _selectedTab;

        [MenuItem("Tools/Google Sheet Data Loader")]
        public static void Open()
        {
            var window = GetWindow<GoogleSheetDataLoaderWindow>("Google Sheet Loader");
            window.minSize = new Vector2(SIZE_W, SIZE_H);
            window.Show();
        }

        private void OnEnable()
        {
            _sheetUrl = EditorPrefs.GetString(PrefKeyUrl, string.Empty);
            _clientId = EditorPrefs.GetString(PrefKeyClientId, string.Empty);
            _clientSecret = EditorPrefs.GetString(PrefKeyClientSecret, string.Empty);
            _enumSheetName = EditorPrefs.GetString(PrefKeyEnumSheetName, DefaultEnumSheetName);
            _selectedTab = EditorPrefs.GetInt(PrefKeySelectedTab, TabAuth);
        }

        private void OnGUI()
        {
            DrawTabBar();
            EditorGUILayout.Space(6);

            using (var scope = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scope.scrollPosition;
                if (_selectedTab == TabAuth)
                {
                    DrawAuthTab();
                }
                else if (_selectedTab == TabSync)
                {
                    DrawSyncTab();
                }
            }
        }

        private void DrawTabBar()
        {
            int newTab = GUILayout.Toolbar(_selectedTab, TabLabels, GUILayout.Height(26));
            if (newTab != _selectedTab)
            {
                _selectedTab = newTab;
                EditorPrefs.SetInt(PrefKeySelectedTab, _selectedTab);
            }
        }

        private void DrawAuthTab()
        {
            EditorGUILayout.LabelField("구글 연동 (OAuth2)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "[Google Cloud + OAuth 설정]\n" +
                "1) https://console.cloud.google.com 에서 프로젝트 만들기\n" +
                "2) APIs & Services → Library:\n" +
                "   - Google Sheets API 사용 설정\n" +
                "   - Google Drive API 사용 설정\n" +
                "3) APIs & Services → OAuth consent screen:\n" +
                "   - User Type: External\n" +
                "   - Test users 에 본인 Google 계정 등록 (필수)\n" +
                "4) APIs & Services → Credentials → OAuth client ID:\n" +
                "   - Application type: Desktop app (필수)\n" +
                "   - 발급된 Client ID / Client Secret 아래 입력\n" +
                "5) [인증 (OAuth)] 클릭 → 브라우저 로그인 → 권한 동의\n" +
                "   (Windows 방화벽 팝업 뜨면 '허용')\n\n" +
                "한 번 인증되면 refresh token으로 자동 갱신, 재로그인 불필요.\n" +
                "시트는 비공개 유지 가능 (본인 계정 권한으로 접근).",
                MessageType.Info);

            EditorGUILayout.Space(6);

            DrawTextField("Client ID", PrefKeyClientId, ref _clientId, masked: false);
            DrawTextField("Client Secret", PrefKeyClientSecret, ref _clientSecret, masked: true);

            EditorGUILayout.Space(8);

            DrawAuthRow();
            EditorGUILayout.Space(6);
            DrawStatus();
        }

        private void DrawSyncTab()
        {
            EditorGUILayout.LabelField("DB 로드 / Enum 자동 생성", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "[로드 규칙]\n" +
                "- 스프레드시트의 모든 시트를 순회하며 처리\n" +
                "- 시트명 / 컬럼명이 '#'로 시작하면 제외 (주석/임시용)\n\n" +
                "[Enum 시트]\n" +
                "- 'Enum 시트명' 과 정확히 일치하는 시트는 C# enum 코드로 생성\n" +
                "  → Assets/Scripts/GeneratedEnums/<EnumName>.cs (asmdef 자동 생성)\n" +
                "  → 시트 row1 = enum 타입명, row2+ = enum 멤버\n\n" +
                "[일반 시트]\n" +
                "- Assets/Resources/GoogleSheetData/<시트명>.json 으로 저장\n" +
                "- row1 = 컬럼명 / row2 = 타입 / row3+ = 데이터\n" +
                "- 배열 셀 구분자: '|'",
                MessageType.Info);

            EditorGUILayout.Space(6);

            DrawAuthStatusLine();
            EditorGUILayout.Space(4);

            DrawTextField("스프레드시트 URL", PrefKeyUrl, ref _sheetUrl, masked: false);
            DrawTextField("Enum 시트명 (비우면 enum 자동 생성 안 함)", PrefKeyEnumSheetName, ref _enumSheetName, masked: false);

            EditorGUILayout.Space(8);

            DrawSyncRow();
            EditorGUILayout.Space(6);
            DrawStatus();
        }

        private void DrawAuthStatusLine()
        {
            bool authed = OAuth2TokenStore.HasRefreshToken();
            string text = authed
                ? "인증 상태: 인증됨 (refresh token 보유)"
                : "인증 상태: 인증 필요 → '구글 연동' 탭에서 먼저 인증하세요";
            MessageType type = authed ? MessageType.None : MessageType.Warning;
            EditorGUILayout.HelpBox(text, type);
        }

        private void DrawTextField(string label, string prefKey, ref string value, bool masked)
        {
            EditorGUILayout.LabelField(label, EditorStyles.label);
            string updated = masked
                ? EditorGUILayout.PasswordField(value)
                : EditorGUILayout.TextField(value);
            if (updated != value)
            {
                value = updated;
                EditorPrefs.SetString(prefKey, value ?? string.Empty);
            }
        }

        private void DrawAuthRow()
        {
            bool credsReady = string.IsNullOrWhiteSpace(_clientId) == false
                              && string.IsNullOrWhiteSpace(_clientSecret) == false;
            bool hasRefreshToken = OAuth2TokenStore.HasRefreshToken();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_busy || credsReady == false))
                {
                    if (GUILayout.Button(hasRefreshToken ? "재인증 (OAuth)" : "인증 (OAuth)", GUILayout.Height(28)))
                    {
                        _ = AuthorizeAsync();
                    }
                }

                using (new EditorGUI.DisabledScope(_busy || hasRefreshToken == false))
                {
                    if (GUILayout.Button("토큰 삭제", GUILayout.Height(28), GUILayout.Width(120)))
                    {
                        TryDeleteToken();
                    }
                }
            }

            EditorGUILayout.LabelField(
                hasRefreshToken ? "상태: 인증됨 (refresh token 보유)" : "상태: 인증 필요",
                EditorStyles.miniLabel);
        }

        private void DrawSyncRow()
        {
            bool ready = _busy == false
                         && OAuth2TokenStore.HasRefreshToken()
                         && string.IsNullOrWhiteSpace(_sheetUrl) == false
                         && string.IsNullOrWhiteSpace(_clientId) == false
                         && string.IsNullOrWhiteSpace(_clientSecret) == false;

            using (new EditorGUI.DisabledScope(ready == false))
            {
                if (GUILayout.Button("구글 시트 DB 최신화", GUILayout.Height(36)))
                {
                    _ = GoogleDBSyncAsync();
                }
            }
        }

        private void DrawStatus()
        {
            if (string.IsNullOrEmpty(_lastMessage))
            {
                return;
            }
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(_lastMessage, _lastMessageType);
        }

        private void TryDeleteToken()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "토큰 삭제 확인",
                "저장된 OAuth 토큰을 삭제하시겠습니까?\n삭제 후 동기화하려면 재인증이 필요합니다.",
                "Yes",
                "No");
            if (confirmed == false)
            {
                return;
            }

            OAuth2TokenStore.Clear();
            _lastMessage = "저장된 토큰을 삭제했습니다.";
            _lastMessageType = MessageType.Info;
            Repaint();
        }

        private async Task AuthorizeAsync()
        {
            _busy = true;
            try
            {
                _lastMessage = "브라우저에서 로그인 진행 중...";
                _lastMessageType = MessageType.Info;
                Repaint();

                await OAuth2Authenticator.AuthorizeAsync(_clientId, _clientSecret);

                _lastMessage = "OAuth 인증 완료. 이제 'DB 로드' 탭에서 동기화 가능.";
                _lastMessageType = MessageType.Info;
                Debug.Log("[GoogleSheetSync] OAuth 인증 완료");
            }
            catch (Exception e)
            {
                _lastMessage = $"인증 실패: {e.Message}";
                _lastMessageType = MessageType.Error;
                Debug.LogError($"[GoogleSheetSync] {e}");
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }

        private async Task GoogleDBSyncAsync()
        {
            _busy = true;
            try
            {
                _lastMessage = "동기화 중...";
                _lastMessageType = MessageType.Info;
                Repaint();

                List<string> savedPaths = await SheetJsonConverter.SyncAllAsync(_sheetUrl, _clientId, _clientSecret, _enumSheetName);

                _lastMessage = $"{savedPaths.Count}개 파일 저장 완료\n" + string.Join("\n", savedPaths);
                _lastMessageType = MessageType.Info;
                Debug.Log($"[GoogleSheetSync] {_lastMessage}");
            }
            catch (Exception e)
            {
                _lastMessage = $"동기화 실패: {e.Message}";
                _lastMessageType = MessageType.Error;
                Debug.LogError($"[GoogleSheetSync] {e}");
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }
    }
}
