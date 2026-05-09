using System.Collections.Generic;

namespace Jinhyeong_GameData
{
    public class StageContainer
        : Containers.StageDictionaryContainer
    {
        private Dictionary<int, List<StageLevel>> _resolvedLevels;

        public override bool Validate(out string errorMessage)
        {
            StageLevelContainer levels = DataManager.Instance.GetContainer<StageLevelContainer>();
            if (levels == null)
            {
                errorMessage = "StageLevelContainer가 등록되지 않았습니다";
                return false;
            }

            foreach (var kv in All)
            {
                int[] levelIds = kv.Value.StageLevel;
                if (levelIds == null)
                {
                    continue;
                }
                for (int i = 0; i < levelIds.Length; i++)
                {
                    if (levels.ContainsKey(levelIds[i]) == false)
                    {
                        errorMessage = $"Stage '{kv.Key}'가 참조하는 StageLevel '{levelIds[i]}'가 없습니다";
                        return false;
                    }
                }
            }
            errorMessage = null;
            return true;
        }

        public override void AfterAllTableLoaded()
        {
            StageLevelContainer levels = DataManager.Instance.GetContainer<StageLevelContainer>();
            if (levels == null)
            {
                return;
            }

            _resolvedLevels = new Dictionary<int, List<StageLevel>>(Count);
            foreach (var kv in All)
            {
                int[] levelIds = kv.Value.StageLevel;
                if (levelIds == null || levelIds.Length == 0)
                {
                    continue;
                }
                var list = new List<StageLevel>(levelIds.Length);
                for (int i = 0; i < levelIds.Length; i++)
                {
                    StageLevel sl = levels.Get(levelIds[i]);
                    if (sl != null)
                    {
                        list.Add(sl);
                    }
                }
                _resolvedLevels[kv.Key] = list;
            }
        }

        public IReadOnlyList<StageLevel> GetStageLevels(int stageId)
        {
            if (_resolvedLevels == null)
            {
                return null;
            }
            _resolvedLevels.TryGetValue(stageId, out List<StageLevel> list);
            return list;
        }

        public override void Clear()
        {
            base.Clear();
            _resolvedLevels = null;
        }
    }
}
