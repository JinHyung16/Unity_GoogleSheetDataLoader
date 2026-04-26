using Jinhyeong_JsonParsing;

namespace Jinhyeong_GameData.Containers
{
    public abstract class StageLevelDictionaryContainer
        : DictionaryContainer<int, StageLevel>
    {
        public override string Name => "StageLevel";

        protected override StageLevel Parse(DataTable table, int row)
        {
            int key = table.GetInt(row, "id");
            if (key <= 0)
            {
                return null;
            }

            var data = new StageLevel();
            data.__Parse(table, row);
            return data;
        }
    }
}
