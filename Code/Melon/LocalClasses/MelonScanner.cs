﻿using Melon.Classes;
using Melon.DisplayClasses;
using Melon.Models;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Pastel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Melon.LocalClasses
{
    /// <summary>
    /// Handles scanning local files and displaying scan progress.
    /// </summary>
    public static class MelonScanner
    {
        // Vars
        public static string CurrentFolder { get; set; }
        public static string CurrentFile { get; set; }
        public static string CurrentStatus { get; set; }
        public static double ScannedFiles { get; set; }
        public static double FoundFiles { get; set; }
        public static bool Indexed { get; set; }
        public static bool endDisplay { get; set; }

        // Scanning Functions
        public static void StartScan()
        {
            Indexed = false;
            MelonUI.ClearConsole();
            if(StateManager.MelonSettings.LibraryPaths.Count() == 0)
            {
                Console.WriteLine("No library paths to search!".Pastel(MelonColor.Error));
                Console.WriteLine("Press any key to continue...".Pastel(MelonColor.BackgroundText));
                Console.ReadKey(intercept: true);
                return;
            }
            foreach (var path in StateManager.MelonSettings.LibraryPaths)
            {
                ScanFolderCounter(path);
            }

            foreach (var path in StateManager.MelonSettings.LibraryPaths)
            {
                ScanFolder(path);
            }
            endDisplay = true;
            CurrentFolder = "N/A";
            CurrentFile = "N/A";
            CurrentStatus = "Scanning Complete";
            IndexCollections();
            DisplayManager.UIExtensions.Clear();
            DisplayManager.MenuOptions.Remove("Scan Progress");
            DisplayManager.MenuOptions.Insert(0, "Library Scanner", MelonScanner.Scan);
        }
        private static void ScanFolderCounter(string path)
        {
            CurrentFolder = path;
            var folders = Directory.GetDirectories(path);
            foreach (var folder in folders)
            {
                ScanFolderCounter(folder);
            }
            FoundFiles = FoundFiles + Directory.GetFiles(path).Count();
        }
        private static void ScanFolder(string path)
        {
            CurrentFolder = path;
            var folders = Directory.GetDirectories(path);
            foreach (var folder in folders)
            {
                ScanFolder(folder);
            }

            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var ArtistCollection = NewMelonDB.GetCollection<Artist>("Artists");
            var AlbumCollection = NewMelonDB.GetCollection<Album>("Albums");
            var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");
            var FailedCollection = NewMelonDB.GetCollection<FailedFiles>("FailedFiles");

            var files = Directory.GetFiles(path);
            foreach(var file in files)
            {
                try
                {
                    CurrentFile = file;
                    var filename = Path.GetFileName(file);
                    if (!filename.EndsWith(".flac") && !filename.EndsWith(".aac") && !filename.EndsWith(".wma") &&
                        !filename.EndsWith(".wav") && !filename.EndsWith(".mp3") && !filename.EndsWith(".m4a"))
                    {
                        ScannedFiles++;
                        continue;
                    }

                    CurrentStatus = "Preparing Artist and Genre tags";

                    // Split artists
                    var fileMetadata = TagLib.File.Create(file);
                    List<string> albumArtists = new List<string>();
                    foreach (var aa in fileMetadata.Tag.AlbumArtists)
                    {
                        // TODO: Make this a setting
                        albumArtists.AddRange(aa.Split(new string[] { ",", ";", "/", "feat.", "ft." }, StringSplitOptions.TrimEntries));
                    }
                    List<string> trackArtists = new List<string>();
                    foreach (var ta in fileMetadata.Tag.Performers)
                    {
                        // TODO: Make this a setting too
                        // ps. 9 in the am pm now
                        trackArtists.AddRange(ta.Split(new string[] { ",", ";", "/", "feat.", "ft." }, StringSplitOptions.TrimEntries));
                    }

                    // Split Genres
                    List<string> trackGenres = new List<string>();
                    foreach (var tg in fileMetadata.Tag.Genres)
                    {
                        // TODO: Make this a setting too 2
                        trackGenres.AddRange(tg.Split(new string[] { ",", ";", "/", }, StringSplitOptions.TrimEntries));
                    }

                    // Conform Genres
                    //foreach (var tg in trackGenres)
                    //{

                    //}

                    // Generate IDs
                    List<ObjectId> ArtistIds = new List<ObjectId>();
                    int num = trackArtists.Count();
                    if(num < albumArtists.Count())
                    {
                        num = albumArtists.Count();
                    }
                    for(int i = 0; i < num; i++)
                    {
                        ArtistIds.Add(ObjectId.GenerateNewId());
                    }
                    ObjectId AlbumId = ObjectId.GenerateNewId();
                    ObjectId TrackId = ObjectId.GenerateNewId();

                    // Add Artists
                    int count = 0;
                    foreach (var artist in trackArtists)
                    {
                        var artistFilter = Builders<Artist>.Filter.Eq("ArtistName", artist);
                        var artistDoc = ArtistCollection.Find(artistFilter).FirstOrDefault();


                        if (artistDoc == null)
                        {
                            // Create Artist Object
                            CurrentStatus = $"Adding {artist}";
                            artistDoc = new Artist()
                            {
                                ArtistName = artist,
                                Bio = "",
                                ArtistPfp = "",
                                _id = ArtistIds[count],
                                Releases = new List<ShortAlbum>(),
                                Genres = new List<string>(),
                                SeenOn = new List<ShortAlbum>(),
                                Tracks = new List<ShortTrack>()
                            };
                            
                            // Check if 
                            //var rQuery = from release in artistDoc.Releases
                            //            where release.AlbumName == fileMetadata.Tag.Album
                            //            select release;
                            //var sQuery = from release in artistDoc.SeenOn
                            //            where release.AlbumName == fileMetadata.Tag.Album
                            //            select release;

                            // Add the first release
                            if (albumArtists.Contains(artist))
                            {
                                artistDoc.Releases.Add(new ShortAlbum() { _id = AlbumId, AlbumName = fileMetadata.Tag.Album });
                            }
                            else
                            {
                                artistDoc.SeenOn.Add(new ShortAlbum() { _id = AlbumId, AlbumName = fileMetadata.Tag.Album }) ;
                            }

                            // Add the first track
                            var sTrack = new ShortTrack()
                            {
                                _id = TrackId,
                                AlbumName = fileMetadata.Tag.Album,
                                Duration = fileMetadata.Properties.Duration.ToString(),
                                Position = fileMetadata.Tag.Track,
                                TrackName = fileMetadata.Tag.Title,
                                TrackArtists = new List<ShortArtist>()
                            };
                            for(int i = 0; i < trackArtists.Count(); i++)
                            {
                                sTrack.TrackArtists.Add(new ShortArtist() { _id = ArtistIds[i], ArtistName = trackArtists[i] });
                            }
                            artistDoc.Tracks.Add(sTrack);

                            ArtistCollection.InsertOne(artistDoc);
                        }
                        else
                        {
                            CurrentStatus = $"Updating {artist}";
                            ArtistIds[count] = artistDoc._id;
                            
                            
                            if (albumArtists.Contains(artist))
                            {
                                var rQuery = from release in artistDoc.Releases
                                             where release.AlbumName == fileMetadata.Tag.Album
                                             select release;
                                if (rQuery.Count() == 0)
                                {
                                    var arrayUpdateRelease = Builders<Artist>.Update.Push("Releases", new ShortAlbum() { _id = AlbumId, AlbumName = fileMetadata.Tag.Album });
                                    ArtistCollection.UpdateOne(artistFilter, arrayUpdateRelease);
                                }
                            }
                            else
                            {
                                var sQuery = from release in artistDoc.SeenOn
                                             where release.AlbumName == fileMetadata.Tag.Album
                                             select release;
                                if (sQuery.Count() == 0)
                                {
                                    var arrayUpdateRelease = Builders<Artist>.Update.Push("SeenOn", new ShortAlbum() { _id = AlbumId, AlbumName = fileMetadata.Tag.Album });
                                    ArtistCollection.UpdateOne(artistFilter, arrayUpdateRelease);
                                }
                            }

                            var tQuery = from track in artistDoc.Tracks
                                         where track.TrackName == fileMetadata.Tag.Title
                                         select track;
                            if (tQuery.Count() == 0)
                            {
                                var sTrack = new ShortTrack()
                                {
                                    _id = TrackId,
                                    AlbumName = fileMetadata.Tag.Album,
                                    Duration = fileMetadata.Properties.Duration.ToString(),
                                    Position = fileMetadata.Tag.Track,
                                    TrackName = fileMetadata.Tag.Title,
                                    TrackArtists = new List<ShortArtist>()
                                };
                                for(int i = 0; i < trackArtists.Count(); i++)
                                {
                                    sTrack.TrackArtists.Add(new ShortArtist() { _id = ArtistIds[i], ArtistName = trackArtists[i] });
                                }

                                var arrayUpdateTracks = Builders<Artist>.Update.Push("Tracks", sTrack);
                                ArtistCollection.UpdateOne(artistFilter, arrayUpdateTracks);
                            }
                            foreach (var genre in trackGenres)
                            {
                                if (!artistDoc.Genres.Contains(genre))
                                {
                                    var arrayUpdateGenres = Builders<Artist>.Update.Push("Genres", genre);
                                    ArtistCollection.UpdateOne(artistFilter, arrayUpdateGenres);
                                }
                            }

                        }

                        // Add Release
                        var albumFilter = Builders<Album>.Filter.Eq("AlbumName", fileMetadata.Tag.Album);
                        albumFilter = albumFilter & Builders<Album>.Filter.AnyStringIn("AlbumArtists.ArtistName", fileMetadata.Tag.FirstPerformer);
                        var albumDoc = AlbumCollection.Find(albumFilter).FirstOrDefault();

                        if (albumDoc == null)
                        {
                            CurrentStatus = $"Adding {fileMetadata.Tag.Album}";
                            Album album = new Album();
                            album._id = AlbumId;
                            album.AlbumName = fileMetadata.Tag.Album;
                            try { album.Bio = ""; } catch (Exception) { }
                            try { album.TotalDiscs = fileMetadata.Tag.DiscCount; } catch (Exception) { }
                            try { album.TotalTracks = fileMetadata.Tag.TrackCount; } catch (Exception) { }
                            try { album.Publisher = fileMetadata.Tag.Publisher; } catch (Exception) { }
                            try { album.ReleaseStatus = fileMetadata.Tag.MusicBrainzReleaseStatus; } catch (Exception) { }
                            try { album.ReleaseType = fileMetadata.Tag.MusicBrainzReleaseType; } catch (Exception) { }
                            album.Tracks = new List<ShortTrack>();
                            album.AlbumArtists = new List<ShortArtist>();
                            album.AlbumGenres = new List<string>();
                            albumDoc = album;

                            for(int i = 0; i < albumArtists.Count(); i++)
                            {
                                try
                                {
                                    albumDoc.AlbumArtists.Add(new ShortArtist() { ArtistName = albumArtists[i], _id = ArtistIds[i] });
                                }
                                catch(Exception e)
                                {

                                }
                            }
                            foreach (var genre in trackGenres)
                            {
                                albumDoc.AlbumGenres.Add(genre);
                            }
                            var sTrack = new ShortTrack()
                            {
                                _id = TrackId,
                                AlbumName = fileMetadata.Tag.Album,
                                Duration = fileMetadata.Properties.Duration.ToString(),
                                Position = fileMetadata.Tag.Track,
                                TrackName = fileMetadata.Tag.Title,
                                TrackArtists = new List<ShortArtist>()
                            };
                            for(int i = 0; i < trackArtists.Count(); i++)
                            {
                                sTrack.TrackArtists.Add(new ShortArtist() { _id = ArtistIds[i], ArtistName = trackArtists[i] });
                            }
                            albumDoc.Tracks.Add(sTrack);
                            try
                            {
                                AlbumCollection.InsertOne(albumDoc);
                            }
                            catch(Exception e)
                            {

                            }
                        }
                        else
                        {
                            for (int i = 0; i < trackArtists.Count(); i++)
                            {
                                var aQuery = from release in albumDoc.AlbumArtists
                                             where release.ArtistName == trackArtists[i]
                                             select release;
                                if (aQuery.Count() == 0)
                                {
                                    var arrayUpdateRelease = Builders<Album>.Update.Push("AlbumArtists", new ShortArtist() { _id = ArtistIds[i], ArtistName = trackArtists[i] });
                                    AlbumCollection.UpdateOne(albumFilter, arrayUpdateRelease);
                                }
                            }
                            foreach (var genre in trackGenres)
                            {
                                if (!albumDoc.AlbumGenres.Contains(genre))
                                {
                                    var arrayUpdateGenres = Builders<Album>.Update.Push("AlbumGenres", genre);
                                    AlbumCollection.UpdateOne(albumFilter, arrayUpdateGenres);
                                }
                            }
                            var tQuery = from release in albumDoc.Tracks
                                         where release.TrackName == fileMetadata.Tag.Title
                                         select release;
                            if (tQuery.Count() == 0)
                            {
                                var sTrack = new ShortTrack()
                                {
                                    _id = AlbumId,
                                    AlbumName = fileMetadata.Tag.Album,
                                    Duration = fileMetadata.Properties.Duration.ToString(),
                                    Position = fileMetadata.Tag.Track,
                                    TrackName = fileMetadata.Tag.Title,
                                    TrackArtists = new List<ShortArtist>()
                                };
                                for (int i = 0; i < trackArtists.Count(); i++)
                                {
                                    sTrack.TrackArtists.Add(new ShortArtist() { _id = ArtistIds[i], ArtistName = trackArtists[i] });
                                }
                                var arrayUpdateTracks = Builders<Album>.Update.Push("Tracks", sTrack);
                                AlbumCollection.UpdateOne(albumFilter, arrayUpdateTracks);
                            }

                        }

                        // Add Track
                        var trackFilter = Builders<Track>.Filter.Empty;

                        trackFilter = trackFilter & Builders<Track>.Filter.Eq("Path", file);
                        trackFilter = trackFilter & Builders<Track>.Filter.Eq("AlbumName", fileMetadata.Tag.Album);
                        trackFilter = trackFilter & Builders<Track>.Filter.Eq("TrackName", fileMetadata.Tag.Title);
                        var trackDoc = TracksCollection.Find(trackFilter).FirstOrDefault();
                        if (trackDoc == null)
                        {
                            Track track = new Track();
                            track._id = TrackId;
                            try { track.TrackName = fileMetadata.Tag.Title; } catch (Exception) { }
                            try { track.AlbumName = fileMetadata.Tag.Album; } catch (Exception) { }
                            try { track.Path = file; } catch (Exception) { }
                            try { track.Position = fileMetadata.Tag.Track; } catch (Exception) { }
                            try { track.Format = Path.GetExtension(file); } catch (Exception) { }
                            try { track.Bitrate = fileMetadata.Properties.AudioBitrate.ToString(); } catch (Exception) { }
                            try { track.SampleRate = fileMetadata.Properties.AudioSampleRate.ToString(); } catch (Exception) { }
                            try { track.Channels = fileMetadata.Properties.AudioChannels.ToString(); } catch (Exception) { }
                            try { track.BitsPerSample = fileMetadata.Properties.BitsPerSample.ToString(); } catch (Exception) { }
                            try { track.Disc = fileMetadata.Tag.Disc; } catch (Exception) { }
                            try { track.MusicBrianzID = fileMetadata.Tag.MusicBrainzTrackId; } catch (Exception) { }
                            try { track.ISRC = fileMetadata.Tag.ISRC; } catch (Exception) { }
                            try { track.Year = fileMetadata.Tag.Year.ToString(); } catch (Exception) { }
                            try { track.Duration = fileMetadata.Properties.Duration.ToString(); } catch (Exception) { }
                            try { track.TrackArtists = new List<ShortArtist>(); } catch (Exception) { }
                            try { track.TrackGenres = new List<string>(); } catch (Exception) { }

                            for(int i = 0; i < trackArtists.Count(); i++)
                            {
                                track.TrackArtists.Add(new ShortArtist() { _id = ArtistIds[i], ArtistName = trackArtists[i] });
                            }
                            foreach (var genre in trackGenres)
                            {
                                track.TrackGenres.Add(genre);
                            }
                            TracksCollection.InsertOne(track);
                        }
                        if (!Indexed)
                        {
                            IndexCollections();
                        }
                        count++;
                    }
                }
                catch (Exception e)
                {
                    var fileFilter = Builders<FailedFiles>.Filter.Eq("Type", "Failed");
                    var fileDoc = FailedCollection.Find(fileFilter).FirstOrDefault();
                    if(fileDoc == null)
                    {
                        FailedFiles failed = new FailedFiles();
                        failed.Type = "Failed";
                        failed.Paths = new List<string>
                        {
                            file
                        };
                        FailedCollection.InsertOne(failed);

                    }
                    else
                    {
                        var arrayUpdateFailed = Builders<FailedFiles>.Update.Push("Paths", file);
                        FailedCollection.UpdateOne(fileFilter, arrayUpdateFailed);
                    }
                }
                ScannedFiles++;
            }
            
        }
        public static void IndexCollections()
        {
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var indexOptions = new CreateIndexOptions { Background = true  }; 

            var trackIndexKeysDefinition = Builders<BsonDocument>.IndexKeys.Ascending("TrackName");
            var TracksCollection = NewMelonDB.GetCollection<BsonDocument>("Tracks");
            var trackIndexModel = new CreateIndexModel<BsonDocument>(trackIndexKeysDefinition, indexOptions);
            TracksCollection.Indexes.CreateOne(trackIndexModel);

            var artistIndexKeysDefinition = Builders<BsonDocument>.IndexKeys.Ascending("ArtistName");
            var ArtistCollection = NewMelonDB.GetCollection<BsonDocument>("Artists");
            var artistIndexModel = new CreateIndexModel<BsonDocument>(trackIndexKeysDefinition, indexOptions);
            ArtistCollection.Indexes.CreateOne(artistIndexModel);

            var albumIndexKeysDefinition = Builders<BsonDocument>.IndexKeys.Ascending("AlbumName");
            var AlbumCollection = NewMelonDB.GetCollection<BsonDocument>("Albums");
            var albumIndexModel = new CreateIndexModel<BsonDocument>(trackIndexKeysDefinition, indexOptions);
            AlbumCollection.Indexes.CreateOne(albumIndexModel);
            


        }

        // UI
        public static void Scan()
        {
            // Title
            MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Scan" });

            // Description
            Console.WriteLine($"This will start a scan of all saved paths and their subdirectories.");
            Console.WriteLine($"It may {"take awhile".Pastel(MelonColor.Highlight)}, but this will run in the background (As long as this application stays open!)");
            Console.WriteLine($"You can check progress of it anytime from the {"Main Menu".Pastel(MelonColor.Melon)}");
            Console.WriteLine($"As items are scanned in, they will become available in the {"Library View".Pastel(MelonColor.Highlight)}");
            Console.WriteLine($"Ready to Start?");
            var input = MelonUI.OptionPicker(new List<string>() { "Yes", "No" });
            switch (input)
            {
                case "Yes":
                    Thread scanThread = new Thread(MelonScanner.StartScan);
                    scanThread.Start();
                    DisplayManager.UIExtensions.Add(() => { Console.WriteLine("Library scan started!".Pastel(MelonColor.Highlight)); DisplayManager.UIExtensions.RemoveAt(0); });
                    DisplayManager.MenuOptions.Remove("Library Scanner");
                    DisplayManager.MenuOptions.Insert(0, "Scan Progress", ScanProgressView);
                    ScanProgressView();
                    break;
                case "No":
                    return;
            }
        }
        public static void ScanProgressView()
        {
            // Title
            Console.CursorVisible = false;
            MelonUI.ClearConsole();
            MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Scanner Progress" });

            endDisplay = false;
            int sLeft = Console.CursorLeft;
            int sTop = Console.CursorTop;
            Thread DisplayThread = new Thread(() =>
            {
                int x = Console.WindowWidth;
                while (!endDisplay)
                {
                    if (endDisplay)
                    {
                        return;
                    }
                    if (x != Console.WindowWidth)
                    {
                        x = Console.WindowWidth;
                        MelonUI.ClearConsole();
                        MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Scanner Progress" });
                    }
                    try
                    {
                        string controls = $"Ctrls: Esc";
                        int conX = Console.WindowWidth - controls.Length - 2;
                        Console.CursorLeft = conX;
                        Console.CursorTop = sTop;
                        Console.Write(controls.Pastel(MelonColor.BackgroundText));
                        Console.CursorTop = sTop;
                        Console.CursorLeft = sLeft;
                        Console.WriteLine($"Scanned {MelonScanner.ScannedFiles.ToString().Pastel(MelonColor.Melon)} // {MelonScanner.FoundFiles.ToString().Pastel(MelonColor.Melon)} Found");
                        MelonUI.DisplayProgressBar(MelonScanner.ScannedFiles, MelonScanner.FoundFiles, '#', '-');
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        string msg = $"Current Folder: {MelonScanner.CurrentFolder}";
                        int max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.WriteLine(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        msg = $"Current File: {MelonScanner.CurrentFile}";
                        max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.WriteLine(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        msg = $"Status: {MelonScanner.CurrentStatus}";
                        max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.WriteLine(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.WriteLine(new string(' ', Console.WindowWidth));
                        Console.WriteLine(new string(' ', Console.WindowWidth));
                    }
                    catch (Exception)
                    {

                    }
                }
            });
            DisplayThread.Start();
            while (true)
            {
                // Get Input
                var input = Console.ReadKey(intercept: true);
                if (input.Key == ConsoleKey.Escape)
                {
                    endDisplay = true;
                    Thread.Sleep(100);
                    return;
                }
            }
        }
    }
}
