﻿using Melon.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SharpCompress.Common;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using Melon.LocalClasses;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Data;
using RestSharp;
using Azure.Core;
using RestSharp.Authenticators;
using System.Text;
using System.Linq;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly ILogger<UserController> _logger;

        public UserController(ILogger<UserController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("get")]
        public ObjectResult GetById(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x._id, id);
            var users = UserCollection.Find(userFilter).ToList();
            
            if(users.Count == 0) 
            {
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }
            var user = users[0];
            var roles = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value);
            if (user.Username != User.Identity.Name)
            {
                if (!user.PublicStats && !roles.Contains("Admin"))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            PublicUser pUser = new PublicUser(user);

            return new ObjectResult(pUser) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("search")]
        public ObjectResult SearchUsers(string username = "")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var currentUserFilter = Builders<User>.Filter.Eq(x => x.Username, User.Identity.Name);
            var currentUsers = UserCollection.Find(currentUserFilter).ToList();

            if (currentUsers.Count == 0)
            {
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }
            var cUser = currentUsers[0];

            var userFilter = Builders<User>.Filter.Regex(x => x.Username, new BsonRegularExpression(username, "i"));
            var users = UserCollection.Find(userFilter).ToList();

            var roles = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.Role)
                       .Select(c => c.Value);

            List<PublicUser> pUsers = new List<PublicUser>();
            foreach(var user in users)
            {
                if(user.Friends == null)
                {
                    user.Friends = new List<string>();
                }

                if (user.Username != User.Identity.Name)
                {
                    if (!roles.Contains("Admin"))
                    {
                        continue;
                    }
                    else if (!user.Friends.Contains(cUser._id))
                    {
                        continue;
                    }
                }
                pUsers.Add(new PublicUser(user));
            }

            return new ObjectResult(pUsers) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPost("add-friend")]
        public ObjectResult AddFriend(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x.Username, User.Identity.Name);
            var users = UserCollection.Find(userFilter).ToList();
            var user = users.FirstOrDefault();
            if(user == null)
            {
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }

            if(user.Friends == null)
            {
                user.Friends = new List<string>();
            }

            user.Friends.Add(id);

            UserCollection.ReplaceOne(userFilter, user);

            return new ObjectResult("Friend Added") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPost("remove-friend")]
        public ObjectResult RemoveFriend(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x.Username, User.Identity.Name);
            var users = UserCollection.Find(userFilter).ToList();
            var user = users.FirstOrDefault();
            if(user == null)
            {
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }

            if(user.Friends == null)
            {
                user.Friends = new List<string>();
            }

            user.Friends.Remove(id);

            UserCollection.ReplaceOne(userFilter, user);

            return new ObjectResult("Friend Removed") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("current")]
        public ObjectResult GetCurrent()
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x.Username, User.Identity.Name);
            var users = UserCollection.Find(userFilter).ToList();

            if (users.Count == 0)
            {
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }
            var user = users[0];

            PublicUser pUser = new PublicUser(user);

            return new ObjectResult(pUser) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,Server")]
        [HttpPost("create")]
        public ObjectResult CreateUser(string username, string password, string role = "User")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x.Username, username);
            var users = UserCollection.Find(userFilter).ToList();
            
            if(users.Count != 0) 
            {
                return new ObjectResult("Username is Taken") { StatusCode = 409 };
            }
            var roles = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value);
            if (roles.Contains("Server"))
            {
                role = "User";
            }

            if(password == "")
            {
                return new ObjectResult("Password cannot be empty") { StatusCode = 400 };
            }

            byte[] tempSalt;
            var protectedPassword = Security.HashPassword(password, out tempSalt);

            var user = new User();
            user._id = ObjectId.GenerateNewId().ToString();
            user.Username = username;
            user.Password = protectedPassword;
            user.Salt = tempSalt;
            user.Type = role;
            user.Bio = "";
            user.FavTrack = "";
            user.FavAlbum = "";
            user.FavArtist = "";
            user.LastLogin = DateTime.MinValue;

            UserCollection.InsertOne(user);

            return new ObjectResult(user._id) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPost("create-connection")]
        public async Task<ObjectResult> CreateConnection(string url, string code, string username, string password)
        {
            //var client = new RestClient(url);

            string tempJWT = "";
            //jwtRequest.AddQueryParameter("code",code);
            //jwtRequest.Timeout = 10000;
            //var jwtResponse = client.Execute(jwtRequest);

            using (HttpClient client = new HttpClient())
            {
                // Set the base URI for HTTP requests
                client.BaseAddress = new Uri(url);

                try
                {
                    // Get JWT token
                    HttpResponseMessage response = await client.GetAsync($"/auth/code-authenticate?code={code}");

                    if (response.IsSuccessStatusCode)
                    {
                        tempJWT = await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        return new ObjectResult("Invalid Invite Code") { StatusCode = 400 };
                    }

                    // Create user
                    
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tempJWT);
                    HttpResponseMessage createResponse = await client.PostAsync($"/api/users/create?username={username}&password={password}", null);

                    if (createResponse.IsSuccessStatusCode)
                    {

                    }
                    else
                    {
                        return new ObjectResult($"Couldn't Create user: {createResponse.Content}") { StatusCode = 400 };
                    }

                    // login to user
                    HttpResponseMessage authResponse = await client.GetAsync($"/auth/login?username={username}&password={password}");

                    if (authResponse.IsSuccessStatusCode)
                    {
                        tempJWT = await authResponse.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        return new ObjectResult($"Failed to login to user: {authResponse.Content}") { StatusCode = 400 };
                    }
                }
                catch (HttpRequestException e)
                {
                    return new ObjectResult(e.Message) { StatusCode = 500 };
                }
            }

            Security.Connections.Add(new Connection() { Username = username, Password = password, JWT = tempJWT, URL = url });
            Security.SaveConnections();

            return new ObjectResult("Connection Added") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPost("delete")]
        public ObjectResult Delete(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x._id, id);
            var users = UserCollection.Find(userFilter).ToList();
            
            if(users.Count == 0) 
            {
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }
            var user = users[0];

            UserCollection.DeleteOne(userFilter);

            return new ObjectResult("User Deleted") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPatch("update")]
        public ObjectResult Update(string id, string bio = null, string role = null, string publicStats = null, string favTrackId = null, string favAlbumId = null, string favArtistId = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x._id, id);
            var users = UserCollection.Find(userFilter).ToList();


            if (users.Count == 0)
            {
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }
            var user = users[0];
            var roles = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value);

            if (user.Username != User.Identity.Name)
            {
                if (!roles.Contains("Admin"))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }


            if(bio != null)
            {
                user.Bio = bio;
            }
            if(role != null && roles.Contains("Admin"))
            {
                user.Type = role;
            }
            else if(role != null && !roles.Contains("Admin"))
            {
                return new ObjectResult("Invalid Auth") { StatusCode = 401 };
            }
            if(publicStats != null)
            {
                user.PublicStats = Convert.ToBoolean(publicStats);
            }
            if(favTrackId != null)
            {
                user.FavTrack = favTrackId;
            }
            if (favAlbumId != null)
            {
                user.FavAlbum = favAlbumId;
            }
            if (favArtistId != null)
            {
                user.FavArtist = favArtistId;
            }

            UserCollection.ReplaceOne(userFilter, user);

            return new ObjectResult("User Updated") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPatch("change-username")]
        public ObjectResult ChangeUsername(string id, string username)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x._id, id);
            var users = UserCollection.Find(userFilter).ToList();

            var checkFilter = Builders<User>.Filter.Eq(x => x.Username, username);
            var u = UserCollection.Find(checkFilter).ToList();

            if (users.Count == 0)
            {
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }

            if (u.Count != 0)
            {
                return new ObjectResult("Username is Taken") { StatusCode = 400 };
            }
            var user = users[0];
            var roles = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value);

            if (user.Username != User.Identity.Name)
            {
                if (!roles.Contains("Admin"))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }
            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var statsFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user.Username);
            var update = Builders<PlayStat>.Update.Set(x => x.User, username);
            StatsCollection.UpdateMany(statsFilter, update);

            user.Username = username;

            UserCollection.ReplaceOne(userFilter, user);
            

            return new ObjectResult("Username Changed") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPatch("change-password")]
        public ObjectResult ChangePassword(string id, string password)
        {
            
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x._id, id);
            var users = UserCollection.Find(userFilter).ToList();

            if (users.Count == 0)
            {
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }
            var user = users[0];
            var roles = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value);

            if (user.Username != User.Identity.Name)
            {
                if (!roles.Contains("Admin"))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            byte[] tempSalt;
            var protectedPassword = Security.HashPassword(password, out tempSalt);
            
            user.Password = protectedPassword;
            user.Salt = tempSalt;

            UserCollection.ReplaceOne(userFilter, user);

            return new ObjectResult("Password Changed") { StatusCode = 200 };
        }

    }
}
