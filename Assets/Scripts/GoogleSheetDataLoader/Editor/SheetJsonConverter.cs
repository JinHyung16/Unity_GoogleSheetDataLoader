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

        public static async Task<SyncResult> SyncAllAsync(
            string url,
            string clientId,
            string clientSecret,
            IEnumerable<string> enumSheetNames = null,
            string enumOutputFileName = null)
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

            string accessToken = await OAuth2Authenticator.EnsureAccessTokenAsync(clientId, clientSecret);

            List<SheetMeta> sheets = await GoogleSheetsApi.ListSheetsAsync(spreadsheetId, accessToken);
            if (sheets == null || sheets.Count == 0)
            {
                throw new Exception("발견된 시트가 없습니다");
            }

            EnsureFolderExists();

            var enumSheetSet = BuildEnumSheetSet(enumSheetNames);
            var collectedEnums = new List<EnumDef>();
            var savedPaths = new List<string>();
            var generatedCodePaths = new List<string>();
            var liveTableNames = new HashSet<string>(StringComparer.Ordinal);
            var liveDataClassNames = new HashSet<string>(StringComparer.Ordinal);

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

                List<List<string>> rows;
                try
                {
                    rows = await GoogleSheetsApi.GetValuesAsync(spreadsheetId, title, accessToken);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GoogleSheetSync] '{title}' 다운로드 실패: {e.Message}");
                    continue;
                }

                if (enumSheetSet.Contains(title))
                {
                    collectedEnums.AddRange(EnumCodeGenerator.CollectFromRows(rows));
                    continue;
                }

                SheetData data = ConvertRowsToSheetData(rows, title, out string convertError);
                if (data == null)
                {
                    Debug.LogWarning($"[GoogleSheetSync] '{title}' 변환 실패: {convertError}");
                    continue;
                }

                string savedPath = SaveJsonToResources(data);
                if (string.IsNullOrEmpty(savedPath) == false)
                {
                    savedPaths.Add(savedPath);
                    liveTableNames.Add(SanitizeFileName(data.TableName));

                    List<string> codePaths = DataContainerCodeGenerator.Generate(data);
                    if (codePaths != null)
                    {
                        generatedCodePaths.AddRange(codePaths);
                    }
                    liveDataClassNames.Add(DataContainerCodeGenerator.GetDataClassName(data.TableName));
                }
            }

            string enumPath = EnumCodeGenerator.WriteCombinedFile(
                collectedEnums,
                enumOutputFileName);

            List<string> deletedPaths = DeleteStaleJsonFiles(liveTableNames);
            List<string> deletedCodePaths = DataContainerCodeGenerator.DeleteStaleFiles(liveDataClassNames);

            AssetDatabase.Refresh();

            if (savedPaths.Count == 0 && deletedPaths.Count == 0 && string.IsNullOrEmpty(enumPath))
            {
                throw new Exception("모든 시트 처리 실패 (콘솔 경고 확인)");
            }
            return new SyncResult
            {
                SavedPaths = savedPaths,
                DeletedPaths = deletedPaths,
                GeneratedCodePaths = generatedCodePaths,
                DeletedCodePaths = deletedCodePaths,
                EnumOutputPath = enumPath,
                EnumCount = collectedEnums.Count,
            };
        }

        private static HashSet<string> BuildEnumSheetSet(IEnumerable<string> names)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (names == null)
            {
                return set;
            }
            foreach (string raw in names)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }
                set.Add(raw.Trim());
            }
            return set;
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

        /// <summary>
        /// Removes JSON files in <c>Resources/GoogleSheetData/</c> whose base name
        /// is not in <paramref name="liveTableNames"/>. Their .meta sidecars are
        /// removed too. This keeps the local cache in sync when a sheet is
        /// renamed/deleted in Google Sheets.
        /// </summary>
        private static List<string> DeleteStaleJsonFiles(HashSet<string> liveTableNames)
        {
            var deleted = new List<string>();
            string folder = Path.Combine(ResourcesRoot, DataSubFolder);
            if (Directory.Exists(folder) == false)
            {
                return deleted;
            }
            foreach (string jsonPath in Directory.GetFiles(folder, "*.json"))
            {
                string baseName = Path.GetFileNameWithoutExtension(jsonPath);
                if (string.IsNullOrEmpty(baseName)) continue;
                if (liveTableNames.Contains(baseName)) continue;

                try
                {
                    File.Delete(jsonPath);
                    string metaPath = jsonPath + ".meta";
                    if (File.Exists(metaPath)) File.Delete(metaPath);
                    deleted.Add(jsonPath.Replace('\\', '/'));
                    Debug.Log($"[GoogleSheetSync] 시트에서 사라진 '{baseName}' → 로컬 JSON 삭제");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GoogleSheetSync] '{baseName}' 삭제 실패: {e.Message}");
                }
            }
            return deleted;
        }

        private static SheetData ConvertRowsToSheetData(List<List<string>> rows, string tableName, out string error)
        {
            error = null;

            if (rows == null || rows.Count == 0)
            {
                error = "빈 시트";
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

    public class SyncResult
    {
        public List<string> SavedPaths = new List<string>();
        public List<string> DeletedPaths = new List<string>();
        public List<string> GeneratedCodePaths = new List<string>();
        public List<string> DeletedCodePaths = new List<string>();
        public string EnumOutputPath;
        public int EnumCount;
    }
}
