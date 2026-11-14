using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StajDesk.Api.Common;
using StajDesk.Api.Features;
using StajDesk.Domain;
using StajDesk.Infrastructure.Email;

namespace StajDesk.Tests;

public sealed class FakeEmailSender(bool enabled = true) : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];
    public bool Enabled => enabled;
    public Task SendAsync(IReadOnlyList<EmailMessage> messages, CancellationToken ct) { Sent.AddRange(messages); return Task.CompletedTask; }
    /// <summary>Son e-postadaki 6 haneli kod (testte posta kutusunu okumak gibi).</summary>
    public string LastCode => Regex.Match(Sent[^1].TextBody, @"\b(\d{6})\b").Groups[1].Value;
}

public sealed class MutableClock(DateTime now) : TimeProvider
{
    public DateTime Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
}

public class LoginCodeTests
{
    private static (TestWorld W, LoginCodeService Svc, FakeEmailSender Mail, MutableClock Clock) Setup(bool smtp = true)
    {
        var w = new TestWorld();
        var mail = new FakeEmailSender(smtp);
        var clock = new MutableClock(TestWorld.Now);
        var svc = new LoginCodeService(w.Db, mail, Options.Create(new LoginCodeOptions()), clock, NullLogger<LoginCodeService>.Instance);
        return (w, svc, mail, clock);
    }

    [Fact]
    public async Task Code_is_emailed_stored_only_as_hash_and_works_once()
    {
        var (w, svc, mail, _) = Setup();
        var ch = await svc.CreateAsync(w.Employee, "127.0.0.1", default);

        Assert.Single(mail.Sent);
        Assert.Equal(w.Employee.Email, mail.Sent[0].ToAddress);
        var code = mail.LastCode;
        Assert.Matches("^[0-9]{6}$", code);
        Assert.DoesNotContain(code, mail.Sent[0].Subject);                     // kod konu satırında görünmez
        var stored = await w.Db.LoginChallenges.SingleAsync();
        Assert.NotEqual(code, stored.CodeHash);                                // düz metin saklanmaz
        Assert.Equal("ec***@test", ch.MaskedEmail);

        var user = await svc.VerifyAsync(new VerifyLoginCodeRequest(ch.ChallengeId, code), default);
        Assert.Equal(w.Employee.Id, user.Id);
        var again = await Assert.ThrowsAsync<AppException>(() => svc.VerifyAsync(new VerifyLoginCodeRequest(ch.ChallengeId, code), default));
        Assert.Equal("CODE_EXPIRED", again.Code);                              // tek kullanımlık
    }

    [Fact]
    public async Task Wrong_codes_are_limited_and_then_the_challenge_is_locked()
    {
        var (w, svc, mail, _) = Setup();
        var ch = await svc.CreateAsync(w.Employee, null, default);
        var wrong = mail.LastCode == "000000" ? "111111" : "000000";

        for (var i = 1; i <= 4; i++)
        {
            var ex = await Assert.ThrowsAsync<ValidationAppException>(() => svc.VerifyAsync(new VerifyLoginCodeRequest(ch.ChallengeId, wrong), default));
            Assert.Contains($"Kalan deneme hakkı: {5 - i}", ex.Errors["code"][0]);
        }
        var last = await Assert.ThrowsAsync<AppException>(() => svc.VerifyAsync(new VerifyLoginCodeRequest(ch.ChallengeId, wrong), default));
        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, last.Status);
        // Doğru kod bile artık kabul edilmez
        await Assert.ThrowsAsync<AppException>(() => svc.VerifyAsync(new VerifyLoginCodeRequest(ch.ChallengeId, mail.LastCode), default));
    }

    [Fact]
    public async Task Code_expires_after_five_minutes()
    {
        var (w, svc, mail, clock) = Setup();
        var ch = await svc.CreateAsync(w.Employee, null, default);
        clock.Now = clock.Now.AddMinutes(5).AddSeconds(1);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.VerifyAsync(new VerifyLoginCodeRequest(ch.ChallengeId, mail.LastCode), default));
        Assert.Equal("CODE_EXPIRED", ex.Code);
    }

    [Fact]
    public async Task Resend_respects_cooldown_and_invalidates_the_previous_code()
    {
        var (w, svc, mail, clock) = Setup();
        var ch = await svc.CreateAsync(w.Employee, null, default);
        var first = mail.LastCode;

        var cool = await Assert.ThrowsAsync<AppException>(() => svc.ResendAsync(ch.ChallengeId, default));
        Assert.Equal("RESEND_COOLDOWN", cool.Code);

        clock.Now = clock.Now.AddSeconds(31);
        await svc.ResendAsync(ch.ChallengeId, default);
        Assert.Equal(2, mail.Sent.Count);
        if (first != mail.LastCode)
            await Assert.ThrowsAsync<ValidationAppException>(() => svc.VerifyAsync(new VerifyLoginCodeRequest(ch.ChallengeId, first), default));
        Assert.Equal(w.Employee.Id, (await svc.VerifyAsync(new VerifyLoginCodeRequest(ch.ChallengeId, mail.LastCode), default)).Id);
    }

    [Fact]
    public async Task New_login_invalidates_older_pending_codes()
    {
        var (w, svc, mail, _) = Setup();
        var old = await svc.CreateAsync(w.Employee, null, default);
        var oldCode = mail.LastCode;
        await svc.CreateAsync(w.Employee, null, default);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.VerifyAsync(new VerifyLoginCodeRequest(old.ChallengeId, oldCode), default));
        Assert.Equal("CODE_EXPIRED", ex.Code);
    }

    [Fact]
    public void Login_code_is_inactive_without_smtp()
    {
        var (_, svc, _, _) = Setup(smtp: false);
        Assert.False(svc.IsActive);
    }

    [Theory]
    [InlineData("ahmet.yilmaz@devoplus.local", "ah**********@devoplus.local")]
    [InlineData("a@x.com", "a***@x.com")]
    public void Email_is_masked(string input, string expected) => Assert.Equal(expected, LoginCodeService.MaskEmail(input));
}
