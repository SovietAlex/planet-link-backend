﻿using System;
using System.Text.Json.Serialization;

namespace Library.User.Contract
{
    public class UserSessionContract
    {
        public UserSessionContract()
        {
            RefreshToken = default!;
            Token = default!;
        }

        [JsonIgnore]
        public string RefreshToken { get; internal set; }
        [JsonIgnore]
        public bool IsAuthenticated => !IsExpired;
        [JsonIgnore]
        public bool IsExpired => TokenExpiresOn <= DateTimeOffset.Now;
        [JsonIgnore]
        public bool IsAboutToExpire => (TokenExpiresOn - DateTimeOffset.Now).TotalMinutes <= 5;
        [JsonIgnore]
        public int UserId { get; internal set; }

        public int UserSessionId { get; internal set; }
        public string Token { get; internal set; }
        public DateTimeOffset TokenExpiresOn { get; internal set; }

        public UserContract User => IUserMemoryCache.Users[UserId];
    }
}