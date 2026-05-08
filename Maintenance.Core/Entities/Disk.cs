using System;
using System.IO;

namespace Maintenance.Core.Entities
{
    public class Disk
    {
        private readonly string _rootFolderPath;

        public string Name { get; set; }
        public string Letter { get; set; }
        public long FreeSpace { get; set; }
        public long TotalSize { get; set; }
        public double FreeSpacePercentage { get; set; }

        public Disk(string name, string letter, long freeSpace, long totalSize)
        {
            Name = string.IsNullOrEmpty(name) ? "" : name;
            Letter = letter;
            FreeSpace = freeSpace / 1024 / 1024 / 1024;
            TotalSize = totalSize / 1024 / 1024 / 1024;
            FreeSpacePercentage = Math.Round((double)freeSpace / totalSize * 100, 2);

            _rootFolderPath = AppDomain.CurrentDomain.BaseDirectory;
        }

        public bool Exists() => new DriveInfo(Letter).IsReady;

        public bool IsCritical() => FreeSpacePercentage < 10;

        public string GetScript() => File.ReadAllText($"{_rootFolderPath}/{Letter}.txt");

        public bool HasScript() => File.Exists($"{_rootFolderPath}/{Letter}.txt");
    }
}
