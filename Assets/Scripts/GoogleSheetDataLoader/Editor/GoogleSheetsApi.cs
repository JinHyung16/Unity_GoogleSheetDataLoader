using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Jinhyeong_GoogleSheetDataLoader.Editor
{
    public class SheetMeta
    {
        public string Title;
        public int SheetId;
    }

    public static class GoogleSheetsApi
    {
        private const string SheetsApiBase = "https://sheets.googleapis.com/v4/spreadsheets";
        private const int MetadataTimeoutSeconds = 30;
        private const int ValuesTimeoutSeconds = 60;

        public static async Task<List<SheetMeta>> ListSheetsAsync(string spreadsheetId, string accessToken)
        {
            string url = $"{SheetsApiBase}/{spreadsheetId}?fields=sheets.properties(title,sheetId)";
            string json = await GetTextAsync(url, accessToken, MetadataTimeoutSeconds);

            SheetsListResponse parsed = JsonUtility.FromJson<SheetsListResponse>(json);
            var result = new List<SheetMeta>();

            if (parsed == null || parsed.sheets == null)
            {
                return result;
            }

            for (int i = 0; i < parsed.sheets.Length; i++)
            {
                SheetEntry entry = parsed.sheets[i];
                if (entry == null || entry.properties == null)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(entry.properties.title))
                {
                    continue;
                }
                result.Add(new SheetMeta
                {
                    Title = entry.properties.title,
                    SheetId = entry.properties.sheetId,
                });
            }
            return result;
        }

        public static async Task<List<List<string>>> GetValuesAsync(string spreadsheetId, string sheetTitle, string accessToken)
        {
            if (string.IsNullOrEmpty(sheetTitle))
            {
                throw new ArgumentException("시트 이름이 비어있습니다");
            }

            string range = "'" + sheetTitle.Replace("'", "''") + "'";
            string encodedRange = UnityWebRequest.EscapeURL(range);
            string url = $"{SheetsApiBase}/{spreadsheetId}/values/{encodedRange}?majorDimension=ROWS&valueRenderOption=FORMATTED_VALUE&dateTimeRenderOption=FORMATTED_STRING";

            string json = await GetTextAsync(url, accessToken, ValuesTimeoutSeconds);
            return SheetsValuesJson.ParseValues(json);
        }

        private static async Task<string> GetTextAsync(string url, string accessToken, int timeoutSeconds)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                req.timeout = timeoutSeconds;
                req.redirectLimit = 8;

                UnityWebRequestAsyncOperation op = req.SendWebRequest();
                while (op.isDone == false)
                {
                    await Task.Yield();
                }

                string body = req.downloadHandler != null ? req.downloadHandler.text : null;

#if UNITY_2020_1_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"GET 실패 ({req.responseCode}): {req.error} {body}");
                }
#else
                if (req.isNetworkError || req.isHttpError)
                {
                    throw new Exception($"GET 실패 ({req.responseCode}): {req.error} {body}");
                }
#endif

                if (string.IsNullOrEmpty(body))
                {
                    throw new Exception("응답이 비어있습니다");
                }
                return body;
            }
        }

        [Serializable]
        private class SheetsListResponse
        {
            public SheetEntry[] sheets;
        }

        [Serializable]
        private class SheetEntry
        {
            public SheetProperties properties;
        }

        [Serializable]
        private class SheetProperties
        {
            public string title;
            public int sheetId;
        }

        internal static class SheetsValuesJson
        {
            public static List<List<string>> ParseValues(string json)
            {
                var rows = new List<List<string>>();
                if (string.IsNullOrEmpty(json))
                {
                    return rows;
                }

                int idx = FindValuesArrayStart(json);
                if (idx < 0)
                {
                    return rows;
                }
                idx++;

                while (idx < json.Length)
                {
                    SkipWhitespace(json, ref idx);
                    if (idx >= json.Length)
                    {
                        break;
                    }
                    char c = json[idx];
                    if (c == ']')
                    {
                        break;
                    }
                    if (c == ',')
                    {
                        idx++;
                        continue;
                    }
                    if (c == '[')
                    {
                        idx++;
                        rows.Add(ParseRow(json, ref idx));
                        continue;
                    }
                    idx++;
                }
                return rows;
            }

            private static int FindValuesArrayStart(string json)
            {
                int keyIdx = json.IndexOf("\"values\"", StringComparison.Ordinal);
                if (keyIdx < 0)
                {
                    return -1;
                }
                int colonIdx = json.IndexOf(':', keyIdx);
                if (colonIdx < 0)
                {
                    return -1;
                }
                int bracketIdx = json.IndexOf('[', colonIdx);
                return bracketIdx;
            }

            private static List<string> ParseRow(string json, ref int idx)
            {
                var row = new List<string>();
                while (idx < json.Length)
                {
                    SkipWhitespace(json, ref idx);
                    if (idx >= json.Length)
                    {
                        break;
                    }
                    char c = json[idx];
                    if (c == ']')
                    {
                        idx++;
                        break;
                    }
                    if (c == ',')
                    {
                        idx++;
                        continue;
                    }
                    if (c == '"')
                    {
                        row.Add(ReadString(json, ref idx));
                        continue;
                    }
                    row.Add(ReadLiteral(json, ref idx));
                }
                return row;
            }

            private static string ReadString(string json, ref int idx)
            {
                idx++;
                var sb = new StringBuilder();
                while (idx < json.Length)
                {
                    char c = json[idx];
                    if (c == '"')
                    {
                        idx++;
                        return sb.ToString();
                    }
                    if (c == '\\' && idx + 1 < json.Length)
                    {
                        char esc = json[idx + 1];
                        idx += 2;
                        switch (esc)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (idx + 4 <= json.Length)
                                {
                                    string hex = json.Substring(idx, 4);
                                    if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                    {
                                        sb.Append((char)code);
                                    }
                                    idx += 4;
                                }
                                break;
                            default: sb.Append(esc); break;
                        }
                        continue;
                    }
                    sb.Append(c);
                    idx++;
                }
                return sb.ToString();
            }

            private static string ReadLiteral(string json, ref int idx)
            {
                int start = idx;
                while (idx < json.Length)
                {
                    char c = json[idx];
                    if (c == ',' || c == ']' || c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    {
                        break;
                    }
                    idx++;
                }
                string raw = json.Substring(start, idx - start).Trim();
                if (string.Equals(raw, "null", StringComparison.Ordinal))
                {
                    return string.Empty;
                }
                return raw;
            }

            private static void SkipWhitespace(string json, ref int idx)
            {
                while (idx < json.Length)
                {
                    char c = json[idx];
                    if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                    {
                        return;
                    }
                    idx++;
                }
            }
        }
    }
}
