using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using NLog;


namespace BrianHassel.ZipBackup {
    internal static class StaticHelpers {
        
        public static bool SendEmail(EmailSettings emailSettings, string subject, string body) {
            try {
                var emailPassword = SecurityHelpers.DecodeSecret(emailSettings.EmailPassword);

                var client = new SmtpClient(emailSettings.EmailServerAddress, emailSettings.EmailPort) {
                                                                                                           Credentials =
                                                                                                               new NetworkCredential(emailSettings.EmailUser, emailPassword),
                                                                                                           EnableSsl = emailSettings.EmailUseSSL
                                                                                                       };

                //Create the email
                var mailMessage = new MailMessage(emailSettings.EmailUser, emailSettings.EmailTo, subject, body);

                //Try to attach the log if specified
                var logFile = Path.Combine(Environment.CurrentDirectory, "logs", "logfile.txt");

                if (File.Exists(logFile))
                    mailMessage.Attachments.Add(new Attachment(logFile));
                else {
                    log.Warn("Unable to find log file to attach: " + logFile);
                }

                client.Send(mailMessage);
                return true;
            }catch(Exception e) {
                log.ErrorException("Could not send email. " + subject, e);
                return false;
            }
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

        private static readonly Logger log = LogManager.GetCurrentClassLogger();
    }
}
