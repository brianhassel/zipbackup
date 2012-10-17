using System;
using System.IO;
using System.Windows.Forms;

namespace BrianHassel.ZipBackup {

    internal class Program {

        private static int Main(string[] args) {

            if(args.Length ==0) {
                return RunBackup(false);
            } else if (args.Length == 1) {
                switch (args[0]) {
                    case "/C":
                    case "/c":
                    case "-C":
                    case "-c":
                        return UpdateOrCreateSettingsFile();

                    case "/F":
                    case "/f":
                    case "-F":
                    case "-f": 
                        return RunBackup(true);
                }
            } 
            Console.WriteLine("Option is not valid.");
            return ExitCodeFail;
        }

        private static int RunBackup(bool forceFull) {
            var configFile = GetConfigFile();
            if (!File.Exists(configFile)) {
                Console.WriteLine("Configuration file [config.xml] does not exist. Run program with /C switch to create one.");
                return ExitCodeFail;
            }
            
            var p = new BackupEngine(BackupSettings.LoadBackupSettings(configFile));
            return p.PerformBackups(forceFull) ? ExitCodeSuccess : ExitCodeFail;
        }

        private static int UpdateOrCreateSettingsFile() {
            var configFile = GetConfigFile();
            var backupSettings = File.Exists(configFile) ? BackupSettings.LoadBackupSettings(configFile) : new BackupSettings();

            var passwordSettingsDialog = new PasswordSettingsDialog();
            if(passwordSettingsDialog.ShowDialog() == DialogResult.OK) {

                if (!string.IsNullOrWhiteSpace(passwordSettingsDialog.ArchivePassword))
                    backupSettings.ArchivePassword = SecurityHelpers.EncodeSecret(passwordSettingsDialog.ArchivePassword);

                if (!string.IsNullOrWhiteSpace(passwordSettingsDialog.FTPPassword))
                    backupSettings.FTPSettings.FTPPassword = SecurityHelpers.EncodeSecret(passwordSettingsDialog.FTPPassword);
                
                if (!string.IsNullOrWhiteSpace(passwordSettingsDialog.EmailPassword))
                    backupSettings.EmailSettings.EmailPassword = SecurityHelpers.EncodeSecret(passwordSettingsDialog.EmailPassword);

                //Create a backup of the existing config file
                if(File.Exists(configFile)) {
                    File.Copy(configFile, configFile + ".bak", true);
                }

                backupSettings.SaveBackupSettings(configFile);
            }
            return ExitCodeSuccess;
        }

        private static string GetConfigFile() {
            return Path.Combine(Environment.CurrentDirectory, "config.xml");
        }

        private const int ExitCodeSuccess = 0;
        private const int ExitCodeFail = 1;
        private const int ExitCodeWarn = 2;
    }
}
