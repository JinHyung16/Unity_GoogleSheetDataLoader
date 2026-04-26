using Jinhyeong_JsonParsing;

namespace Jinhyeong_GameData
{
    public partial class StageLevel : IDataKey<int>, IData
    {
        public int Id { get; private set; }
        public int StartColumn { get; private set; }
        public int EndColumn { get; private set; }

        public int Key => Id;

        public void __Parse(DataTable table, int row)
        {
            Id = table.GetInt(row, "id");
            StartColumn = table.GetInt(row, "StartColumn");
            EndColumn = table.GetInt(row, "EndColumn");
        }
    }
}
