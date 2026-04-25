using System.Collections.Generic;
using System.IO;
using System.Text;
using Jinhyeong_GoogleSheetDataLoader;

namespace Jinhyeong_GoogleSheetDataLoader.Editor
{
    public static class EnumCodeGenerator
    {
        public const string OutputFolder = "Assets/Scripts/GeneratedEnums";
        public const string EnumNamespace = "Jinhyeong_GeneratedEnums";
        public const string AsmdefFileName = "Jinhyeong_GeneratedEnums.asmdef";
        public const string AsmdefName = "Jinhyeong_GeneratedEnums";

        public static List<string> Generate(string csv)
        {
            var generated = new List<string>();

            List<List<string>> rows = CsvParser.Parse(csv);
            if (rows == null || rows.Count == 0)
            {
                return generated;
            }

            EnsureFolderAndAsmdef();

            List<string> header = rows[0];
            int columnCount = header.Count;

            for (int c = 0; c < columnCount; c++)
            {
                string rawName = header[c] != null ? header[c].Trim() : string.Empty;
                if (string.IsNullOrEmpty(rawName))
                {
                    continue;
                }
                if (rawName.StartsWith("#"))
                {
                    continue;
                }

                string enumName = SanitizeIdentifier(rawName);
                List<string> values = CollectValues(rows, c);
                if (values.Count == 0)
                {
                    continue;
                }

                string path = WriteEnumFile(enumName, values);
                generated.Add(path);
            }

            return generated;
        }

        private static List<string> CollectValues(List<List<string>> rows, int col)
        {
            var values = new List<string>();
            var seen = new HashSet<string>();
            for (int r = 1; r < rows.Count; r++)
            {
                List<string> row = rows[r];
                if (row == null || col >= row.Count)
                {
                    continue;
                }
                string raw = row[col] != null ? row[col].Trim() : string.Empty;
                if (string.IsNullOrEmpty(raw))
                {
                    continue;
                }
                if (raw.StartsWith("#"))
                {
                    continue;
                }
                string sanitized = SanitizeIdentifier(raw);
                if (seen.Add(sanitized) == false)
                {
                    continue;
                }
                values.Add(sanitized);
            }
            return values;
        }

        private static string WriteEnumFile(string enumName, List<string> values)
        {
            var sb = new StringBuilder();
            sb.AppendLine("namespace " + EnumNamespace);
            sb.AppendLine("{");
            sb.AppendLine("    public enum " + enumName);
            sb.AppendLine("    {");
            for (int i = 0; i < values.Count; i++)
            {
                sb.Append("        ").Append(values[i]);
                if (i < values.Count - 1)
                {
                    sb.Append(",");
                }
                sb.AppendLine();
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string path = Path.Combine(OutputFolder, enumName + ".cs").Replace('\\', '/');
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        private static string SanitizeIdentifier(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return "_";
            }
            var sb = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                bool valid = (c >= 'a' && c <= 'z')
                             || (c >= 'A' && c <= 'Z')
                             || (c >= '0' && c <= '9')
                             || c == '_';
                if (valid)
                {
                    sb.Append(c);
                    continue;
                }
                sb.Append('_');
            }
            if (sb.Length > 0 && sb[0] >= '0' && sb[0] <= '9')
            {
                sb.Insert(0, '_');
            }
            return sb.ToString();
        }

        private static void EnsureFolderAndAsmdef()
        {
            if (Directory.Exists(OutputFolder) == false)
            {
                Directory.CreateDirectory(OutputFolder);
            }

            string asmdefPath = Path.Combine(OutputFolder, AsmdefFileName).Replace('\\', '/');
            if (File.Exists(asmdefPath))
            {
                return;
            }

            string content =
                "{\n" +
                "    \"name\": \"" + AsmdefName + "\",\n" +
                "    \"rootNamespace\": \"" + EnumNamespace + "\",\n" +
                "    \"references\": [],\n" +
                "    \"includePlatforms\": [],\n" +
                "    \"excludePlatforms\": [],\n" +
                "    \"allowUnsafeCode\": false,\n" +
                "    \"overrideReferences\": false,\n" +
                "    \"precompiledReferences\": [],\n" +
                "    \"autoReferenced\": true,\n" +
                "    \"defineConstraints\": [],\n" +
                "    \"versionDefines\": [],\n" +
                "    \"noEngineReferences\": true\n" +
                "}\n";
            File.WriteAllText(asmdefPath, content);
        }
    }
}
