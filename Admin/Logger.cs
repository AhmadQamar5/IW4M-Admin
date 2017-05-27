﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace IW4MAdmin
{
    class Logger : SharedLibrary.Interfaces.ILogger
    {
        enum LogType
        { 
            Verbose,
            Info,
            Debug,
            Warning,
            Error
        }

        string FileName;

        public Logger(string fn)
        {
            FileName = fn;
        }

        void Write(string msg, LogType type)
        {
            string LogLine = $"[{DateTime.Now.ToString("HH:mm:ss")}] - {type}: {msg}";
#if DEBUG
            // lets keep it simple and dispose of everything quickly as logging wont be that much (relatively)
           
            Console.WriteLine(LogLine);
            File.AppendAllText(FileName, LogLine);
#else
            if (type == LogType.Error || type == LogType.Verbose)
                Console.WriteLine(LogLine);
            if (type != LogType.Debug)
                File.AppendAllText(FileName, LogLine);
#endif

        }

        public void WriteVerbose(string msg)
        {
            Write(msg, LogType.Verbose);
        }

        public void WriteDebug(string msg)
        {
            Write(msg, LogType.Debug);
        }

        public void WriteError(string msg)
        {
            Write(msg, LogType.Error);
        }

        public void WriteInfo(string msg)
        {
            Write(msg, LogType.Info);
        }

        public void WriteWarning(string msg)
        {
            Write(msg, LogType.Warning);
        }
    }
}
