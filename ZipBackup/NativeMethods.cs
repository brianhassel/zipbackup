using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace BrianHassel.ZipBackup {
    internal static class NativeMethods {

        public static void PreventSleep() {
            try {
                SetThreadExecutionState(ExecutionState.EsContinuous | ExecutionState.EsSystemRequired);
            } catch {}
        }

        public static void AllowSleep() {
            try {
                SetThreadExecutionState(ExecutionState.EsContinuous);
            } catch {}
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

        [FlagsAttribute]
        private enum ExecutionState : uint {
            EsAwaymodeRequired = 0x00000040,
            EsContinuous = 0x80000000,
            EsDisplayRequired = 0x00000002,
            EsSystemRequired = 0x00000001
        }
    }
}
