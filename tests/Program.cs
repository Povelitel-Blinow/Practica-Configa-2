using System;
using System.IO;

namespace DependencyVisualizer.Tests
{
    /// <summary>Консольные проверки этапа 1 без сторонних библиотек.</summary>
    internal static class Program
    {
        private static int failures;
        private static int checks;

        /// <summary>Запускает проверки успешного ввода и ошибок всех параметров.</summary>
        private static int Main()
        {
            string expected = "package=bash" + Environment.NewLine
                + "repository=https://archive.ubuntu.com/ubuntu/" + Environment.NewLine
                + "test-mode=false" + Environment.NewLine + "version=1.0-1" + Environment.NewLine;
            Check(ValidArgs(), 0, expected);
            Check(new string[] { "--help" }, 0, "--package");
            Check(new string[0], 1, "--package");
            Check(new string[] { "--unknown", "value" }, 1, "Неизвестная");
            Check(new string[] { "--package" }, 1, "--package");
            Check(new string[] { "--package", "--version" }, 1, "--package");
            Check(new string[] { "--package", "bash", "--package", "curl" }, 1, "Повтор");
            CheckOptions();
            CheckValue(1, "Bad Name", "--package");
            CheckValue(1, "a", "--package");
            CheckValue(3, "ftp://example.org", "--repository");
            CheckValue(3, "relative/path", "--repository");
            CheckValue(5, "yes", "--test-mode");
            CheckValue(7, "latest", "--version");
            CheckValue(7, "1.0 bad", "--version");
            CheckValue(7, "1.0-", "--version");
            CheckTestRepository();
            Console.WriteLine("Проверок: " + checks + "; ошибок: " + failures);
            return failures == 0 ? 0 : 1;
        }

        /// <summary>Проверяет пропуск, пустое значение и повтор каждой опции.</summary>
        private static void CheckOptions()
        {
            const int PairLength = 2;
            string[] valid = ValidArgs();
            for (int i = 0; i < valid.Length; i += PairLength)
            {
                string[] missing = new string[valid.Length - PairLength];
                Array.Copy(valid, 0, missing, 0, i);
                Array.Copy(valid, i + PairLength, missing, i, missing.Length - i);
                Check(missing, 1, valid[i]);
                CheckValue(i + 1, "", valid[i]);
                CheckValue(i + 1, "  ", valid[i]);
                string[] duplicate = new string[valid.Length + PairLength];
                Array.Copy(valid, duplicate, valid.Length);
                duplicate[valid.Length] = valid[i];
                duplicate[valid.Length + 1] = valid[i + 1];
                Check(duplicate, 1, "Повтор");
            }
        }

        /// <summary>Проверяет локальный файл, путь с пробелами и отсутствующий файл.</summary>
        private static void CheckTestRepository()
        {
            string path = Path.Combine(Path.GetTempPath(), "repository " + Guid.NewGuid() + ".txt");
            try
            {
                File.WriteAllText(path, "A: B\nB:\n");
                string[] args = ValidArgs();
                args[1] = "A";
                args[3] = path;
                args[5] = "true";
                Check(args, 0, "test-mode=true");
                args[1] = "bash";
                Check(args, 1, "--package");
                args[1] = "A";
                args[3] = path + ".missing";
                Check(args, 1, "--repository");
                args[3] = Path.GetDirectoryName(path);
                Check(args, 1, "--repository");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Создаёт корректный набор параметров.</summary>
        private static string[] ValidArgs()
        {
            return new string[]
            {
                "--package", "bash", "--repository", "https://archive.ubuntu.com/ubuntu/",
                "--test-mode", "false", "--version", "1.0-1"
            };
        }

        /// <summary>Проверяет некорректное значение одной настройки.</summary>
        private static void CheckValue(int index, string value, string message)
        {
            string[] args = ValidArgs();
            args[index] = value;
            Check(args, 1, message);
        }

        /// <summary>Сравнивает код завершения, сообщение и поток вывода.</summary>
        private static void Check(string[] args, int expectedCode, string expectedText)
        {
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();
            int code = DependencyVisualizer.Program.Run(args, output, error);
            string result = expectedCode == 0 ? output.ToString() : error.ToString();
            string other = expectedCode == 0 ? error.ToString() : output.ToString();
            checks++;
            if (code != expectedCode || !result.Contains(expectedText) || other.Length != 0)
            {
                failures++;
                Console.WriteLine("FAIL: " + string.Join(" ", args));
                Console.WriteLine("Код: " + code + "; stdout: " + output + "; stderr: " + error);
            }
        }
    }
}
