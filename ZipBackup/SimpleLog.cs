using System;
using System.Text;

namespace BrianHassel.ZipBackup {
    public class SimpleLog {

        public SimpleLog(bool logToConsole) {
            this.logToConsole = logToConsole;
            logLines = new StringBuilder(1000);
        }

        public void Info(string text) {
            logLines.AppendLine(text);
            if(logToConsole)
                Console.WriteLine(text);
        }

        public void Error(string text) {
            logLines.AppendLine(text);
            if (logToConsole)
                Console.WriteLine(text);
        }

        public string GetEmailText() {
            return logLines.ToString();
        }


        private readonly bool logToConsole;
        private readonly StringBuilder logLines;
    }
}
