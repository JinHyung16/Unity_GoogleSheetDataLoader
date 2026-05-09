using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jinhyeong_JsonParsing;
using UnityEngine;

namespace Jinhyeong_GameData
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
        private readonly Dictionary<Type, IDataContainer> _containers = new Dictionary<Type, IDataContainer>(16);
        private bool _initialized;

        public bool IsInitialized => _initialized;
        public int TableCount => _tables.Count;
        public int ContainerCount => _containers.Count;

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized)
            {
                return;
            }

            DiscoverContainers();
            _tables.Clear();

            TextAsset[] assets = Resources.LoadAll<TextAsset>(ResourcesSubFolder);
            if (assets == null)
            {
                LoadAllContainersFromTables();
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

            LoadAllContainersFromTables();
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

        public T GetContainer<T>() where T : class, IDataContainer
        {
            if (_containers.TryGetValue(typeof(T), out IDataContainer container))
            {
                return container as T;
            }
            return null;
        }

        public void Clear()
        {
            foreach (KeyValuePair<Type, IDataContainer> kv in _containers)
            {
                if (kv.Value == null)
                {
                    continue;
                }
                kv.Value.Clear();
            }
            _tables.Clear();
            _initialized = false;
        }

        private void DiscoverContainers()
        {
            _containers.Clear();

            Type baseType = typeof(DataContainer);
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int a = 0; a < assemblies.Length; a++)
            {
                Type[] types = TryGetTypes(assemblies[a]);
                if (types == null)
                {
                    continue;
                }

                for (int i = 0; i < types.Length; i++)
                {
                    Type t = types[i];
                    if (t == null)
                    {
                        continue;
                    }
                    if (t.IsAbstract)
                    {
                        continue;
                    }
                    if (baseType.IsAssignableFrom(t) == false)
                    {
                        continue;
                    }
                    if (_containers.ContainsKey(t))
                    {
                        continue;
                    }

                    IDataContainer instance = TryCreateContainer(t);
                    if (instance == null)
                    {
                        continue;
                    }
                    _containers[t] = instance;
                }
            }
        }

        private void LoadAllContainersFromTables()
        {
            foreach (KeyValuePair<Type, IDataContainer> kv in _containers)
            {
                IDataContainer container = kv.Value;
                if (container == null)
                {
                    continue;
                }

                if (TryGetTable(container.Name, out DataTable table) == false)
                {
                    Debug.LogWarning($"[DataManager] '{container.Name}' 테이블이 없습니다 ({kv.Key.Name})");
                    container.Clear();
                    continue;
                }

                container.Load(table);
            }

            foreach (KeyValuePair<Type, IDataContainer> kv in _containers)
            {
                IDataContainer container = kv.Value;
                if (container == null || container.Loaded == false)
                {
                    continue;
                }
                if (container.Validate(out string errorMessage) == false)
                {
                    Debug.LogError($"[DataManager] '{container.Name}' 검증 실패: {errorMessage}");
                }
            }

            foreach (KeyValuePair<Type, IDataContainer> kv in _containers)
            {
                IDataContainer container = kv.Value;
                if (container == null || container.Loaded == false)
                {
                    continue;
                }
                container.AfterAllTableLoaded();
            }
        }

        private static Type[] TryGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DataManager] {assembly.FullName} 타입 로드 실패: {e.Message}");
                return null;
            }
        }

        private static IDataContainer TryCreateContainer(Type t)
        {
            try
            {
                return (IDataContainer)Activator.CreateInstance(t);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DataManager] {t.Name} 생성 실패: {e.Message}");
                return null;
            }
        }
    }
}
