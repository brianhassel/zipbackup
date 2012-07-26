using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;


namespace BrianHassel.ZipBackup {
    internal static class StaticHelpers {

        public static string DecodeSecret(string sec) {
            try {
                var data = ProtectedData.Unprotect(Convert.FromBase64String(sec), salt, DataProtectionScope.LocalMachine);
                return encoding.GetString(data);
            } catch {
                return null;
            }
        }

        public static string EncodeSecret(string plainText) {
            try {
                var data = ProtectedData.Protect(encoding.GetBytes(plainText), salt, DataProtectionScope.LocalMachine);
                return Convert.ToBase64String(data);
            } catch {
                return null;
            }
        }

        private static readonly UnicodeEncoding encoding = new UnicodeEncoding();
        private static readonly byte[] salt = { 0x12, 0xaa, 0x07, 0x88, 0xd3, 0xab, 0xdd, 0xc2, 0x49, 0xb3 };



        public static void SendEmail(EmailSettings emailSettings, string subject, string body) {

            var emailPassword = DecodeSecret(emailSettings.EmailPassword);

            var client = new SmtpClient(emailSettings.EmailServerAddress, emailSettings.EmailPort) {
                Credentials = new NetworkCredential(emailSettings.EmailUser, emailPassword),
                EnableSsl = emailSettings.EmailUseSSL
            };
            client.Send(emailSettings.EmailUser, emailSettings.EmailTo, subject, body);
        }



        public static string FormatFileSize(object fileSize, int decimalPlaces = 2) {
            if (fileSize is string) {
                return (string)fileSize;
            }

            decimal size;

            try {
                size = Convert.ToDecimal(fileSize);
            } catch (InvalidCastException) {
                return fileSize.ToString();
            }

            string suffix;
            if (size > TeraByte) {
                size /= TeraByte;
                suffix = "TB";
            } else if (size > GigaByte) {
                size /= GigaByte;
                suffix = "GB";
            } else if (size > MegaByte) {
                size /= MegaByte;
                suffix = "MB";
            } else if (size > KiloByte) {
                size /= KiloByte;
                suffix = "KB";
            } else {
                suffix = "B";
                decimalPlaces = 0;
            }

            return String.Format("{0:N" + decimalPlaces + "} {1}", size, suffix);
        }
        private const Decimal KiloByte = 1024M;
        private const Decimal MegaByte = KiloByte * KiloByte;
        private const Decimal GigaByte = MegaByte * KiloByte;
        private const Decimal TeraByte = GigaByte * KiloByte;
    }
}
