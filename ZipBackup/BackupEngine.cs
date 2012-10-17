using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NLog;

namespace BrianHassel.ZipBackup {
    internal sealed class BackupEngine{

        internal BackupEngine(BackupSettings backupSettings) {
            this.backupSettings = backupSettings;
        }

        public bool PerformBackups(bool forceFull) {
            bool result;
            
            //Make sure the computer does not enter a sleep state while we are busy.
            NativeMethods.PreventSleep();

            try {
                foreach (var backupJob in backupSettings.BackupJobs) {
                    log.Info("");
                    log.Info("####{0}####", backupJob.BackupName);

                    DetermineBackupMode(backupJob, forceFull);
                    CleanupLocalBackups(backupJob);
                    BuildArchive(backupJob);
                }
                if (backupSettings.SyncWithFTP)
                    SyncFTPBackups();

                result = true;
            }
            catch (Exception e) {
                log.FatalException("Unhandled", e);
                result = false;
            }


            if (backupSettings.SendEmail) {
                if (!result || backupSettings.SendEmailOnSuccess) { //send email on failure or always if option specified
                    string text = string.Format("Backup of {0} {1}.", Environment.MachineName, result ? "was successful." : "FAILED");
                    if (!StaticHelpers.SendEmail(backupSettings.EmailSettings, text, text))
                        result = false;
                }
            }

            //Return the system to the proper sleep state.
            NativeMethods.AllowSleep();

            return result;
        }

        private void DetermineBackupMode(BackupJob backupJob, bool forceFull) {
            if (forceFull) {
                log.Info("Performing FULL backup (requested).");
                backupJob.FullBackupFile =
                    new FileInfo(Path.Combine(backupSettings.LocalBackupFolder, string.Format("F-{0}-{1}.7z", backupJob.BackupName, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"))));
                backupJob.IncrementalFile = null;
                return;
            }

            //Set the 7z container files for the Full/Inc. Figure out which backup to perform.
            var matchingFiles = Directory.GetFiles(backupSettings.LocalBackupFolder, string.Format("F-{0}*.7z", backupJob.BackupName));
            //No full backups
            if (matchingFiles.Length != 1) {
                log.Info("No full backup file found. Performing FULL backup.");
                backupJob.FullBackupFile =
                    new FileInfo(Path.Combine(backupSettings.LocalBackupFolder, string.Format("F-{0}-{1}.7z", backupJob.BackupName, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"))));
                backupJob.IncrementalFile = null;
                return;
            }

            //There is one full backup, figure if it needs to be redone
            backupJob.FullBackupFile = new FileInfo(matchingFiles[0]);

            var fullBackupAgeDays = (DateTime.UtcNow - backupJob.FullBackupFile.CreationTimeUtc).TotalDays;

            if (fullBackupAgeDays > backupJob.MaxFullBackupAgeDays) {
                log.Info("Full backup is {0} days old. Performing FULL backup.", fullBackupAgeDays.ToString("0.00"));
                backupJob.FullBackupFile =
                    new FileInfo(Path.Combine(backupSettings.LocalBackupFolder, string.Format("F-{0}-{1}.7z", backupJob.BackupName, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"))));
                backupJob.IncrementalFile = null;
            }
            else {
                //Test the full backup file to make sure it isn't corrupt
                bool isFullBackupOK = TestFullBackupFile(backupJob);

                if (isFullBackupOK) {
                    log.Info("Full backup is {0} days old and passed validity check. Performing INCREMENTAL backup.", fullBackupAgeDays.ToString("0.00"));
                    backupJob.IncrementalFile =
                        new FileInfo(Path.Combine(backupSettings.LocalBackupFolder,
                                                  string.Format("I-{0}-{1}.7z", backupJob.BackupName, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"))));
                }else {
                    log.Info("Full backup failed validity check. Performing FULL backup.");
                    backupJob.FullBackupFile =
                        new FileInfo(Path.Combine(backupSettings.LocalBackupFolder, string.Format("F-{0}-{1}.7z", backupJob.BackupName, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"))));
                    backupJob.IncrementalFile = null;
                }
            }
        }

       

        private void CleanupLocalBackups(BackupJob backupJob) {
            if (backupJob.IsFullBackup) {
                foreach (var fullBak in Directory.GetFiles(backupSettings.LocalBackupFolder, string.Format("F-{0}*.7z", backupJob.BackupName)))
                    File.Delete(fullBak);
                RemoveOldLocalIncrementalBackups(backupJob, 0);
            }
            else {
                //Only keep 1 less than specified in config - the backup that is about to run is that extra
                RemoveOldLocalIncrementalBackups(backupJob, Math.Max(backupJob.NumberIncremental - 1, 0));
            }
        }

        private void RemoveOldLocalIncrementalBackups(BackupJob backupJob, int numKeep) {
            var existingFiles = new List<string>(Directory.GetFiles(backupSettings.LocalBackupFolder, string.Format("I-{0}-*.7z", backupJob.BackupName)));

            var filesToDelete = existingFiles.OrderByDescending(f => f.ToLower()).Skip(numKeep);
            log.Info("Found: {0} existing local incremental files. Will keep: {1}", existingFiles.Count, numKeep);

            foreach (string fileToDelete in filesToDelete) {
                log.Info("Deleting local: {0}", fileToDelete);
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
                log.Info("Full archive file created. Size: {0}", StaticHelpers.FormatFileSize(backupJob.FullBackupFile.Length));
            }
            else {
                backupJob.IncrementalFile.Refresh();
                log.Info("Incremental archive file created. Size: {0}", StaticHelpers.FormatFileSize(backupJob.IncrementalFile.Length));
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
            log.Debug(output);

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

        private bool TestFullBackupFile(BackupJob backupJob) {
            string args = "t \"" + backupJob.FullBackupFile.FullName + "\"";
            var archivePassword = SecurityHelpers.DecodeSecret(backupSettings.ArchivePassword);
            if (!string.IsNullOrEmpty(archivePassword))
                args += " -p" + archivePassword;

            var p = new ProcessStartInfo {
                                             FileName = backupSettings.SevenZipExecutablePath,
                                             UseShellExecute = false,
                                             Arguments = args,
                                             RedirectStandardOutput = true
                                         };
            var x = Process.Start(p);
            string output = x.StandardOutput.ReadToEnd();

            x.WaitForExit(1000*60*30);
            log.Debug(output);

            return x.ExitCode == 0;
        }

        private void SyncFTPBackups() {
            var ftpSettings = backupSettings.FTPSettings;
            log.Info("Starting FTP: {0}", ftpSettings.FTPServerAddress);

            var ftpPassword = SecurityHelpers.DecodeSecret(ftpSettings.FTPPassword);

            var ftpClient = new FTPClient(ftpSettings.FTPServerAddress, ftpSettings.FTPUser, ftpPassword, port: ftpSettings.FTPPort, useSSL: ftpSettings.FTPUseSSL,
                                          attempts: ftpSettings.RetryAttempts, attemptWaitSeconds: ftpSettings.RetryDelaySeconds);
            
            var localBackupDirectory = new DirectoryInfo(backupSettings.LocalBackupFolder);
            var localBackupFiles = new List<FileInfo>();
            localBackupFiles.AddRange(localBackupDirectory.GetFiles("I-*.7z"));
            localBackupFiles.AddRange(localBackupDirectory.GetFiles("F-*.7z"));

            var list = ftpClient.GetList();
            if(list == null)
                throw new ApplicationException("Could not retrieve list from FTP site.");

            var remoteBackupFileNames = list.Where(rbf =>
                                                              rbf.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) &&
                                                              (rbf.StartsWith("I-", StringComparison.OrdinalIgnoreCase) || rbf.StartsWith("F-", StringComparison.OrdinalIgnoreCase))
                ).ToList();

            foreach(string remoteBackupFileName in remoteBackupFileNames) {
                bool existsLocal = localBackupFiles.Exists(lbf => string.Equals(lbf.Name, remoteBackupFileName, StringComparison.OrdinalIgnoreCase));
                if(!existsLocal) {
                    log.Info("Deleting remote: {0}", remoteBackupFileName);
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
                    log.Info("File: {0} ({1}) does not exist on FTP site. Starting upload.", localBackupFile.Name, StaticHelpers.FormatFileSize(localBackupFile.Length));
                }
                else {
                    //Check the sizes
                    if (ftpSettings.FTPVerifySizes) {
                        long? ftpFileSize = ftpClient.GetFileSize(remoteFileName);
                        shouldUpload = localBackupFile.Length != ftpFileSize;
                        if (shouldUpload) {
                            log.Warn("File: {0} ({1}) size DOES NOT match file size on FTP site ({2}).", localBackupFile.Name, StaticHelpers.FormatFileSize(localBackupFile.Length),
                                     StaticHelpers.FormatFileSize(ftpFileSize));
                        }else {
                            log.Info("File: {0} ({1}) size matches file size on FTP site ({2}).", localBackupFile.Name, StaticHelpers.FormatFileSize(localBackupFile.Length),
                                     StaticHelpers.FormatFileSize(ftpFileSize));
                        }
                    }
                }

                if (shouldUpload) {
                    Stopwatch sw = Stopwatch.StartNew();
                    if (!ftpClient.UploadFile(localBackupFile)) {
                        throw new ApplicationException("Upload failed. " + ftpClient.LastStatusDescription);
                    }
                    sw.Stop();
                    double bytesSec = localBackupFile.Length/sw.Elapsed.TotalSeconds;
                    log.Info("Upload of:{0} ({1}) completed in:{2} ({3} / second)", localBackupFile.Name, StaticHelpers.FormatFileSize(localBackupFile.Length), sw.Elapsed,
                             StaticHelpers.FormatFileSize(bytesSec));
                }
            }
        }

        private readonly BackupSettings backupSettings;
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
    }
}