﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SharedLibrary
{
    public class IFile
    {
        public IFile(String fileName)
        {
            //Not safe for directories with more than one folder but meh
            _Directory = fileName.Split('\\')[0];
            Name = (fileName.Split('\\'))[fileName.Split('\\').Length - 1];

            if (!Directory.Exists(_Directory))
                Directory.CreateDirectory(_Directory);

            if (!File.Exists(fileName))
            {
                try
                {
                    FileStream penis = File.Create(fileName);
                    penis.Close();
                }

                catch
                {
                    Console.WriteLine("Unable to create file!");
                }
            }

            try
            {
                Handle = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                sze = Handle.BaseStream.Length;
            }

            catch
            {
                //Console.WriteLine("Unable to open log file for writing!");
            }
        }

        public IFile(String file, bool write)
        {
            Name = file;
            writeHandle = new StreamWriter(new FileStream(Name, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
            sze = 0;
        }

        public long getSize()
        {
            sze = Handle.BaseStream.Length;
            return sze;
        }

        public void Write(String line)
        {
            if (writeHandle != null)
            {
                writeHandle.WriteLine(line);
                writeHandle.Flush();
            }
        }

        public String[] getParameters(int num)
        {
            if (sze > 0)
            {
                String firstLine = Handle.ReadLine();
                String[] Parms = firstLine.Split(':');
                if (Parms.Length < num)
                    return null;
                else
                    return Parms;
            }

            return null;
        }

        public void Close()
        {
            if (Handle != null)
                Handle.Close();
            if (writeHandle != null)
                writeHandle.Close();
        }

        public String[] readAll()
        {
            return Handle.ReadToEnd().Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public String getLines()
        {
            return Handle.ReadToEnd();
        }

        public String[] Tail(int lineCount)
        {
            var buffer = new List<string>(lineCount);
            string line;
            for (int i = 0; i < lineCount; i++)
            {
                line = Handle.ReadLine();
                if (line == null) return buffer.ToArray();
                buffer.Add(line);
            }

            int lastLine = lineCount - 1;           //The index of the last line read from the buffer.  Everything > this index was read earlier than everything <= this indes

            while (null != (line = Handle.ReadLine()))
            {
                lastLine++;
                if (lastLine == lineCount) lastLine = 0;
                buffer[lastLine] = line;
            }

            if (lastLine == lineCount - 1) return buffer.ToArray();
            var retVal = new string[lineCount];
            buffer.CopyTo(lastLine + 1, retVal, 0, lineCount - lastLine - 1);
            buffer.CopyTo(0, retVal, lineCount - lastLine - 1, lastLine + 1);
            return retVal;
        }
        //END

        private long sze;
        private String Name;
        private String _Directory;
        private StreamReader Handle;
        private StreamWriter writeHandle;
    }
}
