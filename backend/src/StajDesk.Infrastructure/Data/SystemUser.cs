using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StajDesk.Domain;

namespace StajDesk.Infrastructure.Data;

public sealed class SystemUser : ICurrentUser
{
    public Guid? UserId => null;
    public string? UserName => "Sistem";
    public string? IpAddress => null;
}
