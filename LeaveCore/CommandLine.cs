using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Globalization;

namespace LeaveCore
{
    public class CommandLine
    {
        public static bool Run(ProcessStartInfo startInfo)
        {
            return Run(startInfo, false);
        }
        public static bool Run(ProcessStartInfo startInfo, bool WaitForExit)
        {
            Process proc = new Process();
            proc.StartInfo = startInfo;
            bool b = proc.Start();
            if (WaitForExit)
                proc.WaitForExit();
            return b;
        }
    }

    public enum FingerScanCommand { Help = 0, Get, Clean, Conso, Build, Sync, Check }
    public class CommandLineHelper
    {
        static string[] Commands = new string[] { "?", "get", "clean", "conso", "build", "sync", "check" };
        public static ProcessStartInfo FingerScanCommand(
            FingerScanCommand Command, DateTime FromDate, DateTime ToDate, long LogId, string WorkingDirectory)
        {
            if (string.IsNullOrEmpty(WorkingDirectory))
                WorkingDirectory = WorkingPath;
            if (!WorkingDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
                WorkingDirectory += Path.DirectorySeparatorChar;

            CultureInfo en = new CultureInfo("en-US");
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = WorkingDirectory + "FingerScan.exe",
                Arguments = string.Format("-from {0} -to {1} -logid {2} {3}",
                    FromDate.ToString("yyyyMMdd", en), ToDate.ToString("yyyyMMdd", en), LogId, Commands[(int)Command]),
                ErrorDialog = false,
                UseShellExecute = false,
                WorkingDirectory = WorkingDirectory
            };
            return startInfo;
        }
        static string WorkingPath
        {
            get
            {
                string workdir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (!workdir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    workdir += Path.DirectorySeparatorChar;
                return workdir;
            }
        }
    }
}
