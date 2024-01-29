using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using System.Data;
using MongoDB.Driver;
using Melon.LocalClasses;
using System.Diagnostics;
using Melon.Models;
using System.Security.Claims;
using ATL.Playlist;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/playlists")]
    public class PlaylistsController : ControllerBase
    {
        private readonly ILogger<PlaylistsController> _logger;

        public PlaylistsController(ILogger<PlaylistsController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "Admin,User")]
        [HttpPost("create")]
        public ObjectResult CreatePlaylist(string name, string description = "", List<string> trackIds = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var userName = User.Identity.Name;

            Playlist playlist = new Playlist();
            playlist._id = new MelonId(ObjectId.GenerateNewId());
            playlist.PlaylistId = playlist._id.ToString();
            playlist.Name = name;
            playlist.TrackCount = 0;
            playlist.Owner = userName;
            playlist.Editors = new List<string>();
            playlist.Viewers = new List<string>();
            playlist.PublicEditing = false;
            playlist.PublicViewing = false;
            playlist.Description = description;
            playlist.ArtworkPath = "";
            playlist.Tracks = new List<ShortTrack>();
            //var str = queue._id.ToString();
            var pFilter = Builders<Playlist>.Filter.Eq(x=>x.PlaylistId, playlist.PlaylistId);
            if(trackIds == null)
            {
                PCollection.InsertOne(playlist);
                return new ObjectResult(playlist._id.ToString()) { StatusCode = 200 };
            }
            foreach(var id in trackIds)
            {
                var trackFilter = Builders<Track>.Filter.Eq("TrackId", id);
                var trackDoc = TCollection.Find(trackFilter).ToList();
                if(trackDoc.Count != 0)
                {
                    playlist.TrackCount++;
                    playlist.Tracks.Add(new ShortTrack(trackDoc[0]));
                }
            
            }
            PCollection.InsertOne(playlist);


            return new ObjectResult(playlist._id.ToString()) { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin,User")]
        [HttpPost("add-tracks")]
        public ObjectResult AddToPlaylist(string id, List<string> trackIds)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var userName = User.Identity.Name;

            //var str = queue._id.ToString();
            var pFilter = Builders<Playlist>.Filter.Eq("PlaylistId", id);
            var playlists = PCollection.Find(pFilter).ToList();
            if(playlists.Count == 0)
            {
                return new ObjectResult("Playlist Not Found") { StatusCode = 404 };
            }
            var playlist = playlists[0];

            if(playlist.PublicEditing == false)
            {
                if(playlist.Owner != userName && !playlist.Editors.Contains(userName))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            foreach (var tid in trackIds)
            {
                var trackFilter = Builders<Track>.Filter.Eq("TrackId", tid);
                var trackDoc = TCollection.Find(trackFilter).ToList();
                if (trackDoc.Count != 0)
                {
                    playlist.Tracks.Add(new ShortTrack(trackDoc[0]));
                    playlist.TrackCount++;
                }
            }
            PCollection.ReplaceOne(pFilter, playlist);

            return new ObjectResult("Tracks added") { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin,User")]
        [HttpPost("remove-tracks")]
        public ObjectResult RemoveFromPlaylist(string id, List<string> trackIds)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var userName = User.Identity.Name;

            //var str = queue._id.ToString();
            var pFilter = Builders<Playlist>.Filter.Eq("PlaylistId", id);
            var playlists = PCollection.Find(pFilter).ToList();
            if (playlists.Count == 0)
            {
                return new ObjectResult("Playlist Not Found") { StatusCode = 404 };
            }
            var playlist = playlists[0];

            if (playlist.PublicEditing == false)
            {
                if (playlist.Owner != userName && !playlist.Editors.Contains(userName))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            foreach (var tid in trackIds)
            {
                var query = from track in playlist.Tracks
                            where track.TrackId == tid
                            select track;
                if (query.Count() != 0)
                {
                    playlist.Tracks.Remove(query.ToList()[0]);
                    playlist.TrackCount--;
                }
            }
            PCollection.ReplaceOne(pFilter, playlist);

            return new ObjectResult("Tracks removed") { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin, User")]
        [HttpPost("update")]
        public ObjectResult updatePlaylist(ShortPlaylist playlist)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
                var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

                var userName = User.Identity.Name;

                //var str = queue._id.ToString();
                var pFilter = Builders<Playlist>.Filter.Eq("PlaylistId", playlist.PlaylistId);
                var playlists = PCollection.Find(pFilter).ToList();
                if (playlists.Count == 0)
                {
                    return new ObjectResult("Playlist not found") { StatusCode = 404 };
                }
                var plst = playlists[0];

                if (plst.PublicEditing == false)
                {
                    if (plst.Owner != userName && !plst.Editors.Contains(userName))
                    {
                        return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                    }
                }

                plst._id = playlist._id;
                plst.PlaylistId = playlist.PlaylistId;
                plst.ArtworkPath = playlist.ArtworkPath;
                plst.Description = playlist.Description;
                plst.Name = playlist.Name;
                plst.Owner = playlist.Owner;
                plst.Editors = playlist.Editors;
                plst.Viewers = playlist.Viewers;
                plst.PublicEditing = playlist.PublicEditing;
                plst.PublicViewing = playlist.PublicViewing;

                PCollection.ReplaceOne(pFilter, plst);
            }
            catch (Exception e)
            {
                return new ObjectResult(e.Message) { StatusCode = 500 };
            }


            return new ObjectResult("Playlist updated") { StatusCode = 404 };
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("get")]
        public ObjectResult GetPlaylistById(string id)
        {
            var userName = User.Identity.Name;

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var pCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var pFilter = Builders<Playlist>.Filter.Eq("PlaylistId", id);
            var pDoc = pCollection.Find(pFilter).ToList();

            if(pDoc.Count > 0)
            {
                var plst = pDoc[0];
                if (plst.PublicEditing == false)
                {
                    if (plst.Owner != userName && !plst.Editors.Contains(userName) && !plst.Viewers.Contains(userName))
                    {
                        return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                    }
                }
                return new ObjectResult(new ShortPlaylist(plst)) { StatusCode = 200 };
            }

            return new ObjectResult("Playlist not found") { StatusCode = 404 };
        }

        [Authorize(Roles = "Admin,User")]
        [HttpPost("move-track")]
        public ObjectResult MoveTrack(string id, string trackId, int position)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var userName = User.Identity.Name;

            var pFilter = Builders<Playlist>.Filter.Eq("PlaylistId", id);
            var playlists = PCollection.Find(pFilter).ToList();
            if (playlists.Count() == 0)
            {
                return new ObjectResult("Playlist not found") { StatusCode = 404 };
            }
            var playlist = playlists[0];

            if (playlist.PublicEditing == false)
            {
                if (playlist.Owner != userName && !playlist.Editors.Contains(userName))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            var tracks = (from t in playlist.Tracks
                          where t.TrackId == trackId
                          select t).ToList();
            if (tracks.Count() == 0)
            {
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }
            var track = tracks[0];

            int curIdx = playlist.Tracks.IndexOf(track);
            playlist.Tracks.Insert(position, track);
            playlist.Tracks.RemoveAt(curIdx);

            PCollection.ReplaceOne(pFilter, playlist);

            return new ObjectResult("Track moved") { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("search")]
        public ObjectResult SearchPlaylists(int page, int count, string name="")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            List<ShortPlaylist> playlists = new List<ShortPlaylist>();

            var user = User.Identity.Name;

            var ownerFilter = Builders<Playlist>.Filter.Eq(x => x.Owner, user);
            var viewersFilter = Builders<Playlist>.Filter.AnyEq(x => x.Viewers, user);
            var publicViewingFilter = Builders<Playlist>.Filter.Eq(x => x.PublicViewing, true);
            var EditorsFilter = Builders<Playlist>.Filter.AnyEq(x => x.Editors, user);

            // Combine filters with OR
            var combinedFilter = Builders<Playlist>.Filter.Or(ownerFilter, viewersFilter, publicViewingFilter, EditorsFilter);
            combinedFilter = combinedFilter & Builders<Playlist>.Filter.Regex(x=>x.Name, new BsonRegularExpression(name, "i"));

            playlists.AddRange(PCollection.Find(combinedFilter)
                                    .Skip(page * count)
                                    .Limit(count)
                                    .ToList()
                                    .Select(x => new ShortPlaylist(x)));

            

            return new ObjectResult(playlists) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("get-tracks")]
        public ObjectResult GetTracks(int page, int count, string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var pFilter = Builders<Playlist>.Filter.Eq(x => x.PlaylistId, id);

            if(page == 0 || count == 0)
            {
                return new ObjectResult("Playlist not found") { StatusCode = 400 };
            }

            var Playlists = PCollection.Find(pFilter).ToList();
            if (Playlists.Count() == 0)
            {
                return new ObjectResult("Playlist not found") { StatusCode = 404 };
            }
            var playlist = Playlists[0];

            var userName = User.Identity.Name;
            if (playlist.PublicEditing == false)
            {
                if (playlist.Owner != userName && !playlist.Editors.Contains(userName) && !playlist.Viewers.Contains(userName))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            var tracks = playlist.Tracks.Take(new Range(page * count, (page * count) + count));

            List<Track> fullTracks = TracksCollection.Find(Builders<Track>.Filter.In(x => x.TrackId, tracks.Select(x => x.TrackId))).ToList();

            var usernames = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x.Username));
            usernames.Add(User.Identity.Name);

            foreach (var track in fullTracks)
            {
                // Check for null or empty collections to avoid exceptions
                if (track.PlayCounts != null)
                {
                    track.PlayCounts = track.PlayCounts.Where(x => usernames.Contains(x.Username)).ToList();
                }

                if (track.SkipCounts != null)
                {
                    track.SkipCounts = track.SkipCounts.Where(x => usernames.Contains(x.Username)).ToList();
                }

                if (track.Ratings != null)
                {
                    track.Ratings = track.Ratings.Where(x => usernames.Contains(x.Username)).ToList();
                }
            }

            return new ObjectResult(fullTracks) { StatusCode = 200 };
        }
    }
}
