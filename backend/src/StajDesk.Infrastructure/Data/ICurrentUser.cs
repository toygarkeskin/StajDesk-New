using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StajDesk.Domain;

namespace StajDesk.Infrastructure.Data;

/// <summary>İsteği yapan kullanıcı. API'de HttpContext'ten, Worker'da "sistem" olarak doldurulur.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? UserName { get; }
    string? IpAddress { get; }
}
