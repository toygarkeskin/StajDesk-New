using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StajDesk.Api.Common;
using StajDesk.Api.Features;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Tests;

public sealed class FakeEventBus : IEventBus
{
    public List<IntegrationEvent> Events { get; } = [];
    public void Enqueue(IntegrationEvent evt) => Events.Add(evt);
}

public sealed class NullCache : ICacheService
{
    public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, Func<T, TimeSpan> ttl) => factory();
    public Task RemoveAsync(params string[] keys) => Task.CompletedTask;
    public Task<long> RemoveByPrefixAsync(string prefix) => Task.FromResult(0L);
    public Task<IReadOnlyList<(string Key, TimeSpan? Ttl)>> ListAsync(string prefix) => Task.FromResult<IReadOnlyList<(string, TimeSpan?)>>([]);
}

public sealed class FixedClock(DateTime now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
}

/// <summary>Her test için izole bellek-içi veritabanı ve temel organizasyon verisi.</summary>
public sealed class TestWorld
{
    public static readonly DateTime Now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc); // Pazartesi

    public AppDbContext Db { get; }
    public FakeEventBus Events { get; } = new();
    public TimeProvider Clock { get; set; } = new FixedClock(Now);
    public Department Accounting { get; }
    public Department It { get; }
    public User Employee { get; }
    public User OtherEmployee { get; }
    public User Manager { get; }
    public User Agent { get; }
    public TicketCategory Category { get; }
    public Group SupportGroup { get; }

    public TestWorld()
    {
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Accounting = new Department { Name = "Muhasebe", Type = DepartmentType.HeadOffice };
        It = new Department { Name = "BT", Type = DepartmentType.HeadOffice };
        Manager = new User { FirstName = "Mira", LastName = "Müdür", Email = "mudur@test", Department = Accounting, MustChangePassword = false };
        Employee = new User { FirstName = "Ece", LastName = "Çalışan", Email = "ece@test", Department = Accounting, Manager = Manager, MustChangePassword = false, HireDate = new DateOnly(2020, 1, 1) };
        OtherEmployee = new User { FirstName = "Ali", LastName = "Başka", Email = "ali@test", Department = It, MustChangePassword = false };
        Agent = new User { FirstName = "Deniz", LastName = "Destek", Email = "destek@test", Department = It, MustChangePassword = false };
        Accounting.Manager = Manager;
        SupportGroup = new Group { Name = "IT Destek L1", IsSupportGroup = true };
        SupportGroup.Members.Add(new GroupMember { User = Agent });
        Category = new TicketCategory { Name = "Donanım", DefaultGroup = SupportGroup };
        Db.AddRange(Accounting, It, Manager, Employee, OtherEmployee, Agent, SupportGroup, Category);
        Db.Roles.Add(new Role { Code = RoleCodes.It, Name = "IT", Permissions = [Permissions.TicketsManage] });
        Db.SaveChanges();
    }

    public static UserContext Ctx(User u, params string[] perms) => new(u.Id, u.FullName, u.DepartmentId, new HashSet<string>(), new HashSet<string>(perms));
    public UserContext EmployeeCtx => Ctx(Employee);
    public UserContext AgentCtx => Ctx(Agent, Permissions.TicketsManage);
    public UserContext ManagerCtx => Ctx(Manager, Permissions.TicketsViewDepartment);

    public TicketService Tickets() => new(Db, Events, new NullCache(), new AccessScope(Db), Clock, NullLogger<TicketService>.Instance);
    public LeaveService Leave() => new(Db, Clock, NullLogger<LeaveService>.Instance);
    public RevenueService Revenue() => new(Db, Clock);
    public ShiftService Shifts() => new(Db, Leave(), Clock);
    public FormKindRegistry Kinds() => new([Leave(), new SpendingLimitService(Db), Revenue(), Shifts()]);
    public WorkflowService Workflow() => new(Db, Events, Kinds(), Clock, NullLogger<WorkflowService>.Instance);
}
