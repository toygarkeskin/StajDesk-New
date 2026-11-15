using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace StajDesk.Api.Common;

/// <summary>
/// Kullanıcıya anlık bildirim iten SignalR hub'ı. İstemci yalnızca dinler; sunucuya mesaj göndermez.
/// Her kullanıcı yalnızca kendi kimliğine (JWT "sub") gönderilen olayları alır.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub;
