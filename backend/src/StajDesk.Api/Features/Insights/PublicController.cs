using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

[Route("api/public")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public class PublicController(PublicSiteService site) : ApiController
{
    /// <summary>Genel tanıtım sitesi: aktif bannerlar, haberler, açık pozisyonlar ve şirket istatistikleri.</summary>
    [HttpGet("home")] public async Task<IActionResult> Home(CancellationToken ct) => Ok(await site.HomeAsync(ct));

    /// <summary>Gerçek kaynaklı piyasa verisi (TCMB, gold-api.com). Kaynağa ulaşılamazsa değer uydurulmaz, hata listelenir.</summary>
    [HttpGet("market")] public async Task<IActionResult> Market(CancellationToken ct) => Ok(await site.MarketAsync(ct));

    /// <summary>Open-Meteo hava durumu. Kaynağa ulaşılamazsa data=null döner.</summary>
    [HttpGet("weather")] public async Task<IActionResult> Weather([FromQuery] string city = "istanbul", CancellationToken ct = default) => Ok(await site.WeatherAsync(city, ct));
}
