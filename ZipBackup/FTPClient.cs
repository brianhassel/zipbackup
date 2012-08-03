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
        public FTPClient(string hostAddress, string userName, string password, string domain = null, int? port = null, bool useSSL = true, bool ignoreCert = true, int bufferSize = 1024, bool useBinary=true, bool usePassive=true, bool keepAlive = false) {
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

        public bool UploadFile(FileInfo localFile, string remoteFileName = null) {
            if (string.IsNullOrEmpty(remoteFileName)) remoteFileName = localFile.Name;

            var ftp = BuildFTPRequest(remoteFileName, WebRequestMethods.Ftp.UploadFile);
            using (var fs = localFile.OpenRead()) {
                ftp.ContentLength = fs.Length;
                using (var ftpstream = ftp.GetRequestStream()) {
                    fs.CopyTo(ftpstream, bufferSize);
                }
            }

            using (var response = (FtpWebResponse)ftp.GetResponse()) {
                return CheckStatusCode(response);
            }
        }

        public bool DownloadFile(FileInfo localFile, string remoteFileName = null) {
            if (string.IsNullOrEmpty(remoteFileName)) remoteFileName = localFile.Name;
            FtpWebRequest ftp = BuildFTPRequest(remoteFileName, WebRequestMethods.Ftp.DownloadFile);
            using (var response = (FtpWebResponse) ftp.GetResponse()) {
                using (var ftpstream = response.GetResponseStream()) {
                    using (FileStream fs = localFile.OpenWrite()) {
                        if (ftpstream != null) ftpstream.CopyTo(fs, bufferSize);
                    }
                }
                return CheckStatusCode(response);
            }
        }

        public List<string> GetList(bool includeDetails = false) {
            var lines = new List<string>();
            var ftp = BuildFTPRequest("", includeDetails ? WebRequestMethods.Ftp.ListDirectoryDetails : WebRequestMethods.Ftp.ListDirectory);
            using (var response = (FtpWebResponse)ftp.GetResponse()) {
                using (var ftpstream = new StreamReader(response.GetResponseStream())) {
                    string line;
                    while ((line = ftpstream.ReadLine()) != null) {
                        lines.Add(line);
                    }
                    LastStatusDescription = response.StatusDescription;
                }
            }
            return lines;
        }

        public DateTime? GetModifiedDate(string remoteName) {
            var ftp = BuildFTPRequest(remoteName, WebRequestMethods.Ftp.GetDateTimestamp);
            using (var response = (FtpWebResponse)ftp.GetResponse()) {
                if (CheckStatusCode(response))
                    return response.LastModified;
            }
            return null;
        }

        public long? GetFileSize(string remoteFileName) {
            var ftp = BuildFTPRequest(remoteFileName, WebRequestMethods.Ftp.GetFileSize);
            using (var response = (FtpWebResponse)ftp.GetResponse()) {
                if(CheckStatusCode(response))
                    return response.ContentLength;
            }
            return null;
        }


        public bool DeleteFile(string remoteFileName) {
            var ftp = BuildFTPRequest(remoteFileName, WebRequestMethods.Ftp.DeleteFile);
            using (var response = (FtpWebResponse) ftp.GetResponse()) {
                return CheckStatusCode(response);
            }
        }

        public bool RemoveDirectory(string remoteDirectory) {
            var ftp = BuildFTPRequest(remoteDirectory, WebRequestMethods.Ftp.RemoveDirectory);
            using (var response = (FtpWebResponse)ftp.GetResponse()) {
                return CheckStatusCode(response);
            }
        }

        public bool MakeDirectory(string remoteDirectory) {
            var ftp = BuildFTPRequest(remoteDirectory, WebRequestMethods.Ftp.MakeDirectory);
            using (var response = (FtpWebResponse)ftp.GetResponse()) {
                return CheckStatusCode(response);
            }
        }

        public string LastStatusDescription { get; private set; }

        private bool CheckStatusCode(FtpWebResponse response) {
            //200 and 421        
            LastStatusDescription = response.StatusDescription;
            return response.StatusCode >= FtpStatusCode.CommandOK && response.StatusCode < FtpStatusCode.ServiceNotAvailable;
        }


        private FtpWebRequest BuildFTPRequest(string remoteFileName, string ftpRequest) {
            var uriBuilder = new UriBuilder("ftp", hostAddress) { Path = remoteFileName };
            if (port.HasValue && port.Value != 21)
                uriBuilder.Port = port.Value;

            var uri = uriBuilder.Uri;

            log.Debug("({0}) {1}", ftpRequest, uri);

            var ftp = (FtpWebRequest)WebRequest.Create(uri);
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
