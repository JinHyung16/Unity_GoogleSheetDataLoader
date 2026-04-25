using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Jinhyeong_JsonParsing
{
    public class DataManager
    {
        public const string ResourcesSubFolder = "GoogleSheetData";

        private static DataManager _instance;

        public static DataManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DataManager();
                }
                return _instance;
            }
        }

        private readonly Dictionary<string, DataTable> _tables = new Dictionary<string, DataTable>(16);
        private bool _initialized;

        public bool IsInitialized => _initialized;
        public int TableCount => _tables.Count;

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized)
            {
                return;
            }

            _tables.Clear();

            TextAsset[] assets = Resources.LoadAll<TextAsset>(ResourcesSubFolder);
            if (assets == null)
            {
                _initialized = true;
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                TextAsset asset = assets[i];
                if (asset == null)
                {
                    continue;
                }

                SheetData sheet = null;
                try
                {
                    sheet = JsonUtility.FromJson<SheetData>(asset.text);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DataManager] JSON 파싱 실패: {asset.name} ({e.GetType().Name}) {e.Message}");
                    continue;
                }

                if (sheet == null)
                {
                    continue;
                }

                string tableName = string.IsNullOrEmpty(sheet.TableName) ? asset.name : sheet.TableName;
                _tables[tableName] = new DataTable(sheet);

                await Task.Yield();
            }

            _initialized = true;
        }

        public DataTable GetTable(string tableName)
        {
            if (tableName == null)
            {
                return null;
            }
            if (_tables.TryGetValue(tableName, out DataTable table))
            {
                return table;
            }
            return null;
        }

        public bool TryGetTable(string tableName, out DataTable table)
        {
            table = GetTable(tableName);
            return table != null;
        }

        public void Clear()
        {
            _tables.Clear();
            _initialized = false;
        }
    }
}
