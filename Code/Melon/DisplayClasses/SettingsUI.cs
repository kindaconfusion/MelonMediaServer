﻿using Melon.Classes;
using Melon.LocalClasses;
using Melon.Models;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Pastel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Melon.LocalClasses.StateManager;

namespace Melon.DisplayClasses
{
    /// <summary>
    /// Contains all the UI for adjusting settings.
    /// </summary>
    public static class SettingsUI
    {
        public static void Settings()
        {
            // Used to stay in settings until back is selected
            bool LockUI = true;

            // Check if settings are loaded and if not load them in. 
            if (StateManager.MelonSettings == null)
            {
                MelonUI.ClearConsole();
                Console.WriteLine("Loading Settings...".Pastel(MelonColor.Text));
                if (!Directory.Exists(melonPath))
                {
                    Directory.CreateDirectory(melonPath);
                }

                if (!System.IO.File.Exists($"{melonPath}/Settings.mln"))
                {
                    // If Settings don't exist, add default values.
                    MelonSettings = new Settings()
                    {
                        MongoDbConnectionString = "mongodb://localhost:27017",
                        LibraryPaths = new List<string>()
                    };
                    SaveSettings();
                    DisplayManager.UIExtensions.Add(SetupUI.Display);
                }
                else
                {
                    LoadSettings();
                }
            }


            while (LockUI)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings" });

                // Input
                Dictionary<string, Action> MenuOptions = new Dictionary<string, Action>()
                {
                    { "Back" , () => { LockUI = false; } },
                    //{ "Edit Users", UserSettings },
                    { "Edit MongoDB Connection", MongoDBSettings },
                    { "Edit Library Paths" , LibraryPathSettings },
                    { "Edit Listening URL", ChangeListeningURL },
                    { "Configure HTTPS", HTTPSSetup },
                    { "Edit Colors " , ChangeMelonColors }
                };
                var choice = MelonUI.OptionPicker(MenuOptions.Keys.ToList());
                MenuOptions[choice]();
            }
        }
        private static void HTTPSSetup()
        {
            // Check if ssl is setup already
            var config = Security.GetSSLConfig();
            if(config.Key != "")
            {
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Configure HTTPS" });
                Console.WriteLine($"Changing this setting will require a server restart!".Pastel(MelonColor.Highlight));
                Console.WriteLine($"SSL is already configured, would you like to disabled or edit it?".Pastel(MelonColor.Text));
                var opt = MelonUI.OptionPicker(new List<string>() { "Back", "Disable SSL", "Edit SSL Config"});
                switch (opt) 
                {
                    case "Back":
                        return;
                    case "Disable SSL":
                        Security.SetSSLConfig("", "");
                        Security.SaveSSLConfig();
                        return;
                    case "Edit SSL Config":
                        break;
                }

            }

            bool result = true;
            while (true)
            {
                // Get the Path to the pfx
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Configure HTTPS" });
                Console.WriteLine($"Changing this setting will require a server restart!".Pastel(MelonColor.Highlight));
                Console.WriteLine($"Setting up HTTPS requires a valid SSL Certificate.".Pastel(MelonColor.Text));
                Console.WriteLine($"Please enter the path to your {".pfx".Pastel(MelonColor.Highlight)} certificate (or enter nothing to cancel):".Pastel(MelonColor.Text));
                if (!result)
                {
                    Console.WriteLine($"[Invalid Cert or Password]".Pastel(MelonColor.Error));
                }
                result = false;

                Console.Write("> ".Pastel(MelonColor.Text));
                string pathToCert = Console.ReadLine();
                if (pathToCert == "")
                {
                    return;
                }

                // Get the password to the cert
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Configure HTTPS" });
                Console.WriteLine($"Next, enter the password to your SSL Certificate (or enter nothing to cancel):".Pastel(MelonColor.Text));

                Console.Write("> ".Pastel(MelonColor.Text));
                string password = MelonUI.HiddenInput();
                if (password == "")
                {
                    return;
                }

                // Check if cert and password are valid
                try
                {
                    var certificate = new X509Certificate2(pathToCert, password);
                    result = true;
                }
                catch (Exception)
                {
                    result = false;
                }


                if (result)
                {
                    // Set and Save new conn string
                    Security.SetSSLConfig(pathToCert, password);
                    Security.SaveSSLConfig();
                    break;
                }

            }

        }
        private static void ChangeListeningURL()
        {
            bool result = true;
            while (true)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Listening URL" });

                // Description
                Console.WriteLine($"Changing this setting will require a server restart!".Pastel(MelonColor.Highlight));
                Console.WriteLine($"Current URL: {StateManager.MelonSettings.ListeningURL.Pastel(MelonColor.Melon)}".Pastel(MelonColor.Text));
                Console.WriteLine($"(Enter new urls separated by \";\" or nothing to keep the current string)".Pastel(MelonColor.Text));
                if (!result)
                {
                    Console.WriteLine($"[Invalid URL]".Pastel(MelonColor.Error));
                }
                result = false;

                // Get New URL
                Console.Write("> ".Pastel(MelonColor.Text));
                string input = Console.ReadLine();
                if (input == "")
                {
                    return;
                }

                foreach(var url in input.Split(";"))
                {
                    Regex UrlWithWildcardRegex = new Regex(@"^(https?:\/\/)([\w*]+\.)*[\w*]+(:\d+)?(\/[\w\/]*)*(\?.*)?(#.*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    result = UrlWithWildcardRegex.IsMatch(url);

                    if (result == false)
                    {
                        break;
                    }
                }

                if (result)
                {
                    // Set and Save new conn string
                    StateManager.MelonSettings.ListeningURL = input;
                    StateManager.SaveSettings();
                    break;
                }

            }

        }
        private static void MongoDBSettings()
        {
            bool check = true;
            while (true)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "MongoDB" });

                // Description
                Console.WriteLine($"Current MongoDB connection string: {StateManager.MelonSettings.MongoDbConnectionString.Pastel(MelonColor.Melon)}".Pastel(MelonColor.Text));
                Console.WriteLine($"(Enter a new string or nothing to keep the current string)".Pastel(MelonColor.Text));
                if (!check)
                {
                    Console.WriteLine($"[Couldn't connect to server, try again]".Pastel(MelonColor.Error));
                }
                check = false;

                // Get New MongoDb Connection String
                Console.Write("> ".Pastel(MelonColor.Text));
                string input = Console.ReadLine();
                if (input == "")
                {
                    return;
                }

                check = StateManager.CheckMongoDB(input);
                if (check)
                {
                    // Set and Save new conn string
                    StateManager.MelonSettings.MongoDbConnectionString = input;
                    StateManager.SaveSettings();
                    if (DisplayManager.MenuOptions.Count < 5)
                    {
                        DisplayManager.MenuOptions.Clear();
                        DisplayManager.MenuOptions.Add("Full Scan", MelonScanner.Scan);
                        DisplayManager.MenuOptions.Add("Short Scan", MelonScanner.ScanShort);
                        DisplayManager.MenuOptions.Add("Reset DB", MelonScanner.ResetDB);
                        DisplayManager.MenuOptions.Add("Settings", SettingsUI.Settings);
                        DisplayManager.MenuOptions.Add("Exit", () => Environment.Exit(0));
                    }
                    break;
                }

            }

        }
        private static void LibraryPathSettings()
        {
            while (true)
            {
                // title
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Libraries" });
                Console.WriteLine($"(Select a path to delete it)".Pastel(MelonColor.Text));

                // Get paths
                List<string> NewPaths = new List<string>();
                NewPaths.Add("Back");
                NewPaths.Add("Add New Path");
                NewPaths.AddRange(StateManager.MelonSettings.LibraryPaths);

                // Add Options

                // Get Selection
                string input = MelonUI.OptionPicker(NewPaths);
                if (input == "Add New Path")
                {
                    // For showing error color when directory doesn't exist
                    bool showPathError = false;
                    while (true)
                    {
                        // Title
                        MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Libraries", "Add Library" });

                        // Description and Input UI
                        if (showPathError)
                        {
                            Console.WriteLine("Invalid Path, Please try again (Or enter nothing to quit)".Pastel(MelonColor.Error));
                            showPathError = false;
                        }
                        Console.WriteLine("Enter a new library path:".Pastel(MelonColor.Text));
                        Console.Write($"> ".Pastel(MelonColor.Text));

                        // Get Path
                        string path = Console.ReadLine();
                        if (path == "")
                        {
                            break;
                        }

                        // If path is valid, add it to library paths and save
                        if (!Directory.Exists(path))
                        {
                            showPathError = true;
                        }
                        else
                        {
                            StateManager.MelonSettings.LibraryPaths.Add(path);
                            StateManager.SaveSettings();
                            break;
                        }
                    }
                }
                else if (input == "Back")
                {
                    // Leave
                    return;
                }
                else
                {
                    // Remove selected library path
                    StateManager.MelonSettings.LibraryPaths.Remove(input);
                    StateManager.SaveSettings();
                }
            }
        }
        public static void ChangeMelonColors()
        {
            while (true)
            {
                Dictionary<string, int> ColorMenuOptions = new Dictionary<string, int>()
                {
                    { $"Back" , 7 },
                    { $"Set the {"normal text color".Pastel(MelonColor.Text)}", 0 },
                    { $"Set the {"shaded text color".Pastel(MelonColor.ShadedText)}", 1 },
                    { $"Set the {"background text color".Pastel(MelonColor.BackgroundText)}", 2 },
                    { $"Set the {"Melon title color".Pastel(MelonColor.Melon)}", 3 },
                    { $"Set the {"highlight color".Pastel(MelonColor.Highlight)}", 4 },
                    { $"Set the {"error color".Pastel(MelonColor.Error)}", 5 },
                    { $"Set all colors back to their defaults", 6 }
                };
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Colors" });
                Console.WriteLine("Choose a color to change:".Pastel(MelonColor.Text));
                var choice = MelonUI.OptionPicker(ColorMenuOptions.Keys.ToList());
                Thread.Sleep(100);
                Color newClr = new Color();

                Console.SetCursorPosition(0, 1);

                switch (ColorMenuOptions[choice])
                {
                    case 0:
                        newClr = MelonUI.ColorPicker(MelonColor.Text);
                        MelonColor.Text = newClr;
                        StateManager.MelonSettings.Text = newClr;
                        break;
                    case 1:
                        newClr = MelonUI.ColorPicker(MelonColor.ShadedText);
                        MelonColor.ShadedText = newClr;
                        StateManager.MelonSettings.ShadedText = newClr;
                        break;
                    case 2:
                        newClr = MelonUI.ColorPicker(MelonColor.BackgroundText);
                        MelonColor.BackgroundText = newClr;
                        StateManager.MelonSettings.BackgroundText = newClr;
                        break;
                    case 3:
                        newClr = MelonUI.ColorPicker(MelonColor.Melon);
                        MelonColor.Melon = newClr;
                        StateManager.MelonSettings.Melon = newClr;
                        break;
                    case 4:
                        newClr = MelonUI.ColorPicker(MelonColor.Highlight);
                        MelonColor.Highlight = newClr;
                        StateManager.MelonSettings.Highlight = newClr;
                        break;
                    case 5:
                        newClr = MelonUI.ColorPicker(MelonColor.Error);
                        MelonColor.Error = newClr;
                        StateManager.MelonSettings.Error = newClr;
                        break;
                    case 6:
                        MelonColor.SetDefaults();
                        break;
                    case 7:
                        return;
                }
                StateManager.SaveSettings();
            }
        }
    }
}
