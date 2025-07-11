using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace l2l_aggregator.Infrastructure.OsIntegration.Firebird
{
    public static class LinuxFirebirdPermissionFixer
    {
        public static void EnsureFirebirdDirectoryAccess()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return; // Не Linux — ничего не делаем

            string scriptPath = "/tmp/fix-firebird-perms.sh";

            string scriptContent = @"#!/bin/bash
                                    echo 'Создаём директорию /var/lib/l2l_aggregator...'
                                    mkdir -p /var/lib/l2l_aggregator

                                    echo 'Выдаём права пользователю firebird...'
                                    chown -R firebird:firebird /var/lib/l2l_aggregator
                                    chmod 755 /var/lib/l2l_aggregator

                                    echo '✅ Права успешно установлены.'";

            try
            {
                File.WriteAllText(scriptPath, scriptContent);
                Process.Start("chmod", $"+x {scriptPath}")?.WaitForExit();

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = scriptPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Console.WriteLine(output);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Console.Error.WriteLine("⚠️ Ошибка при выполнении скрипта:\n" + error);
                }
                File.Delete(scriptPath); // Удалим временный скрипт
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ Не удалось установить права: {ex.Message}");
            }
        }
    }
}
