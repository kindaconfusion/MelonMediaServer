﻿using Melon.Classes;
using Melon.DisplayClasses;
using Melon.LocalClasses;
using Microsoft.Owin.Hosting;
using Pastel;
using System.Text;

Console.ForegroundColor = ConsoleColor.White;
Console.OutputEncoding = Encoding.UTF8;

// Melon Startup
StateManager.Init();

// UI Startup
DisplayManager.DisplayHome();