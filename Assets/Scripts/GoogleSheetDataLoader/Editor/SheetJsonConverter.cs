using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jinhyeong_JsonParsing;
using UnityEditor;
using UnityEngine;

namespace Jinhyeong_GoogleSheetDataLoader.Editor
{
    public static class SheetJsonConverter
    {
        public const string ResourcesRoot = "Assets/Resources";
        public const string DataSubFolder = "GoogleSheetData";

        private static readonly Regex SpreadsheetIdPattern = new Regex(
            @"docs\.google\.com/spreadsheets/d/(?<id>[a-zA-Z0-9_-]{20,})",
            RegexOptions.Compiled);

        public static async Task<List<string>> SyncAllAsync(string url, string clientId, string clientSecret, string enumSheetName)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL이 비어있습니다");
            }

            string spreadsheetId = ExtractSpreadsheetId(url.Trim());
            if (string.IsNullOrEmpty(spreadsheetId))
            {
                throw new ArgumentException("URL에서 스프레드시트 ID를 추출할 수 없습니다");
            }

            string normalizedEnumSheet = enumSheetName != null ? enumSheetName.Trim() : string.Empty;
            bool hasEnumSheet = string.IsNullOrEmpty(normalizedEnumSheet) == false;

            string accessToken = await OAuth2Authenticator.EnsureAccessTokenAsync(clientId, clientSecret);

            List<SheetMeta> sheets = await GoogleSheetsApi.ListSheetsAsync(spreadsheetId, accessToken);
            if (sheets == null || sheets.Count == 0)
            {
                throw new Exception("발견된 시트가 없습니다");
            }

            EnsureFolderExists();

            var savedPaths = new List<string>();
            for (int i = 0; i < sheets.Count; i++)
            {
                SheetMeta meta = sheets[i];
                string title = meta.Title?.Trim();
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }
                if (title.StartsWith("#"))
                {
                    continue;
                }

                string csv;
                try
                {
                    csv = await GoogleSheetsApi.DownloadCsvAsync(spreadsheetId, meta.SheetId, accessToken);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GoogleSheetSync] '{title}' 다운로드 실패: {e.Message}");
                    continue;
                }

                if (hasEnumSheet && string.Equals(title, normalizedEnumSheet, StringComparison.Ordinal))
                {
                    try
                    {
                        List<string> enumPaths = EnumCodeGenerator.Generate(csv);
                        savedPaths.AddRange(enumPaths);
                        Debug.Log($"[GoogleSheetSync] '{title}' → {enumPaths.Count}개 enum 생성");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[GoogleSheetSync] '{title}' enum 처리 실패: {e.Message}");
                    }
                    continue;
                }

                SheetData data = ConvertCsvToSheetData(csv, title, out string convertError);
                if (data == null)
                {
                    Debug.LogWarning($"[GoogleSheetSync] '{title}' 변환 실패: {convertError}");
                    continue;
                }

                string savedPath = SaveJsonToResources(data);
                if (string.IsNullOrEmpty(savedPath) == false)
                {
                    savedPaths.Add(savedPath);
                }
            }

            AssetDatabase.Refresh();

            if (savedPaths.Count == 0)
            {
                throw new Exception("모든 시트 처리 실패 (콘솔 경고 확인)");
            }
            return savedPaths;
        }

        public static string ExtractSpreadsheetId(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }
            Match m = SpreadsheetIdPattern.Match(url);
            if (m.Success == false)
            {
                return null;
            }
            return m.Groups["id"].Value;
        }

        private static SheetData ConvertCsvToSheetData(string csv, string tableName, out string error)
        {
            error = null;

            List<List<string>> rows = CsvParser.Parse(csv);
            if (rows == null || rows.Count == 0)
            {
                error = "빈 CSV";
                return null;
            }
            if (rows.Count < 2)
            {
                error = "헤더와 타입 행이 필요";
                return null;
            }

            List<string> header = rows[0];
            List<string> types = rows[1];

            int columnCount = TrimTrailingEmpty(header);
            if (columnCount <= 0)
            {
                error = "헤더가 비어있습니다";
                return null;
            }

            if (types.Count < columnCount)
            {
                error = $"타입 행이 헤더보다 짧습니다 (헤더 {columnCount} / 타입 {types.Count})";
                return null;
            }

            List<int> keepIndices = SelectKeepIndices(header, columnCount);
            if (keepIndices.Count == 0)
            {
                error = "유효한 컬럼이 없습니다 (모두 비어있거나 '#' 시작)";
                return null;
            }

            var sheet = new SheetData
            {
                TableName = tableName
            };

            for (int k = 0; k < keepIndices.Count; k++)
            {
                int idx = keepIndices[k];
                sheet.Columns.Add(header[idx].Trim());
                sheet.Types.Add(types[idx].Trim());
            }

            for (int r = 2; r < rows.Count; r++)
            {
                List<string> src = rows[r];
                if (IsRowEmpty(src))
                {
                    continue;
                }

                var sheetRow = new SheetRow();
                for (int k = 0; k < keepIndices.Count; k++)
                {
                    int idx = keepIndices[k];
                    string value = idx < src.Count ? src[idx] : string.Empty;
                    sheetRow.Values.Add(value);
                }
                sheet.Rows.Add(sheetRow);
            }

            return sheet;
        }

        private static List<int> SelectKeepIndices(List<string> header, int columnCount)
        {
            var keep = new List<int>(columnCount);
            for (int i = 0; i < columnCount; i++)
            {
                string name = header[i] != null ? header[i].Trim() : string.Empty;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                if (name.StartsWith("#"))
                {
                    continue;
                }
                keep.Add(i);
            }
            return keep;
        }

        private static int TrimTrailingEmpty(List<string> header)
        {
            int count = header.Count;
            while (count > 0 && string.IsNullOrWhiteSpace(header[count - 1]))
            {
                count--;
            }
            return count;
        }

        private static bool IsRowEmpty(List<string> row)
        {
            if (row == null)
            {
                return true;
            }
            for (int i = 0; i < row.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(row[i]) == false)
                {
                    return false;
                }
            }
            return true;
        }

        private static void EnsureFolderExists()
        {
            string folder = Path.Combine(ResourcesRoot, DataSubFolder);
            if (Directory.Exists(folder) == false)
            {
                Directory.CreateDirectory(folder);
            }
        }

        private static string SaveJsonToResources(SheetData sheet)
        {
            string folder = Path.Combine(ResourcesRoot, DataSubFolder);
            string fileName = SanitizeFileName(sheet.TableName) + ".json";
            string assetPath = Path.Combine(folder, fileName).Replace('\\', '/');

            string json = JsonUtility.ToJson(sheet, true);
            File.WriteAllText(assetPath, json);
            return assetPath;
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (Array.IndexOf(invalid, c) >= 0)
                {
                    sb.Append('_');
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
