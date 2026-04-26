using Jinhyeong_JsonParsing;

namespace Jinhyeong_GameData
{
    public abstract class DataContainer : IDataContainer
    {
        public abstract string Name { get; }
        public abstract int Count { get; }
        public bool Loaded { get; private set; }

        public abstract void Load(DataTable table);

        protected void SetLoaded(bool loaded)
        {
            Loaded = loaded;
        }

        public virtual void Clear()
        {
            SetLoaded(false);
        }
    }

    public abstract class DataContainer<TKey, TValue>
        : DataContainer
        where TValue : class, IDataKey<TKey>, IData
    {
        protected abstract void MainCollectionConstructor(int count);
        protected abstract void MainCollectionAdd(TKey key, TValue value);
        protected abstract void OnLoadCompleted();
        protected abstract TValue Parse(DataTable table, int row);

        public override void Load(DataTable table)
        {
            if (table == null)
            {
                MainCollectionConstructor(0);
                SetLoaded(true);
                return;
            }

            int count = table.RowCount;
            MainCollectionConstructor(count);

            for (int i = 0; i < count; i++)
            {
                TValue item = Parse(table, i);
                if (item == null)
                {
                    continue;
                }
                MainCollectionAdd(item.Key, item);
            }

            SetLoaded(true);
            OnLoadCompleted();
        }
    }
}
