using System.Collections.Generic;

namespace Jinhyeong_GameData
{
    public abstract class DictionaryContainer<TKey, TValue>
        : DataContainer<TKey, TValue>
        where TValue : class, IDataKey<TKey>, IData
    {
        protected Dictionary<TKey, TValue> _dict;

        public override int Count
        {
            get
            {
                if (_dict == null)
                {
                    return 0;
                }
                return _dict.Count;
            }
        }

        public IReadOnlyDictionary<TKey, TValue> All
        {
            get
            {
                return _dict;
            }
        }

        public virtual TValue Get(TKey key)
        {
            if (_dict == null)
            {
                return null;
            }
            _dict.TryGetValue(key, out TValue value);
            return value;
        }

        public bool TryGet(TKey key, out TValue value)
        {
            if (_dict == null)
            {
                value = null;
                return false;
            }
            return _dict.TryGetValue(key, out value);
        }

        public bool ContainsKey(TKey key)
        {
            if (_dict == null)
            {
                return false;
            }
            return _dict.ContainsKey(key);
        }

        protected override void MainCollectionConstructor(int count)
        {
            _dict = new Dictionary<TKey, TValue>(count);
        }

        protected override void MainCollectionAdd(TKey key, TValue value)
        {
            _dict.Add(key, value);
        }

        protected override void OnLoadCompleted()
        {
        }

        public override void Clear()
        {
            base.Clear();
            if (_dict != null)
            {
                _dict.Clear();
            }
        }
    }
}
