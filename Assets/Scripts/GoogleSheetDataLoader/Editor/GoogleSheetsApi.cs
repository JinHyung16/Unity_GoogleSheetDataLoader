using System;
using System.Collections.Generic;
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
        private const string ExportBase = "https://docs.google.com/spreadsheets/d";
        private const int MetadataTimeoutSeconds = 30;
        private const int CsvTimeoutSeconds = 60;

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

        public static async Task<string> DownloadCsvAsync(string spreadsheetId, int gid, string accessToken)
        {
            string url = $"{ExportBase}/{spreadsheetId}/export?format=csv&gid={gid}";
            return await GetTextAsync(url, accessToken, CsvTimeoutSeconds);
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
    }
}
