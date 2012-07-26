using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace BrianHassel.ZipBackup {
    public class SecurityHelpers {
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
    }
}
