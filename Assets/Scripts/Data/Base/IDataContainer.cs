using Jinhyeong_JsonParsing;

namespace Jinhyeong_GameData
{
    public interface IDataContainer
    {
        string Name { get; }
        bool Loaded { get; }
        void Load(DataTable table);
        void Clear();
        bool Validate(out string errorMessage);
        void AfterAllTableLoaded();
    }
}
