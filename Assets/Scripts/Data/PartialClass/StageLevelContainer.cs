namespace Jinhyeong_GameData
{
    public class StageLevelContainer
        : Containers.StageLevelDictionaryContainer
    {
        public override bool Validate(out string errorMessage)
        {
            foreach (var kv in All)
            {
                StageLevel sl = kv.Value;
                if (sl.StartColumn > sl.EndColumn)
                {
                    errorMessage = $"StageLevel '{kv.Key}': StartColumn({sl.StartColumn}) > EndColumn({sl.EndColumn})";
                    return false;
                }
            }
            errorMessage = null;
            return true;
        }

        public int GetColumnCount(int id)
        {
            StageLevel sl = Get(id);
            if (sl == null)
            {
                return 0;
            }
            return sl.EndColumn - sl.StartColumn + 1;
        }
    }
}
