using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jinhyeong_JsonParsing
{
    public class DataTable
    {
        private readonly SheetData _data;
        private readonly Dictionary<string, int> _columnIndexMap;

        public string Name => _data.TableName;
        public int RowCount => _data.Rows != null ? _data.Rows.Count : 0;
        public int ColumnCount => _data.Columns != null ? _data.Columns.Count : 0;

        public DataTable(SheetData data)
        {
            _data = data != null ? data : new SheetData();
            _columnIndexMap = new Dictionary<string, int>(_data.Columns.Count);
            for (int i = 0; i < _data.Columns.Count; i++)
            {
                string col = _data.Columns[i];
                if (col == null)
                {
                    continue;
                }
                if (_columnIndexMap.ContainsKey(col))
                {
                    Debug.LogWarning($"[DataTable] '{Name}' 테이블에 중복 컬럼명 '{col}' (index={i}). 첫 번째만 사용됩니다.");
                    continue;
                }
                _columnIndexMap[col] = i;
            }
        }

        public bool TryGetColumnIndex(string columnName, out int index)
        {
            if (columnName == null)
            {
                index = -1;
                return false;
            }
            return _columnIndexMap.TryGetValue(columnName, out index);
        }

        public string GetRawCell(int row, string column)
        {
            if (TryGetColumnIndex(column, out int col) == false)
            {
                return null;
            }
            return GetRawCell(row, col);
        }

        public string GetRawCell(int row, int col)
        {
            if (row < 0 || row >= RowCount)
            {
                return null;
            }
            SheetRow sheetRow = _data.Rows[row];
            if (sheetRow == null || sheetRow.Values == null)
            {
                return null;
            }
            if (col < 0 || col >= sheetRow.Values.Count)
            {
                return null;
            }
            return sheetRow.Values[col];
        }

        public int GetInt(int row, string column)
        {
            return ValueParser.ParseInt(GetRawCell(row, column));
        }

        public long GetLong(int row, string column)
        {
            return ValueParser.ParseLong(GetRawCell(row, column));
        }

        public float GetFloat(int row, string column)
        {
            return ValueParser.ParseFloat(GetRawCell(row, column));
        }

        public double GetDouble(int row, string column)
        {
            return ValueParser.ParseDouble(GetRawCell(row, column));
        }

        public bool GetBool(int row, string column)
        {
            return ValueParser.ParseBool(GetRawCell(row, column));
        }

        public string GetString(int row, string column)
        {
            return ValueParser.ParseString(GetRawCell(row, column));
        }

        public T GetEnum<T>(int row, string column) where T : struct, Enum
        {
            return ValueParser.ParseEnum<T>(GetRawCell(row, column));
        }

        public int[] GetIntArray(int row, string column)
        {
            return ValueParser.ParseIntArray(GetRawCell(row, column));
        }

        public long[] GetLongArray(int row, string column)
        {
            return ValueParser.ParseLongArray(GetRawCell(row, column));
        }

        public float[] GetFloatArray(int row, string column)
        {
            return ValueParser.ParseFloatArray(GetRawCell(row, column));
        }

        public double[] GetDoubleArray(int row, string column)
        {
            return ValueParser.ParseDoubleArray(GetRawCell(row, column));
        }

        public bool[] GetBoolArray(int row, string column)
        {
            return ValueParser.ParseBoolArray(GetRawCell(row, column));
        }

        public string[] GetStringArray(int row, string column)
        {
            return ValueParser.ParseStringArray(GetRawCell(row, column));
        }

        public T[] GetEnumArray<T>(int row, string column) where T : struct, Enum
        {
            return ValueParser.ParseEnumArray<T>(GetRawCell(row, column));
        }
    }
}
