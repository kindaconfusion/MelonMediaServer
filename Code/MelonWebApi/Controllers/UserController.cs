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

            var userFilter = Builders<User>.Filter.Eq(x => x.UserId, id);
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
        [Authorize(Roles = "Admin")]
        [HttpGet("search")]
        public ObjectResult SearchUsers(string username = "")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Regex(x => x.Username, new BsonRegularExpression(username, "i"));
            var users = UserCollection.Find(userFilter).ToList();

            List<PublicUser> pUsers = new List<PublicUser>();
            foreach(var user in users)
            {
                pUsers.Add(new PublicUser(user));
            }

            return new ObjectResult(pUsers) { StatusCode = 200 };
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
        public ObjectResult CreateUser(string username, string password, string role)
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

            byte[] tempSalt;
            var protectedPassword = Security.HashPasword(password, out tempSalt);
            var id = ObjectId.GenerateNewId();

            var user = new User();
            user._id = id;
            user.UserId = id.ToString();
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

            return new ObjectResult(user.UserId) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,Server")]
        [HttpPost("create-connection")]
        public ObjectResult CreateConnection(string url, string code, string username, string password)
        {
            var client = new RestClient(url);

            var jwtRequest = new RestRequest("/auth/code-authenticate", Method.Get);
            jwtRequest.AddQueryParameter("code",code);
            var jwtResponse = client.Execute(jwtRequest);

            var createRequest = new RestRequest("/api/users/create", Method.Post);
            createRequest.AddQueryParameter("username", username);
            createRequest.AddQueryParameter("password", password);
            createRequest.AddHeader("Authorization", $"Bearer {jwtResponse.Content}");
            var createResponse = client.Execute(createRequest);

            Security.Connections.Add(new Connection() { Username = username, Password = password, JWT = jwtResponse.Content, URL = url });
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

            var userFilter = Builders<User>.Filter.Eq(x => x.UserId, id);
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

            var userFilter = Builders<User>.Filter.Eq(x => x.UserId, id);
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
            else
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

            var userFilter = Builders<User>.Filter.Eq(x => x.UserId, id);
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

            var userFilter = Builders<User>.Filter.Eq(x => x.UserId, id);
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
            var protectedPassword = Security.HashPasword(password, out tempSalt);
            
            user.Password = protectedPassword;
            user.Salt = tempSalt;

            UserCollection.ReplaceOne(userFilter, user);

            return new ObjectResult("Password Changed") { StatusCode = 200 };
        }

    }
}