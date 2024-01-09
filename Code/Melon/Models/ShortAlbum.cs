﻿using MongoDB.Bson;

namespace Melon.Models
{
    public class ShortAlbum
    {
        public MelonId _id { get; set; }
        public string AlbumId { get; set; }
        public string AlbumName { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string ReleaseType { get; set; }
    }
}

