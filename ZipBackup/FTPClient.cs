using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using NLog;

namespace BrianHassel.ZipBackup {
    public class FTPClient {

        /// <summary>Wrapper for FTPWebRequests</summary>
        /// <param name="hostAddress">The host address with any folders. Format may resemble: ftp.site.com//folder - which indicates (double slash) that the root is assumed. 
        /// This could represent ftp.site.com/Home/folder for sites that default to a subdir.</param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <param name="domain"></param>
        /// <param name="port">Defaults to 21.</param>
        /// <param name="ignoreCert"></param>
        /// <param name="bufferSize"></param>
        /// <param name="useBinary"></param>
        /// <param name="usePassive"></param>
        /// <param name="keepAlive">Defaults to false. KeepAlive=true can cause issues with directories other than the root and other login issues. (Slightly slower)</param>
        public FTPClient(string hostAddress, string userName, string password, string domain = null, int? port = null, bool useSSL = true, bool ignoreCert = true,
                         int bufferSize = 1024, bool useBinary = true, bool usePassive = true, bool keepAlive = false) {
            this.hostAddress = hostAddress;
            this.port = port;
            this.useSSL = useSSL;
            this.bufferSize = bufferSize;
            this.useBinary = useBinary;
            this.usePassive = usePassive;
            this.keepAlive = keepAlive;

            networkCredentials = string.IsNullOrEmpty(domain) ? new NetworkCredential(userName, password) : new NetworkCredential(userName, password, domain);

            if (ignoreCert)
                ServicePointManager.ServerCertificateValidationCallback += delegate { return true; };
        }

        public bool UploadFile(FileInfo localFile, string remoteFileName = null, int attempts = 3) {
            for (int i = 0; i < attempts; i++) {
                var ret = UploadFileInternal(localFile, remoteFileName);
                if (ret)
                    return true;

                System.Threading.Thread.Sleep(10000);
            }
            return false;
        }

        private bool UploadFileInternal(FileInfo localFile, string remoteFileName) {
            if (string.IsNullOrEmpty(remoteFileName))
                remoteFileName = localFile.Name;
            try {
                var ftp = BuildFTPRequest(remoteFileName, WebRequestMethods.Ftp.UploadFile);
                using (var fs = localFile.OpenRead()) {
                    ftp.ContentLength = fs.Length;
                    using (var ftpstream = ftp.GetRequestStream()) {
                        fs.CopyTo(ftpstream, bufferSize);
                    }
                }

                using (var response = (FtpWebResponse) ftp.GetResponse()) {
                    return CheckStatusCode(response);
                }
            } catch (Exception e) {
                log.ErrorException(localFile.FullName, e);
                return false;
            }
        }

        public bool DownloadFile(FileInfo localFile, string remoteFileName = null, int attempts = 3) {
            for (int i = 0; i < attempts; i++) {
                var ret = DownloadFileInternal(localFile, remoteFileName);
                if (ret)
                    return true;

                System.Threading.Thread.Sleep(10000);
            }
            return false;
        }


        private bool DownloadFileInternal(FileInfo localFile, string remoteFileName) {
            if (string.IsNullOrEmpty(remoteFileName))
                remoteFileName = localFile.Name;
            try {
                FtpWebRequest ftp = BuildFTPRequest(remoteFileName, WebRequestMethods.Ftp.DownloadFile);
                using (var response = (FtpWebResponse) ftp.GetResponse()) {
                    using (var ftpstream = response.GetResponseStream()) {
                        using (FileStream fs = localFile.OpenWrite()) {
                            if (ftpstream != null)
                                ftpstream.CopyTo(fs, bufferSize);
                        }
                    }
                    return CheckStatusCode(response);
                }
            } catch (Exception e) {
                log.ErrorException(localFile.FullName, e);
                return false;
            }
        }

        public List<string> GetList(bool includeDetails = false, int attempts = 3) {
            for (int i = 0; i < attempts; i++) {
                var ret = GetListInternal(includeDetails);
                if (ret != null)
                    return ret;

                System.Threading.Thread.Sleep(10000);
            }
            return null;
        }

        private List<string> GetListInternal(bool includeDetails = false) {
            try {
                var lines = new List<string>();
                var ftp = BuildFTPRequest("", includeDetails ? WebRequestMethods.Ftp.ListDirectoryDetails : WebRequestMethods.Ftp.ListDirectory);
                using (var response = (FtpWebResponse) ftp.GetResponse()) {
                    using (var ftpstream = new StreamReader(response.GetResponseStream())) {
                        string line;
                        while ((line = ftpstream.ReadLine()) != null) {
                            lines.Add(line);
                        }
                        LastStatusDescription = response.StatusDescription;
                    }
                }
                return lines;
            } catch (Exception e) {
                log.ErrorException(includeDetails.ToString(), e);
                return null;
            }
        }

        public DateTime? GetModifiedDate(string remoteName, int attempts = 3) {
            for (int i = 0; i < attempts; i++) {
                var ret = GetModifiedDateInternal(remoteName);
                if (ret != null)
                    return ret;

                System.Threading.Thread.Sleep(10000);
            }
            return null;
        }

        private DateTime? GetModifiedDateInternal(string remoteName) {
            try {
                var ftp = BuildFTPRequest(remoteName, WebRequestMethods.Ftp.GetDateTimestamp);
                using (var response = (FtpWebResponse) ftp.GetResponse()) {
                    if (CheckStatusCode(response))
                        return response.LastModified;
                }
                return null;
            } catch (Exception e) {
                log.ErrorException(remoteName, e);
                return null;
            }
        }

        public long? GetFileSize(string remoteFileName, int attempts = 3) {
            for (int i = 0; i < attempts; i++) {
                var ret = GetFileSizeInternal(remoteFileName);
                if (ret != null)
                    return ret;

                System.Threading.Thread.Sleep(10000);
            }
            return null;
        }

        private long? GetFileSizeInternal(string remoteFileName) {
            try {
                var ftp = BuildFTPRequest(remoteFileName, WebRequestMethods.Ftp.GetFileSize);
                using (var response = (FtpWebResponse) ftp.GetResponse()) {
                    if (CheckStatusCode(response))
                        return response.ContentLength;
                }
                return null;
            } catch (Exception e) {
                log.ErrorException(remoteFileName, e);
                return null;
            }
        }


        public bool DeleteFile(string remoteFileName, int attempts = 3) {
            for (int i = 0; i < attempts; i++) {
                var ret = DeleteFileInternal(remoteFileName);
                if (ret)
                    return true;

                System.Threading.Thread.Sleep(10000);
            }
            return false;
        }

        private bool DeleteFileInternal(string remoteFileName) {
            try {
                var ftp = BuildFTPRequest(remoteFileName, WebRequestMethods.Ftp.DeleteFile);
                using (var response = (FtpWebResponse) ftp.GetResponse()) {
                    return CheckStatusCode(response);
                }
            } catch (Exception e) {
                log.ErrorException(remoteFileName, e);
                return false;
            }
        }

        public bool RemoveDirectory(string remoteDirectory, int attempts = 3) {
            for (int i = 0; i < attempts; i++) {
                var ret = RemoveDirectoryInternal(remoteDirectory);
                if (ret)
                    return true;

                System.Threading.Thread.Sleep(10000);
            }
            return false;
        }

        private bool RemoveDirectoryInternal(string remoteDirectory) {
            try {
                var ftp = BuildFTPRequest(remoteDirectory, WebRequestMethods.Ftp.RemoveDirectory);
                using (var response = (FtpWebResponse) ftp.GetResponse()) {
                    return CheckStatusCode(response);
                }
            } catch (Exception e) {
                log.ErrorException(remoteDirectory, e);
                return false;
            }
        }

        public bool MakeDirectory(string remoteDirectory, int attempts = 3) {
            for (int i = 0; i < attempts; i++) {
                var ret = MakeDirectoryInternal(remoteDirectory);
                if (ret)
                    return true;

                System.Threading.Thread.Sleep(10000);
            }
            return false;
        }

        private bool MakeDirectoryInternal(string remoteDirectory) {
            try {
                var ftp = BuildFTPRequest(remoteDirectory, WebRequestMethods.Ftp.MakeDirectory);
                using (var response = (FtpWebResponse) ftp.GetResponse()) {
                    return CheckStatusCode(response);
                }
            } catch (Exception e) {
                log.ErrorException(remoteDirectory, e);
                return false;
            }
        }

        public string LastStatusDescription { get; private set; }

        private bool CheckStatusCode(FtpWebResponse response) {
            //200 and 421        
            LastStatusDescription = response.StatusDescription;
            log.Debug(response.StatusCode + " " + response.StatusDescription);
            return response.StatusCode >= FtpStatusCode.CommandOK && response.StatusCode < FtpStatusCode.ServiceNotAvailable;
        }


        private FtpWebRequest BuildFTPRequest(string remoteFileName, string ftpRequest) {
            var uriBuilder = new UriBuilder("ftp", hostAddress) {Path = remoteFileName};
            if (port.HasValue && port.Value != 21)
                uriBuilder.Port = port.Value;

            var uri = uriBuilder.Uri;

            log.Debug("({0}) {1}", ftpRequest, uri);

            var ftp = (FtpWebRequest) WebRequest.Create(uri);
            if (networkCredentials != null)
                ftp.Credentials = networkCredentials;
            ftp.KeepAlive = keepAlive;
            ftp.UseBinary = useBinary;
            ftp.Method = ftpRequest;
            ftp.UsePassive = usePassive;
            ftp.EnableSsl = useSSL;

            return ftp;
        }

        private readonly string hostAddress;
        private int? port;
        private readonly bool useSSL;
        private readonly NetworkCredential networkCredentials;
        private readonly int bufferSize;
        private readonly bool useBinary;
        private readonly bool usePassive;
        private readonly bool keepAlive;
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
    }
}
