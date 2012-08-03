using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace BrianHassel.ZipBackup {
    public class FTPClient {

        public FTPClient(string hostAddress, string userName, string password, string domain = null, int? port = null, bool useSSL = true, bool ignoreCert = true, int bufferSize = 1024, bool useBinary=true, bool usePassive=true) {
            this.hostAddress = hostAddress;
            this.port = port;
            this.useSSL = useSSL;
            this.bufferSize = bufferSize;
            this.useBinary = useBinary;
            this.usePassive = usePassive;

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

        public string RemoteFolder { get; set; }

        private bool CheckStatusCode(FtpWebResponse response) {
            //200 and 421        
            LastStatusDescription = response.StatusDescription;
            return response.StatusCode >= FtpStatusCode.CommandOK && response.StatusCode < FtpStatusCode.ServiceNotAvailable;
        }


        private FtpWebRequest BuildFTPRequest(string remoteFileName, string ftpRequest) {
            //UriBuilder uri = new UriBuilder("ftp", hostAddress);
            //if (port.HasValue && port.Value != 21)
            //    uri.Port = port.Value;
            //if (!string.IsNullOrEmpty(RemoteFolder))
            //    uri.Path = RemoteFolder + "/" + remoteFileName;
            //else {
            //    uri.Path = remoteFileName;
            //}
            //if (!string.IsNullOrEmpty(remoteFileName))
            //    ftpFullPath += remoteFileName;
            

            var ftpFullPath = string.Format("ftp://{0}", hostAddress);
            if (port.HasValue && port.Value != 21)
                ftpFullPath += ":" + port.Value;
            ftpFullPath += "/";
            if (!string.IsNullOrEmpty(RemoteFolder))
                ftpFullPath += RemoteFolder + "/";
            if (!string.IsNullOrEmpty(remoteFileName))
                ftpFullPath += remoteFileName;

            //var ftpFullPath = port.HasValue ? string.Format("ftp://{0}:{1}/{2}", hostAddress, port.Value, remoteFileName) : string.Format("ftp://{0}/{1}", hostAddress, remoteFileName);
            //Console.WriteLine(uri.Uri);
            var ftp = (FtpWebRequest)WebRequest.Create(ftpFullPath);
            if (networkCredentials != null)
                ftp.Credentials = networkCredentials;
            ftp.KeepAlive = true;
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
    }
}
