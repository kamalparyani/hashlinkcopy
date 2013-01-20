﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public class HashLinkActionEventArgs : EventArgs
    {
        public FileSystemInfo Info { get; private set; }
        public int Level { get; private set; }
        public HashLinkAction Action { get; private set; }
        public string ExtendedInfo { get; private set; }
        public bool Cancel { get; set; }
        public HashLinkActionEventArgs(FileSystemInfo info, int level, HashLinkAction action, string extendedInfo = null)
        {
            this.Info = info;
            this.Level = level;
            this.Action = action;
            this.ExtendedInfo = extendedInfo;
        }
        public override string ToString()
        {
            return String.Format("{0}: \"{1}\"{2}", this.Action, this.Info.FullName, ExtendedInfo);
        }
    }
    public enum HashLinkAction
    {
        EnterSourceDirectory,
        ProcessSourceFile,
        CopyFile,
        LinkFile,
        LinkDirectory,
    }
}

