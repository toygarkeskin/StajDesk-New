using Microsoft.Extensions.Configuration;

namespace StajDesk.Infrastructure;

/// <summary>
/// Açılışta gizli ayarların gerçekten tanımlandığını doğrular. Boş veya şablondaki "CHANGE_ME" değeriyle
/// çalışmaya başlamak yerine anlaşılır bir hata verir (zayıf / örnek parolayla yanlışlıkla yayına çıkmayı önler).
/// Hata mesajında değerin kendisi yer almaz.
/// </summary>
public static class SecretGuard
{
    public static void EnsureConfigured(IConfiguration config, params string[] keys)
    {
        var missing = keys.Where(k =>
        {
            var v = config[k];
            return string.IsNullOrWhiteSpace(v) || v.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
        }).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Gizli ayar(lar) tanımlı değil: {string.Join(", ", missing)}. " +
                "Yerel geliştirme için 'node scripts/setup-secrets.mjs' çalıştırın; docker/üretimde ortam değişkenlerini (.env) doldurun.");
    }
}
