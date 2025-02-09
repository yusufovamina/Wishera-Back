using System;
using System.Collections.Generic;

namespace WishlistApp.Models
{
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; } // "BirthdayPerson" или "Friend"
        public List<string> WishlistIds { get; set; } = new List<string>();
    }
}

