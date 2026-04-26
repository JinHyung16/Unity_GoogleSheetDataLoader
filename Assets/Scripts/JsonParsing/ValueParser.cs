using System;
using System.Globalization;

namespace Jinhyeong_JsonParsing
{
    public static class ValueParser
    {
        public const char ArrayDelimiter = ',';

        public static int ParseInt(string raw)
        {
            if (IsEmpty(raw))
            {
                return 0;
            }
            if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                return result;
            }
            return 0;
        }

        public static long ParseLong(string raw)
        {
            if (IsEmpty(raw))
            {
                return 0L;
            }
            if (long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            {
                return result;
            }
            return 0L;
        }

        public static float ParseFloat(string raw)
        {
            if (IsEmpty(raw))
            {
                return 0f;
            }
            if (float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }
            return 0f;
        }

        public static double ParseDouble(string raw)
        {
            if (IsEmpty(raw))
            {
                return 0d;
            }
            if (double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }
            return 0d;
        }

        public static bool ParseBool(string raw)
        {
            if (IsEmpty(raw))
            {
                return false;
            }
            string trimmed = raw.Trim();
            if (bool.TryParse(trimmed, out bool result))
            {
                return result;
            }
            if (string.Equals(trimmed, "1", StringComparison.Ordinal))
            {
                return true;
            }
            if (string.Equals(trimmed, "0", StringComparison.Ordinal))
            {
                return false;
            }
            if (string.Equals(trimmed, "Y", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(trimmed, "N", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(trimmed, "no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return false;
        }

        public static string ParseString(string raw)
        {
            if (raw == null)
            {
                return null;
            }
            return raw;
        }

        public static T ParseEnum<T>(string raw) where T : struct, Enum
        {
            if (IsEmpty(raw))
            {
                return GetFirstEnumValue<T>();
            }
            if (Enum.TryParse<T>(raw.Trim(), true, out T result))
            {
                return result;
            }
            return GetFirstEnumValue<T>();
        }

        public static int[] ParseIntArray(string raw)
        {
            if (IsEmpty(raw))
            {
                return Array.Empty<int>();
            }
            string[] tokens = SplitArray(raw);
            int[] result = new int[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                result[i] = ParseInt(tokens[i]);
            }
            return result;
        }

        public static long[] ParseLongArray(string raw)
        {
            if (IsEmpty(raw))
            {
                return Array.Empty<long>();
            }
            string[] tokens = SplitArray(raw);
            long[] result = new long[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                result[i] = ParseLong(tokens[i]);
            }
            return result;
        }

        public static float[] ParseFloatArray(string raw)
        {
            if (IsEmpty(raw))
            {
                return Array.Empty<float>();
            }
            string[] tokens = SplitArray(raw);
            float[] result = new float[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                result[i] = ParseFloat(tokens[i]);
            }
            return result;
        }

        public static double[] ParseDoubleArray(string raw)
        {
            if (IsEmpty(raw))
            {
                return Array.Empty<double>();
            }
            string[] tokens = SplitArray(raw);
            double[] result = new double[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                result[i] = ParseDouble(tokens[i]);
            }
            return result;
        }

        public static bool[] ParseBoolArray(string raw)
        {
            if (IsEmpty(raw))
            {
                return Array.Empty<bool>();
            }
            string[] tokens = SplitArray(raw);
            bool[] result = new bool[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                result[i] = ParseBool(tokens[i]);
            }
            return result;
        }

        public static string[] ParseStringArray(string raw)
        {
            if (IsEmpty(raw))
            {
                return Array.Empty<string>();
            }
            return SplitArray(raw);
        }

        public static T[] ParseEnumArray<T>(string raw) where T : struct, Enum
        {
            if (IsEmpty(raw))
            {
                return Array.Empty<T>();
            }
            string[] tokens = SplitArray(raw);
            T[] result = new T[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                result[i] = ParseEnum<T>(tokens[i]);
            }
            return result;
        }

        private static bool IsEmpty(string raw)
        {
            if (raw == null)
            {
                return true;
            }
            if (raw.Length == 0)
            {
                return true;
            }
            return string.IsNullOrWhiteSpace(raw);
        }

        private static string[] SplitArray(string raw)
        {
            return raw.Split(ArrayDelimiter);
        }

        private static T GetFirstEnumValue<T>() where T : struct, Enum
        {
            Array values = Enum.GetValues(typeof(T));
            if (values == null)
            {
                return default;
            }
            if (values.Length <= 0)
            {
                return default;
            }
            object first = values.GetValue(0);
            if (first == null)
            {
                return default;
            }
            return (T)first;
        }
    }
}
