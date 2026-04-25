using System.Collections.Generic;
using System.Text;

namespace Jinhyeong_GoogleSheetDataLoader
{
    public static class CsvParser
    {
        private const char Utf8Bom = '\uFEFF';

        public static List<List<string>> Parse(string csv)
        {
            var rows = new List<List<string>>();
            if (csv == null)
            {
                return rows;
            }
            if (csv.Length <= 0)
            {
                return rows;
            }

            var currentRow = new List<string>();
            var cell = new StringBuilder(64);
            bool inQuotes = false;

            int start = 0;
            if (csv[0] == Utf8Bom)
            {
                start = 1;
            }

            for (int i = start; i < csv.Length; i++)
            {
                char ch = csv[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                            continue;
                        }
                        inQuotes = false;
                        continue;
                    }
                    cell.Append(ch);
                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = true;
                    continue;
                }

                if (ch == ',')
                {
                    currentRow.Add(cell.ToString());
                    cell.Length = 0;
                    continue;
                }

                if (ch == '\r')
                {
                    continue;
                }

                if (ch == '\n')
                {
                    currentRow.Add(cell.ToString());
                    cell.Length = 0;
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                    continue;
                }

                cell.Append(ch);
            }

            if (cell.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(cell.ToString());
                rows.Add(currentRow);
            }

            return rows;
        }
    }
}
