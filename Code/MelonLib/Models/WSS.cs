﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Models
{
    public class WSS
    {
        public string _id { get; set; }
        public System.Net.WebSockets.WebSocket Socket { get; set; }
        public string CurrentQueue { get; set; }
        public string DeviceName { get; set; }
        public string UserId { get; set; }
        public bool IsPublic { get; set; }
        public bool SendProgress { get;set; }
        public DateTime LastPing { get; set; }

    }
}
