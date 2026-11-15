using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Common;

public static class CacheKeys
{
    public const string Prefix = "stajdesk:";
    public const string Categories = "stajdesk:lookup:categories";
    public const string Departments = "stajdesk:lookup:departments";
    public const string JobTitles = "stajdesk:lookup:titles";
    public const string Groups = "stajdesk:lookup:groups";
    public const string Forms = "stajdesk:lookup:forms";
    public const string PublicHome = "stajdesk:public:home";
    public const string Market = "stajdesk:public:market";
    public const string Weather = "stajdesk:public:weather";
    public const string StatsPrefix = "stajdesk:stats:";
    public static string Dashboard(Guid id) => $"stajdesk:stats:dashboard:{id}";
}
