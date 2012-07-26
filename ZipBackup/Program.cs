using System;
using System.IO;
using BrianHassel.ZipBackup;

namespace BrianHassel.ZipBackup {

    internal class Program {

        //private static void Main() {
        //    var back = new BackupSettings();
            
        //    back.ArchivePassword = StaticHelpers.EncodeSecret("test");
        //    back.FTPSettings.FTPPassword = StaticHelpers.EncodeSecret("WmJPBUDeDe");
        //    var p = new BackupEngine(back);
        //    p.PerformBackups();
        //}

        private static int Main(string[] args) {
            var configFile = GetConfigFile();

            if(args.Length ==0) {
                if(File.Exists(configFile)) {
                    var settings = BackupSettings.LoadBackupSettings(configFile);
                    var p = new BackupEngine(settings);
                    return p.PerformBackups() ? 0 : 1;
                }else {
                    Console.WriteLine("Configuration file does not exist. Run program with /C switch to create one.");
                    return 1;
                }
            }
            if (args.Length == 1) {
                var backupSettings = File.Exists(configFile) ? BackupSettings.LoadBackupSettings(configFile) : new BackupSettings();
                UpdateSettingsFile(backupSettings);
                backupSettings.SaveBackupSettings(configFile);
                return 0;
            }
            return 1;
        }

        private static void UpdateSettingsFile(BackupSettings backupSettings) {
            Console.Write(Resource1.Program_Main_EnterArchivePassword);
            backupSettings.ArchivePassword = SecurityHelpers.EncodeSecret(Console.ReadLine());
            Console.Write(Resource1.Program_Main_EnterFTPPassword);
            backupSettings.FTPSettings.FTPPassword = SecurityHelpers.EncodeSecret(Console.ReadLine());
            Console.Write(Resource1.Program_Main_EnterEmailPassword);
            backupSettings.EmailSettings.EmailPassword = SecurityHelpers.EncodeSecret(Console.ReadLine());
        }

        private static string GetConfigFile() {
            return Path.Combine(Environment.CurrentDirectory, "config.xml");
        }
    }
}
