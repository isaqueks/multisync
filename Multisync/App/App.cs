using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;

using Google.Apis.Auth.OAuth2;

using Multisync.App.Util;
using Multisync.App;
using Multisync.GoogleDriveInterface;

namespace Multisync.App
{
    public class App
    {
        SyncStack stack;
        Drive drive;
        Settings config;

        static string[] Scopes = { Google.Apis.Drive.v3.DriveService.Scope.Drive,
        };

        UserCredential SetupDriveAuth()
        {
            UserCredential credential;

            using (var stream =
                new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = "token.json";
                credential = Google.Apis.Auth.OAuth2.GoogleWebAuthorizationBroker.AuthorizeAsync(
                    Google.Apis.Auth.OAuth2.GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new Google.Apis.Util.Store.FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            return credential;
        }

        public App()
        {
            drive = new Drive(SetupDriveAuth());
            if (!File.Exists("multisync_settings.xml"))
            {
                string driveRoot = string.Empty;
                string folderToStore = string.Empty;

                Console.Write("Google drive root to sync: ");
                driveRoot = Console.ReadLine();

                repeat:
                Console.Write("Local folder path to sync: ");
                folderToStore = Console.ReadLine();
                if (!Directory.Exists(folderToStore))
                {
                    Console.Write("Invalid folder");
                    goto repeat;
                }

                config = new Settings();
                config.SyncFolder = folderToStore;
                var root = drive.FindItemFromPath(driveRoot, false);
                config.RootFolderID = root.ItemIdentifier;
                config.SaveTo();
            }
            else
            {
                config = new Settings();
                config.ReadFrom();
            }
        }

        public App(Settings pref): base()
        {
            config = pref;
        }

        void SyncDir(SyncStack sync, Drive.Page page)
        {
            while (page != null)
            {
                Thread.Sleep(300); // Give the processor a time to breath

                foreach (var item in page.Items)
                {
                    var syncItem = sync.GetItemById(item.ID);
                    if (syncItem == null ||
                        ((syncItem.State != SynchronizingItem.SyncState.None ||
                        syncItem.Task == SynchronizingItem.TaskState.Done)))

                    sync.SyncWithState(item, SynchronizingItem.SyncState.CheckDownloadOrUpload);
                }

                sync.SyncSync();
                // Give a time for breathing
                System.Threading.Thread.Sleep(300);

                var dirs = page.GetDirectories();
                foreach (var dir in dirs)
                {
                    SyncDir(sync, drive.GetDirectoryContent(dir.ItemIdentifier));
                }

                if (page.NextPageID != null)
                {
                    page = page.Next(drive);
                }
                else
                {
                    page = null;
                }

            }

        }

        private ChangesScanner scanner;

        public void Run()
        {
            stack = new SyncStack(
                config.SyncFolder,
                drive
            );

            if (!Directory.Exists(config.SyncFolder))
                Directory.CreateDirectory(config.SyncFolder);


            stack.Start();

            var absRoot = PathNormalizer.Normalize(
                    Path.Combine(config.SyncFolder,
                    drive.GetFileById(config.RootFolderID.ID).GetFullName()));

            if (!Directory.Exists(absRoot))
                Directory.CreateDirectory(absRoot);

            scanner = new ChangesScanner(
                absRoot, 
                stack, OnFileChanged);
            scanner.StartAsync();

            while (true)
            {
                Drive.Page currentPage = drive.GetDirectoryContent(
                config.RootFolderID, 100);

                SyncDir(stack, currentPage);
            }
        }

        private void OnFileChanged(string file)
        {
            var relativePath = PathNormalizer.Normalize(
                PathNormalizer.Normalize(file).Replace(
                PathNormalizer.Normalize(config.SyncFolder), String.Empty));

            var item = drive.FindItemFromPath(relativePath);
            if (item == null)
            {
                stack.Upload(config.SyncFolder, file);
                stack.ModLog.SetFileLastWriteTime(file, new DateTime(File.GetLastWriteTime(file).Ticks * 2));

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write("FILE NEW  ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(file);
            }
        }
    }
}
