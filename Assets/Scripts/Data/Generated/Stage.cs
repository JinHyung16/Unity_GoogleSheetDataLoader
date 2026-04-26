using Jinhyeong_JsonParsing;

namespace Jinhyeong_GameData
{
    public partial class Stage : IDataKey<int>, IData
    {
        public int Id { get; private set; }
        public int SpawnTileCount { get; private set; }
        public int[] StageLevel { get; private set; }

        public int Key => Id;

        public void __Parse(DataTable table, int row)
        {
            Id = table.GetInt(row, "id");
            SpawnTileCount = table.GetInt(row, "SpawnTileCount");
            StageLevel = table.GetIntArray(row, "StageLevel");
        }
    }
}
