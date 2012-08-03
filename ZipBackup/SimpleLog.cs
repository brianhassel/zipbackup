using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BrianHassel.ZipBackup {
    public class SimpleLog {

        public SimpleLog(bool logToConsole) {
            this.logToConsole = logToConsole;
            logLines = new List<string>(150);
        }

        public void Info(string text) {
            logLines.Add(text);
            if(logToConsole)
                Console.WriteLine(text);
        }

        public void Error(string text) {
            logLines.Add(text);
            if (logToConsole)
                Console.WriteLine(text);
        }

        public string GetEmailText() {
            return string.Join("\n", logLines);
        }


        private readonly bool logToConsole;
        private readonly List<string> logLines;
    }
}
