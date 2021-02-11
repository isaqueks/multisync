using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Multisync.App.Util;

namespace Multisync.App
{
    public class ModifiedFileLog
    {

        public static ModifiedFileLog Main { get; private set; }

        public struct ModHeader
        {
            public DateTime lastMultisyncModRecord;
            public DateTime lastDriveModRecord;
        }

        private SafeDictionary<string, ModHeader> filesMod;

        public ModifiedFileLog()
        {
            Main = this;
            filesMod = new SafeDictionary<string, ModHeader>();
        }

        public void SaveTo(string path = "./modlog.multisync")
        {
            if (File.Exists(path))
                File.Delete(path);

            StreamWriter stream = new StreamWriter(path);

            filesMod.Lock();
            foreach (var item in filesMod)
            {
                var msyncRecord = ((long)item.Value.lastMultisyncModRecord.Ticks);
                var driveRecord = ((long)item.Value.lastDriveModRecord.Ticks);

                string line = item.Key + "::" + msyncRecord + "::" + driveRecord;
                stream.WriteLine(line);
            }
            filesMod.Unlock();

            stream.Flush();
            stream.Close();
        }

        public void LoadFrom(string path = "./modlog.multisync")
        {
            if (!File.Exists(path)) return;

            StreamReader stream = new StreamReader(path);
            string line;
            while ((line = stream.ReadLine()) != null)
            {
                string[] args = line.Split("::");

                string strMsyncDate = args[args.Length - 2];
                string strDriveDate = args[args.Length - 1];

                long modMsyncTicks = long.Parse(strMsyncDate);
                long modDriveTicks = long.Parse(strDriveDate);

                string fileName = line.Substring(0,
                        line.Length - 
                        strMsyncDate.Length - 
                        strDriveDate.Length - 
                    2*2);

                ModHeader header;

                header.lastMultisyncModRecord = new DateTime(modMsyncTicks);
                header.lastDriveModRecord = new DateTime(modDriveTicks);

                filesMod.Add(fileName, header);
            }

            stream.Close();
        }

        public void UpdateFileModHeader(string path, ModHeader modSet)
        {
            filesMod.Add(path, modSet);
        }

        public void UpdateFileDriveModDate(string path, DateTime driveModDate)
        {
            ModHeader header;
            header.lastDriveModRecord = driveModDate;
            header.lastMultisyncModRecord = File.GetLastWriteTime(path);

            UpdateFileModHeader(path, header);
        }

        public void UpdateFileMultisyncModDate(string path)
        {
            ModHeader header;
            header.lastDriveModRecord = GetDriveModDate(path);
            header.lastMultisyncModRecord = File.GetLastWriteTime(path);

            UpdateFileModHeader(path, header);
        }

        public void SetFileLastWriteTime(string path, DateTime time)
        {
            if (!File.Exists(path)) return;
            File.SetLastWriteTime(path, time);
            UpdateFileMultisyncModDate(path);
        }

        public void RemoveFile(string path)
        {
            if (filesMod.ContainsKey(path))
                filesMod.Remove(path);
        }

        public DateTime GetMultisyncModDate(string path)
        {
            if (!filesMod.ContainsKey(path))
                return new DateTime(0);

            return filesMod[path].lastMultisyncModRecord;
        }

        public DateTime GetDriveModDate(string path)
        {
            if (!filesMod.ContainsKey(path))
                return new DateTime(0);

            return filesMod[path].lastDriveModRecord;
        }
    }
}
