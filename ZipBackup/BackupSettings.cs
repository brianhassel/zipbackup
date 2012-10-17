using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using NLog;


namespace BrianHassel.ZipBackup {
    
    [DataContract(Name = "Settings", Namespace = "AutomatedBackup")]
    public class BackupSettings {

        public BackupSettings() {
            SevenZipExecutablePath = @"7za.exe";
            CompressionLevel = 9;
            EncryptHeaders = false;
            LocalBackupFolder = @"D:\Bak\";

            BackupJobs = new List<BackupJob> {
                                                 new BackupJob {
                                                                   BackupName = "Logs",
                                                                   BackupLocations = new List<string> {@"C:\temp\logs"},
                                                                   MaxFullBackupAgeDays = 20,
                                                                NumberIncremental = 3,
                                                               },
                                                                new BackupJob {
                                                                   BackupName = "VIZ",
                                                                   BackupLocations = new List<string> {@"C:\temp\viz"},
                                                                   MaxFullBackupAgeDays = 20,
                                                                NumberIncremental = 3,
                                                               }
                                             };

            EmailSettings = new EmailSettings {EmailServerAddress = "smtp.gmail.com", EmailPort = 587, EmailTo = "backup@gmail.com", EmailUser = "donotreply@gmail.com"};
            FTPSettings = new FTPSettings { FTPServerAddress = "ftp.com", FTPPort = 21, FTPUser = "ftpuser", FTPVerifySizes = true};
        }

        [DataMember]
        public string SevenZipExecutablePath { get; set; }
        
        [DataMember]
        public int CompressionLevel { get; set; }
        
        [DataMember]
        public bool EncryptHeaders { get; set; }

        [DataMember]
        public string ArchivePassword { get; set; }

        [DataMember]
        public string LocalBackupFolder { get; set; }

        [DataMember]
        public List<BackupJob> BackupJobs { get; set; }

        [DataMember]
        public bool SyncWithFTP { get; set; }

        [DataMember]
        public FTPSettings FTPSettings { get; set; }

        [DataMember]
        public bool SendEmail { get; set; }

        [DataMember]
        public bool SendEmailOnSuccess { get; set; }

        [DataMember]
        public EmailSettings EmailSettings { get; set; }

       
        internal static BackupSettings LoadBackupSettings(string fileName) {
            try {
                return SerializationHelpers.DeserializeObjectFromFile<BackupSettings>(new FileInfo(fileName), false);
            }catch(Exception e) {
                log.ErrorException("Could not load settings from: " + fileName, e);
                return null;
            }
        }

        internal void SaveBackupSettings(string fileName) {
            try {
                SerializationHelpers.SerializeObjectToFile(this, new FileInfo(fileName), false, true);
            } catch (Exception e) {
                log.ErrorException("Could not save settings to: " + fileName, e);
            }
        }

        private static readonly Logger log = LogManager.GetCurrentClassLogger();
    }

    [DataContract]
    public class FTPSettings {
        [DataMember]
        public string FTPServerAddress { get; set; }

        [DataMember]
        public int FTPPort { get; set; }

        [DataMember]
        public bool FTPUseSSL { get; set; }

        [DataMember]
        public string FTPUser { get; set; }

        [DataMember]
        public string FTPPassword { get; set; }

        [DataMember]
        public bool FTPVerifySizes { get; set; }

        [DataMember]
        public int RetryAttempts { get; set; }

        [DataMember]
        public int RetryDelaySeconds { get; set; }
    }

    [DataContract]
    public class EmailSettings {
        
        [DataMember]
        public string EmailServerAddress { get; set; }

        [DataMember]
        public int EmailPort { get; set; }

        [DataMember]
        public bool EmailUseSSL { get; set; }

        [DataMember]
        public string EmailUser { get; set; }

        [DataMember]
        public string EmailPassword { get; set; }

        [DataMember]
        public string EmailTo { get; set; }
    }

    [DataContract]
    public class BackupJob {

        public BackupJob() {
            BackupLocations = new List<string>();
        }

        [DataMember]
        public string BackupName { get; set; }

        [DataMember]
        public List<string> BackupLocations { get; set; }

        [DataMember]
        public double MaxFullBackupAgeDays { get; set; }

        [DataMember]
        public int NumberIncremental { get; set; }

        [IgnoreDataMember]
        public bool IsFullBackup { 
            get { return IncrementalFile == null; }
        }
        
        [IgnoreDataMember]
        public FileInfo FullBackupFile { get; set; }
        [IgnoreDataMember]
        public FileInfo IncrementalFile { get; set; }
    }
}