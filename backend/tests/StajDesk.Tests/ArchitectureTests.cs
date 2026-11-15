using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using StajDesk.Api.Features;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Tests;

/// <summary>
/// Katman kuralları yalnızca belgede yazmaz: kuralı çiğneyen kod bu testlerde kırılır.
/// Ayrıntı: docs/architecture/overview.md
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Ticket).Assembly;
    private static readonly Assembly Infrastructure = typeof(AppDbContext).Assembly;
    private static readonly Assembly Api = typeof(WorkflowService).Assembly;

    private static IEnumerable<string> References(Assembly a) => a.GetReferencedAssemblies().Select(r => r.Name!);

    [Fact]
    public void Domain_hicbir_katmana_ve_altyapi_kutuphanesine_bagimli_degildir()
    {
        var refs = References(Domain).ToList();
        Assert.DoesNotContain(refs, r => r.StartsWith("StajDesk."));
        Assert.DoesNotContain(refs, r => r.StartsWith("Microsoft.EntityFrameworkCore") || r.StartsWith("Microsoft.AspNetCore") || r.StartsWith("Npgsql"));
    }

    [Fact]
    public void Infrastructure_yalnizca_Domaine_bagimlidir()
    {
        var refs = References(Infrastructure).Where(r => r.StartsWith("StajDesk.")).ToList();
        Assert.Equal(["StajDesk.Domain"], refs);
    }

    [Fact]
    public void Controllerlar_Features_altinda_ve_adlandirma_kuralina_uygundur()
    {
        var controllers = Api.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        Assert.NotEmpty(controllers);
        Assert.All(controllers, c =>
        {
            Assert.EndsWith("Controller", c.Name);
            Assert.Equal("StajDesk.Api.Features", c.Namespace);
        });
    }

    [Fact]
    public void Her_form_turu_isleyicisi_tek_bir_ture_aittir()
    {
        var handlers = Api.GetTypes().Where(t => typeof(IFormKindHandler).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }).ToList();
        Assert.True(handlers.Count >= 5, "izin, limit, ciro, vardiya ve standart işleyici bekleniyor");
        Assert.All(handlers, h => Assert.EndsWith(h == typeof(StandardFormHandler) ? "Handler" : "Service", h.Name));
    }

    [Fact]
    public void Domain_entityleri_ortak_Entity_tabanindan_turetilir()
    {
        var entities = Domain.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Entity))).ToList();
        Assert.NotEmpty(entities);
        Assert.Contains(typeof(Ticket), entities);
    }
}
