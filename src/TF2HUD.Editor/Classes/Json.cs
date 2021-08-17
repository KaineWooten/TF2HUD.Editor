﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using HUDEditor.Models;
using HUDEditor.Properties;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;

namespace HUDEditor.Classes
{
    public class Json : INotifyPropertyChanged
    {
        // HUDs to manage
        public HUD[] HUDList;

        // Highlighted HUD
        private HUD _highlightedHud;
        public HUD HighlightedHUD { get { return this._highlightedHud; } set { this._highlightedHud = value; OnPropertyChanged("HighlightedHUD"); OnPropertyChanged("HighlightedHUDInstalled"); } }
        public bool HighlightedHUDInstalled { get { return HighlightedHUD != null ? Directory.Exists($"{MainWindow.HudPath}\\{HighlightedHUD.Name}") : false; } }

        // Selected HUD
        private HUD _selectedHud;
        public HUD SelectedHUD { get { return this._selectedHud; } set { this._selectedHud = value; SelectionChanged?.Invoke(this, value); OnPropertyChanged("SelectedHUD"); } }

        // Selected HUD Installed
        public bool SelectedHUDInstalled
        {
            get
            {
                MainWindow.Logger.Info("this.SelectedHUD != null" + this.SelectedHUD != null);
                MainWindow.Logger.Info("MainWindow.HudPath != \"\"" + MainWindow.HudPath != "");
                MainWindow.Logger.Info("Directory.Exists(MainWindow.HudPath)" + Directory.Exists(MainWindow.HudPath));
                MainWindow.Logger.Info("Utilities.CheckUserPath(MainWindow.HudPath)" + Utilities.CheckUserPath(MainWindow.HudPath));
                MainWindow.Logger.Info("MainWindow.CheckHudInstallation()" + MainWindow.CheckHudInstallation());
                return this.SelectedHUD != null && MainWindow.HudPath != "" && Directory.Exists(MainWindow.HudPath) && Utilities.CheckUserPath(MainWindow.HudPath) && MainWindow.CheckHudInstallation();
            }
        }

        public event EventHandler<HUD> SelectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public Json()
        {
            var hudList = new List<HUD>();
            foreach (var jsonFile in Directory.EnumerateFiles("JSON"))
            {
                // Extract HUD information from the file path.
                var fileName = jsonFile.Split("\\")[^1];
                var fileInfo = fileName.Split(".");
                var hudName = fileInfo[0];
                var extension = fileInfo[^1];
                if (extension != "json") continue;
                var json = new StreamReader(File.OpenRead(jsonFile), new UTF8Encoding(false)).ReadToEnd();

                // Add the HUD object to the list.
                hudList.Add(new HUD(hudName, JsonConvert.DeserializeObject<HudJson>(json)));
            }

            // Load all shared huds from JSON/Shared/shared.json, and
            // shared controls from JSON/Shared/controls.json, for each
            // hud, assign unique ids for the controls based on the hud
            // name and add to HUDs list.

            var sharedFolder = $"JSON\\Shared";
            var sharedHUDs = JsonConvert.DeserializeObject<List<HudJson>>(
                new StreamReader(File.OpenRead($"{sharedFolder}\\shared.json"), new UTF8Encoding(false)).ReadToEnd()
            );
            var sharedControlsJSON = new StreamReader(File.OpenRead($"{sharedFolder}\\controls.json"), new UTF8Encoding(false)).ReadToEnd();
            hudList.AddRange(sharedHUDs.Select(hud =>
            {
                var hudControls = JsonConvert.DeserializeObject<HudJson>(sharedControlsJSON);
                foreach (var group in hudControls.Controls)
                    foreach (var control in hudControls.Controls[group.Key])
                        control.Name = $"{Utilities.EncodeID(hud.Name)}_{Utilities.EncodeID(control.Name)}";

                hud.Layout = hudControls.Layout;
                hud.Controls = hudControls.Controls;
                return new HUD(hud.Name, hud);
            }));

            HUDList = hudList.ToArray();

            var selectedHud = GetHUDByName(Settings.Default.hud_selected);
            HighlightedHUD = selectedHud;
            SelectedHUD = selectedHud;
        }

        /// <summary>
        ///     Find and retrieve a HUD object selected by the user.
        /// </summary>
        /// <param name="name">Name of the HUD the user wants to view.</param>
        public HUD GetHUDByName(string name)
        {
            foreach (var hud in HUDList)
                if (string.Equals(hud.Name, name, StringComparison.InvariantCultureIgnoreCase))
                    return hud;

            // MainWindow.ShowMessageBox(MessageBoxImage.Error, string.Format(Utilities.GetLocalizedString(Resources.error_hud_missing), name));
            return null;
        }

        /// <summary>
        ///     Invoke a WPF Binding update of a property.
        /// </summary>
        /// <param name="propertyChanged">Name of property to update</param>
        public void OnPropertyChanged(string propertyChanged)
        {
            MainWindow.Logger.Info($"OnPropertyChanged: {propertyChanged}");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyChanged));
        }

        /// <summary>
        ///     Synchronize the local HUD schema files with the latest versions on GitHub.
        /// </summary>
        public static bool Update(bool force = false)
        {
            try
            {
                var restartRequired = false;

                // Get the local schema names and file sizes.
                List<Tuple<string, int>> localFiles = new();
                foreach (var file in new DirectoryInfo("JSON").GetFiles().Where(x => x.FullName.EndsWith(".json")))
                    localFiles.Add(new Tuple<string, int>(file.Name.Replace(".json", string.Empty), (int) file.Length));
                if (localFiles.Count <= 0) return false;

                // Setup the WebClient for download remote files.
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                var client = new WebClient();
                client.Headers.Add("User-Agent", "request");
                var remoteList = client.DownloadString(Settings.Default.json_list);
                client.Dispose();

                // Get the remote schema names and file sizes.
                List<Tuple<string, int>> remoteFiles = new();
                foreach (var file in JsonConvert.DeserializeObject<List<GitJson>>(remoteList)
                    .Where(x => x.Name.EndsWith(".json")))
                    remoteFiles.Add(new Tuple<string, int>(file.Name.Replace(".json", string.Empty), file.Size));
                if (remoteFiles.Count <= 0) return false;

                // Compare the local and remote files.
                foreach (var (remoteName, remoteSize) in remoteFiles)
                {
                    if (!force)
                    {
                        var downloadFile = true;
                        MainWindow.Logger.Info($"{remoteName}: Checking ...");
                        foreach (var (localName, localSize) in localFiles)
                            if (string.Equals(remoteName, localName))
                            {
                                // The remote file is found locally. Check if the file size has noticeably changed.
                                downloadFile = !Enumerable.Range(remoteSize - 100, remoteSize + 100)
                                    .Contains(localSize);
                                break;
                            }

                        // If the remote file is not found locally, or the size difference is too great - download the latest version.
                        if (!downloadFile)
                        {
                            MainWindow.Logger.Info($"{remoteName}: No updates...");
                            continue;
                        }
                    }

                    var fileName = $"{remoteName}.json";
                    MainWindow.Logger.Info($"{remoteName}: Downloading latest version...");
                    client.DownloadFile(string.Format(Settings.Default.json_file, fileName), fileName);
                    client.Dispose();

                    // Move the fresh file into the JSON folder, overwriting the previous version.
                    if (File.Exists(fileName))
                        File.Move(fileName, $"JSON/{fileName}", true);

                    restartRequired = true;
                }

                return restartRequired;
            }
            catch (Exception e)
            {
                // Skip this process is there was an error.
                MainWindow.Logger.Error(e.Message);
                Console.WriteLine(e);
                return false;
            }
        }

        public async Task<bool> UpdateAsync()
        {
            try
            {
                return (await Task.WhenAll(HUDList.Select(async x =>
                {
                    var url = string.Format(Settings.Default.json_file, $"{x.Name}.json");
                    MainWindow.Logger.Info($"Requesting {x.Name} from {url}");
                    var response = await Utilities.Fetch(url);
                    if (response == null)
                    {
                        MainWindow.Logger.Info(
                            $"{x.Name}: Received HTTP error, unable to determine whether HUD has been updated!");
                        return false;
                    }

                    var value = JsonConvert.DeserializeObject<HudJson>(response);
                    // Added if !DEBUG to test compare HUDs method
# if !DEBUG
                    File.WriteAllText($"JSON/{x.Name}.json", response);
# endif
                    return !x.TestHUD(new HUD(x.Name, value));
                }))).Contains(true);
            }
            catch (Exception e)
            {
                MainWindow.Logger.Error(e.Message);
                Console.WriteLine(e);
                return false;
            }
        }
    }
}