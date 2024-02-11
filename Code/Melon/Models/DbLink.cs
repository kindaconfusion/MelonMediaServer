﻿using MongoDB.Bson;

namespace Melon.Models
{
    public class DbLink
    {
        public string _id { get; set; }
        public string Name { get; set; }

        public DbLink()
        {

        }
        public DbLink(Track t)
        {
            _id = t._id;
            Name = t.Name;
        }
        public DbLink(Album a)
        {
            _id = a._id;
            Name = a.Name;
        }
        public DbLink(Artist a)
        {
            _id = a._id;
            Name = a.Name;
        }
    }
}
