using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace BrianHassel.ZipBackup {
    internal sealed class BackupEngine{

        internal BackupEngine(BackupSettings backupSettings) {
            log = new SimpleLog(true);
            this.backupSettings = backupSettings;
        }
       
        public bool PerformBackups() {
            bool res;
            try{
                foreach (var backupJob in backupSettings.BackupJobs) {
                    log.Info("\n####" + backupJob.BackupName + "####");

                    DetermineBackupMode(backupJob);
                    CleanupLocalBackups(backupJob);
                    BuildArchive(backupJob);
                }
                SyncFTPBackups();
                res = true;
            }catch(Exception e) {
                log.Error("Unhandled " + e);
                res= false;
            }

            try {
                StaticHelpers.SendEmail(backupSettings.EmailSettings, res ? "Backup successful" : "Backup Failed", log.GetEmailText());
            } catch (Exception e) {
                log.Error("Unhandled " + e);
            }
            return res;
        }

        private void DetermineBackupMode(BackupJob backupJob) {
            //Set the 7z container files for the Full/Inc. Figure out which backup to perform.
            var matchingFiles = Directory.GetFiles(backupSettings.LocalBackupFolder, string.Format("F-{0}*.7z", backupJob.BackupName));

            if (matchingFiles.Length == 1) {
                backupJob.FullBackupFile = new FileInfo(matchingFiles[0]);

                var fullBackupAgeDays = (DateTime.UtcNow - backupJob.FullBackupFile.CreationTimeUtc).TotalDays;
                
                if(fullBackupAgeDays > backupJob.MaxFullBackupAgeDays) {
                    log.Info(string.Format("Full backup is {0} days old. Performing FULL backup.", fullBackupAgeDays.ToString("0.00")));
                    backupJob.FullBackupFile =
                        new FileInfo(Path.Combine(backupSettings.LocalBackupFolder, string.Format("F-{0}-{1}.7z", backupJob.BackupName, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"))));
                    backupJob.IncrementalFile = null;
                }else {
                    log.Info(string.Format("Full backup is {0} days old. Performing INCREMENTAL backup.", fullBackupAgeDays.ToString("0.00")));
                    backupJob.IncrementalFile = new FileInfo(Path.Combine(backupSettings.LocalBackupFolder, string.Format("I-{0}-{1}.7z", backupJob.BackupName, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"))));
                }
            } else {
                log.Info("No full backup file found. Performing FULL backup.");
                backupJob.FullBackupFile =
                    new FileInfo(Path.Combine(backupSettings.LocalBackupFolder, string.Format("F-{0}-{1}.7z", backupJob.BackupName, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"))));
                backupJob.IncrementalFile = null;
            }
        }

        private void CleanupLocalBackups(BackupJob backupJob) {
            if (backupJob.IsFullBackup) {
                foreach(var fullBak in Directory.GetFiles(backupSettings.LocalBackupFolder, string.Format("F-{0}*.7z", backupJob.BackupName)))
                    File.Delete(fullBak);
                RemoveOldLocalIncrementalBackups(backupJob, 0);
            } else {
                RemoveOldLocalIncrementalBackups(backupJob, backupJob.NumberIncremental);
            }
        }

        private void RemoveOldLocalIncrementalBackups(BackupJob backupJob, int numKeep) {
            var existingFiles = new List<string>(Directory.GetFiles(backupSettings.LocalBackupFolder, string.Format("I-{0}-*.7z", backupJob.BackupName)));

            var filesToDelete = existingFiles.OrderByDescending(f => f.ToLower()).Skip(numKeep);
            log.Info("Found: " + existingFiles.Count + " existing local incremental files. Will keep:" + numKeep);

            foreach (string fileToDelete in filesToDelete) {
                log.Info("Deleting local: " + fileToDelete);
                File.Delete(fileToDelete);
            }
        }

        private void BuildArchive(BackupJob backupJob) {
            var r = new Random();
            string listFile = "l" +r.Next(0, 100000)+  ".tmp";
            
            File.WriteAllLines(listFile, backupJob.BackupLocations);

            Run7Zip(backupJob, listFile);

            File.Delete(listFile);
            if (backupJob.IsFullBackup) {
                backupJob.FullBackupFile.Refresh();
                log.Info("Archive file created. Size: " + StaticHelpers.FormatFileSize(backupJob.FullBackupFile.Length));
            }
            else {
                backupJob.IncrementalFile.Refresh();
                log.Info("Archive file created. Size: " + StaticHelpers.FormatFileSize(backupJob.IncrementalFile.Length));
            }
        }

        private void Run7Zip(BackupJob backupJob, string listFile) {
            var p = new ProcessStartInfo {
                                             FileName = backupSettings.SevenZipExecutablePath,
                                             UseShellExecute = false,
                                             Arguments = BuildZipArguments(backupJob, listFile),
                                             RedirectStandardOutput = true
                                         };

            var x = Process.Start(p);
            string output = x.StandardOutput.ReadToEnd();

            x.WaitForExit(1000 * 60 * 30);
            log.Info(output);
            if (x.ExitCode != 0)
                throw new ApplicationException("Archive failed. Exit Code: " + x.ExitCode);
        }

        private string BuildZipArguments(BackupJob backupJob, string listFile) {
            const string archiveType = "7z";
            
            var arguments = new StringBuilder(100);

            arguments.Append(backupJob.IsFullBackup ? "a" : "u");

            arguments.AppendFormat(" \"{0}\"", backupJob.FullBackupFile.FullName);

            arguments.AppendFormat(" -t{0}", archiveType);
            arguments.AppendFormat(" -mx={0}", backupSettings.CompressionLevel);
            arguments.AppendFormat(" -mhe={0}", backupSettings.EncryptHeaders ? "on" : "off");

            //Is the archive encrypted?
            var archivePassword = SecurityHelpers.DecodeSecret(backupSettings.ArchivePassword);
            if (!string.IsNullOrEmpty(archivePassword))
                arguments.AppendFormat(" -p{0}", archivePassword);

            if (!backupJob.IsFullBackup) {
                arguments.Append(" -ms=off");
                arguments.Append(" -u- -up0q3r2x2y2z0w2");
                arguments.AppendFormat("!\"{0}\"", backupJob.IncrementalFile.FullName);
            }

            arguments.AppendFormat(" @{0}", listFile);
            return arguments.ToString();
        }

        private void SyncFTPBackups() {
            var ftpSettings = backupSettings.FTPSettings;
            log.Info("Starting FTP: " + ftpSettings.FTPServerAddress);

            var ftpPassword = SecurityHelpers.DecodeSecret(ftpSettings.FTPPassword);
            var ftpClient = new FTPClient(ftpSettings.FTPServerAddress, ftpSettings.FTPUser, ftpPassword, port: ftpSettings.FTPPort, useSSL: ftpSettings.FTPUseSSL);
            
            ftpClient.RemoteFolder = ftpSettings.FTPFolder;

            DirectoryInfo localBackupDirectory = new DirectoryInfo(backupSettings.LocalBackupFolder);
            List<FileInfo> localBackupFiles = new List<FileInfo>();
            localBackupFiles.AddRange(localBackupDirectory.GetFiles("I-*.7z"));
            localBackupFiles.AddRange(localBackupDirectory.GetFiles("F-*.7z"));


            var remoteBackupFileNames = ftpClient.GetList().Where(rbf =>
                                                              rbf.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) &&
                                                              (rbf.StartsWith("I-", StringComparison.OrdinalIgnoreCase) || rbf.StartsWith("F-", StringComparison.OrdinalIgnoreCase))
                ).ToList();

            foreach(string remoteBackupFileName in remoteBackupFileNames) {
                bool existsLocal = localBackupFiles.Exists(lbf => string.Equals(lbf.Name, remoteBackupFileName, StringComparison.OrdinalIgnoreCase));
                if(!existsLocal) {
                    log.Info("Deleting remote: " + remoteBackupFileName);
                    if (!ftpClient.DeleteFile(remoteBackupFileName)) {
                        throw new ApplicationException("Delete failed. " + ftpClient.LastStatusDescription);
                    }
                }
            }

            foreach(var localBackupFile in localBackupFiles) {
                string remoteFileName = remoteBackupFileNames.Find(rbfn => string.Equals(rbfn, localBackupFile.Name, StringComparison.OrdinalIgnoreCase));
                bool shouldUpload = false;

                if (string.IsNullOrEmpty(remoteFileName)) {
                    shouldUpload = true;
                    log.Info(string.Format("File: {0} ({1}) does not exist on FTP site. Starting upload.", localBackupFile.Name,
                                           StaticHelpers.FormatFileSize(localBackupFile.Length)));
                }
                else {
                    //Check the sizes
                    if (ftpSettings.FTPVerifySizes) {
                        long? ftpFileSize = ftpClient.GetFileSize(remoteFileName);
                        shouldUpload = localBackupFile.Length != ftpFileSize;
                        if (shouldUpload)
                            log.Info(string.Format("File: {0} ({1}) size does not match file size on FTP site ({2}).", localBackupFile.Name,
                                                   StaticHelpers.FormatFileSize(localBackupFile.Length), StaticHelpers.FormatFileSize(ftpFileSize)));
                    }
                }

                if (shouldUpload) {
                    Stopwatch sw = Stopwatch.StartNew();
                    if (!ftpClient.UploadFile(localBackupFile)) {
                        throw new ApplicationException("Upload failed. " + ftpClient.LastStatusDescription);
                    }
                    sw.Stop();
                    double bytesSec = localBackupFile.Length/sw.Elapsed.TotalSeconds;
                    log.Info(string.Format("Upload of:{0} ({1}) completed in:{2} ({3} / second)", localBackupFile.Name, StaticHelpers.FormatFileSize(localBackupFile.Length),
                                           sw.Elapsed, StaticHelpers.FormatFileSize(bytesSec)));
                }
            }
        }

        private readonly BackupSettings backupSettings;
        private readonly SimpleLog log;
    }
}