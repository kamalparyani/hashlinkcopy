﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace de.intronik.hashlinkcopy
{
    public class Monitor
    {
        long processedFiles;
        long processedDirectories;
        long skippedFiles;
        long skippedDirectories;
        long copiedFiles;
        long copiedBytes;
        long movedFiles;
        long movedBytes;
        long linkedFiles;
        long linkedBytes;
        long hashedFiles;
        long hashedBytes;
        long deletedFiles;
        long deletedDirectories;
        long createdDirectories;
        long collisions;
        long errors;
        bool dryRun = false;
        DateTime startTime = DateTime.Now;


        string lastFolder = "";
        string lastFile = "";
        string lastLink = "";
        string lastCopy = "";
        string lastError = "";

        public static Monitor Root = new Monitor();

        public bool DryRun
        {
            get { return this.dryRun; }
            set { this.dryRun = value; }
        }

        static string FixWidth(string s, int width)
        {
            return s.Substring(Math.Max(0, s.Length - width)).PadRight(width);
        }

        void PrintIt(string key, int keyLen, object value, int valueLen)
        {
            Console.Write("{0}: {1}", FixWidth(key, keyLen), FixWidth(value.ToString(), valueLen));
            if (Console.CursorLeft != 0)
                Console.SetCursorPosition(0, Console.CursorTop + 1);
        }

        DateTime lastPrint = DateTime.MinValue;

        void PrintInfo()
        {
            if (Logger.Root.Verbosity > Verbosity.None) return;
            var printTime = DateTime.Now;
            if (printTime.Subtract(lastPrint).TotalMilliseconds < 500) return;
            this.lastPrint = printTime;
            Console.SetCursorPosition(0, 0);
            var kl = 20;
            var vl = Console.WindowWidth - 2 - kl;
            PrintIt("Elapsed", kl, DateTime.Now.Subtract(this.startTime), vl);
            PrintIt("Processed files", kl, this.processedFiles, vl);
            PrintIt("Processed folders", kl, this.processedDirectories, vl);
            PrintIt("Linked files", kl, this.linkedFiles, vl);
            PrintIt("Linked bytes", kl, FormatBytes(this.linkedBytes), vl);
            PrintIt("Copied files", kl, this.copiedFiles, vl);
            PrintIt("Copied bytes", kl, FormatBytes(this.copiedBytes), vl);
            PrintIt("Moved files", kl, this.movedFiles, vl);
            PrintIt("Moved bytes", kl, FormatBytes(this.movedBytes), vl);
            PrintIt("Hashed files", kl, this.hashedFiles, vl);
            PrintIt("Hashed bytes", kl, FormatBytes(this.hashedBytes), vl);
            PrintIt("Last directory", kl, lastFolder, vl);
            PrintIt("Last file", kl, lastFile, vl);
            PrintIt("Last linked", kl, lastLink, vl);
            PrintIt("Last copied", kl, lastCopy, vl);
            PrintIt("Last error", kl, lastError, vl);
        }

        public Monitor()
        {
        }

        public void ProcessFile(string path)
        {
            this.lastFile = path;
            this.processedFiles++;
            Logger.Root.WriteLine(Verbosity.Debug, "FILE: {0}", path);
            PrintInfo();
        }
        public void ProcessDirectory(string path)
        {
            this.processedDirectories++;
            this.lastFolder = path;
            Logger.Root.WriteLine(Verbosity.Debug, "FOLDER: {0}", path);
            PrintInfo();
        }
        public void SkipFile(string path, string reason)
        {
            this.skippedFiles++;
            Logger.Root.WriteLine(Verbosity.Debug, "Skipping file '{0}': {1}", path, reason);
        }
        public void SkipDirectory(string path, string reason)
        {
            this.skippedDirectories++;
            Logger.Root.WriteLine(Verbosity.Debug, "Skipping folder '{0}': {1}", path, reason);
        }
        public void CopyFile(string source, string dest, long size)
        {
            if (!this.dryRun) File.Copy(source, dest);
            this.copiedFiles++;
            this.copiedBytes += size;
            this.lastCopy = source;
            Logger.Root.WriteLine(Verbosity.Verbose, "Copy file '{0}' to '{1}'", source, dest);
        }
        public int LinkFile(string source, string dest, long size)
        {
            var start = DateTime.Now;
            var ok = this.dryRun || Win32.CreateHardLink(dest, source, IntPtr.Zero);
            var et = DateTime.Now.Subtract(start).TotalSeconds;
            if (et >= 1.0d)
                Logger.Root.WriteLine(Verbosity.Warning, "Hardlink for {0}=>{1} took {2:F1}s, links {3}", source, dest, et, Win32.GetFileLinkCount(source));
            var errorCode = ok ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (ok)
            {
                this.linkedFiles++;
                this.linkedBytes += size;
                this.lastLink = source;
                Logger.Root.WriteLine(Verbosity.Verbose, "Link file '{0}' to '{1}'", dest, source);
            } return errorCode;
        }

        private void DeleteFileSystemInfo(FileSystemInfo fsi)
        {
            if (this.dryRun)
                fsi.Attributes = FileAttributes.Normal;
            var di = fsi as DirectoryInfo;
            this.deletedFiles++;
            if (di != null)
                foreach (var dirInfo in di.GetFileSystemInfos())
                    DeleteFileSystemInfo(dirInfo);
            if (!this.dryRun)
                fsi.Delete();
        }

        public void DeleteDirectory(string path)
        {
            this.deletedDirectories++;
            Logger.Root.WriteLine(Verbosity.Debug, "Deleting directory '{0}'", path);
            this.DeleteFileSystemInfo(new DirectoryInfo(path));
        }

        public void DeleteFile(FileInfo info)
        {
            this.deletedFiles++;
            Logger.Root.WriteLine(Verbosity.Debug, "Deleting file '{0}'", this.lastFile = info.FullName);
            this.DeleteFileSystemInfo(info);
        }

        public void CreateDirectory(string path)
        {
            this.createdDirectories++;
            Logger.Root.WriteLine(Verbosity.Debug, "Creating Directory '{0}'", path);
            if (!this.dryRun) Directory.CreateDirectory(path);
        }

        public void MoveFile(string source, string dest, long size)
        {
            if (!this.dryRun) File.Move(source, dest);
            this.movedFiles++;
            this.movedBytes += size;
            Logger.Root.WriteLine(Verbosity.Verbose, "Moving file '{0}' to '{1}'", source, dest);
        }

        public byte[] HashFile(HashAlgorithm hashProvider, string source, long size)
        {
            this.hashedFiles++;
            this.hashedBytes += size;
            Logger.Root.WriteLine(Verbosity.Debug, "SHA1 of '{0}' ({1}byte)", source, size);
            using (var inputStream = File.OpenRead(source))
                return hashProvider.ComputeHash(inputStream);
        }
        public void HashCollision(string path1, string path2)
        {
            this.collisions++;
            Logger.Root.WriteLine(Verbosity.Error, "Hash Collision '{0}'<->'{1}'", path1, path2);
        }
        public void Error(string path, Exception error)
        {
            this.errors++;
            this.lastError = error.Message;
            Logger.Root.Error("{0}:{1} processing '{2}'", error.GetType().Name, error.Message, path);
        }

        enum FileSizeUnit
        {
            Byte,
            KB,
            MB,
            GB,
            TB,
            Max,
        };

        public static string FormatBytes(long count)
        {
            var unit = FileSizeUnit.Byte;
            while ((count >> (10 * (int)unit)) > 1024) unit++;
            var b = new StringBuilder();
            while (unit >= FileSizeUnit.Byte)
            {
                var divider = (long)1 << (10 * (int)unit);
                if (b.Length > 0)
                    b.Append(' ');
                b.AppendFormat("{0}{1}", count / divider, unit);
                count %= divider;
                unit--;
            }
            return b.ToString();

        }

        public void PrintStatistics()
        {
            Logger.Root.PrintInfo("processedFiles", processedFiles);
            Logger.Root.PrintInfo("processedDirectories", processedDirectories);
            Logger.Root.PrintInfo("skippedFiles", skippedFiles);
            Logger.Root.PrintInfo("copiedFiles", copiedFiles);
            Logger.Root.PrintInfo("copiedBytes", FormatBytes(copiedBytes));
            Logger.Root.PrintInfo("movedFiles", movedFiles);
            Logger.Root.PrintInfo("movedBytes", FormatBytes(movedBytes));
            Logger.Root.PrintInfo("linkedFiles", linkedFiles);
            Logger.Root.PrintInfo("linkedBytes", FormatBytes(linkedBytes));
            Logger.Root.PrintInfo("hashedFiles", hashedFiles);
            Logger.Root.PrintInfo("hashedBytes", FormatBytes(hashedBytes));
            Logger.Root.PrintInfo("deletedFiles", deletedFiles);
            Logger.Root.PrintInfo("deletedDirectories", deletedDirectories);
            Logger.Root.PrintInfo("createdDirectories", createdDirectories);
            Logger.Root.PrintInfo("collisions", collisions);
            Logger.Root.PrintInfo("errors", errors);
        }
    }
}
