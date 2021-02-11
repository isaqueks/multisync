using System;
using System.IO;
using System.Xml;
using Multisync.GoogleDriveInterface;

namespace Multisync.App
{
    public class Settings
    {
        public string SyncFolder;
        public ItemID RootFolderID;

        public static Settings Default()
        {
            Settings config = new Settings();
            config.RootFolderID = ItemID.ROOT;
            config.SyncFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "Multisync");

            return config;
        }

        public void SaveTo(string path="multisync_settings.xml")
        {
            XmlDocument doc = new XmlDocument();
            var root = doc.CreateElement("Settings");
            doc.AppendChild(root);

            var syncFolderElement = doc.CreateElement("SyncFolder");
            syncFolderElement.SetAttribute("value", SyncFolder);
            root.AppendChild(syncFolderElement);

            var rootFolderIDElement = doc.CreateElement("RootFolderID");
            rootFolderIDElement.SetAttribute("ID", RootFolderID.ID);
            rootFolderIDElement.SetAttribute("Name", RootFolderID.Name);
            root.AppendChild(rootFolderIDElement);

            doc.Save(path);
        }
        
        public void ReadFrom(string path = "multisync_settings.xml")
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(path);

            var root = doc.GetElementsByTagName("Settings")[0];
            var syncFolderElement = doc.GetElementsByTagName("SyncFolder")[0];
            var rootFolderIDElement = doc.GetElementsByTagName("RootFolderID")[0];

            SyncFolder = syncFolderElement.Attributes["value"].Value;

            RootFolderID = new ItemID(
                rootFolderIDElement.Attributes["Name"].Value,
                rootFolderIDElement.Attributes["ID"].Value);
        }
    }
}
