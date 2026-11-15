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


public interface ICacheService
{
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl) => GetOrSetAsync(key, factory, _ => ttl);
    /// <summary>Süre, üretilen değere göre seçilir (ör. dış kaynak hatası içeren sonuç kısa süre önbelleğe alınır).</summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, Func<T, TimeSpan> ttl);
    Task RemoveAsync(params string[] keys);
    Task<long> RemoveByPrefixAsync(string prefix);
    Task<IReadOnlyList<(string Key, TimeSpan? Ttl)>> ListAsync(string prefix);
}
