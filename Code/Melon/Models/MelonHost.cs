﻿using Melon.Classes;
using Melon.DisplayClasses;
using Melon.LocalClasses;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Melon.Models;
using Melon.Interface;
using Melon.Types;

namespace Melon.PluginModels
{
    public class MelonHost : IHost
    {
        public string Version => "1.2.0";
        public IMelonAPI Api => new API();
        public IMelonAPI MelonAPI => new API();
        public IStorageAPI Storage => new StorageAPI();
        public IMelonScanner MelonScanner => new Scanner();
        public IStateManager StateManager => new State();
        public IDisplayManager DisplayManager => new Display();
        public IMelonUI MelonUI => new UI();
        public ISettingsUI SettingsUI => new SettingsMenu();
        
    }
    public class API : IMelonAPI
    {
        public List<Track> ShuffleTracks(List<Track> tracks, string UserId, ShuffleType type, bool fullRandom = false, bool enableTrackLinks = true)
        {
            return MelonAPI.ShuffleTracks(tracks, UserId, type, fullRandom, enableTrackLinks);
        }
    }
    public class StorageAPI : IStorageAPI
    {
        public T LoadConfigFile<T>(string filename, string[] protectedProperties, out bool converted)
        {
            return Storage.LoadConfigFile<T>(filename, protectedProperties, out converted);
        }
        public void SaveConfigFile<T>(string filename, T config, string[] protectedProperties)
        {
            Storage.SaveConfigFile(filename, config, protectedProperties);
        }
    }
    public class Scanner : IMelonScanner
    {
        public string CurrentFolder
        {
            get
            {
                return MelonScanner.CurrentFolder;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.CurrentFolder = value;
                }
            }
        }
        public string CurrentFile
        {
            get
            {
                return MelonScanner.CurrentFile;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.CurrentFile = value;
                }
            }
        }
        public string CurrentStatus
        {
            get
            {
                return MelonScanner.CurrentStatus;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.CurrentStatus = value;
                }
            }
        }
        public double ScannedFiles
        {
            get
            {
                return MelonScanner.ScannedFiles;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.ScannedFiles = value;
                }
            }
        }
        public double FoundFiles
        {
            get
            {
                return MelonScanner.FoundFiles;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.FoundFiles = value;
                }
            }
        }
        public long averageMilliseconds
        {
            get
            {
                return MelonScanner.averageMilliseconds;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.averageMilliseconds = value;
                }
            }
        }
        public bool Indexed
        {
            get
            {
                return MelonScanner.Indexed;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.Indexed = value;
                }
            }
        }
        public bool endDisplay
        {
            get
            {
                return MelonScanner.endDisplay;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.endDisplay = value;
                }
            }
        }
        public bool Scanning
        {
            get
            {
                return MelonScanner.Scanning;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.Scanning = value;
                }
            }
        }
        public void StartScan(bool skip)
        {
            MelonScanner.StartScan(skip);
        }
        public void UpdateCollections()
        {
            MelonScanner.UpdateCollections();
        }
        public void ResetDB()
        {
            MelonScanner.ResetDb();
        }
    }
    public class State : IStateManager
    {
        public string melonPath
        {
            get
            {
                return StateManager.melonPath;
            }
        }
        public MongoClient DbClient
        {
            get
            {
                return StateManager.DbClient;
            }
        }
        public ShortSettings MelonSettings
        {
            get
            {
                return new ShortSettings(StateManager.MelonSettings);
            }
        }
        public Flags MelonFlags
        {
            get
            {
                return StateManager.MelonFlags;
            }
        }
        public ResourceManager StringsManager
        {
            get
            {
                return StateManager.StringsManager;
            }
        }
        public List<IPlugin> Plugins
        {
            get
            {
                return StateManager.Plugins;
            }
        }
        public Dictionary<string, string> LaunchArgs
        {
            get
            {
                return StateManager.LaunchArgs;
            }
        }

        public byte[] GetDefaultImage()
        {
            return StateManager.GetDefaultImage();
        }
    }
    public class Display : IDisplayManager
    {
        public OrderedDictionary MenuOptions
        {
            get
            {
                return DisplayManager.MenuOptions;
            }
            set
            {
                if (value == null)
                {
                    DisplayManager.MenuOptions = value;
                }
            }
        }
        public OrderedDictionary UIExtensions
        {
            get
            {
                return DisplayManager.UIExtensions;
            }
            set
            {
                if (value == null)
                {
                    DisplayManager.UIExtensions = value;
                }
            }
        }
    }
    public class UI : IMelonUI
    {
        public void BreadCrumbBar(List<string> list)
        {
            MelonUI.BreadCrumbBar(list);
        }
        public void ClearConsole()
        {
            MelonUI.ClearConsole();
        }
        public void ClearConsole(int left, int top, int width, int height)
        {
            MelonUI.ClearConsole(left, top, width, height);
        }
        public Color ColorPicker(Color CurColor)
        {
            return MelonUI.ColorPicker(CurColor);
        }
        public string HiddenInput()
        {
            return MelonUI.HiddenInput();
        }
        public void DisplayProgressBar(double count, double max, char foreground, char background)
        {
            MelonUI.DisplayProgressBar(count, max, foreground, background);
        }
        public void ShowIndeterminateProgress()
        {
            MelonUI.ShowIndeterminateProgress();
        }
        public void HideIndeterminateProgress()
        {
            MelonUI.HideIndeterminateProgress();
        }
        public bool endOptionsDisplay
        {
            get
            {
                return MelonUI.endOptionsDisplay;
            }
            set
            {
                if (value == null)
                {
                    MelonUI.endOptionsDisplay = value;
                }
            }
        }
        public string OptionPicker(List<string> Choices)
        {
            return MelonUI.OptionPicker(Choices);
        }
        public string StringInput(bool UsePred, bool AutoCorrect, bool FreeInput, bool ShowChoices, List<string> Choices = null)
        {
            return MelonUI.StringInput(UsePred, AutoCorrect, FreeInput, ShowChoices, Choices);
        }

        public void ChecklistDisplayToggle()
        {
            ChecklistUI.ChecklistDislayToggle();
        }

        public void SetChecklistItems(string[] list)
        {
            ChecklistUI.SetChecklistItems(list);
        }

        public void InsertInChecklist(string item, int place, bool check)
        {
            ChecklistUI.InsertInChecklist(item, place, check);
        }

        public void UpdateChecklist(int place, bool check)
        {
            ChecklistUI.UpdateChecklist(place, check);
        }
    }
    public class SettingsMenu : ISettingsUI
    {
        public OrderedDictionary MenuOptions
        {
            get
            {
                return SettingsUI.MenuOptions;
            }
            set
            {
                if (value == null)
                {
                    SettingsUI.MenuOptions = value;
                }
            }
        }
    }
}
