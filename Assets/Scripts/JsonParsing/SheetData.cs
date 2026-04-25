using System;
using System.Collections.Generic;

namespace Jinhyeong_JsonParsing
{
    [Serializable]
    public class SheetData
    {
        public string TableName;
        public List<string> Columns = new List<string>();
        public List<string> Types = new List<string>();
        public List<SheetRow> Rows = new List<SheetRow>();
    }
}
