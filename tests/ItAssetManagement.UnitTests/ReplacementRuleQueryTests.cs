using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class ReplacementRuleQueryTests
{
    private sealed class Repo : IRepository
    {
        public List<ReplacementRule> Items = [];

        public IQueryable<T> Query<T>(bool tracking = false) where T : class =>
            Items.Cast<T>().AsQueryable();
        public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.ToList());
        public Task<T?> FirstAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.FirstOrDefault());
        public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Count());
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Any());
        public void Add<T>(T entity) where T : class { var item = (ReplacementRule)(object)entity; item.Id = 1; item.RowVersion = new byte[16]; Items.Add(item); }
        public void ExpectVersion<T>(T entity, byte[] version) where T : class { }
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task LockAsync(string resource, CancellationToken ct) => Task.CompletedTask;
        public Task LockAssetAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveWorkflowAsync(long assetId, CancellationToken ct) => throw new NotSupportedException();
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> query, string keyword) => throw new NotSupportedException();
    }
    private sealed class Actor(params string[] grants) : IActor
    {
        public long? UserId => 1; public Guid CorrelationId => Guid.Empty; public string? Method => null; public string? Path => null;
        public bool Has(string code) => grants.Contains(code);
    }
    private sealed class NoWork : IUnitOfWork
    { public Task<T> RunAsync<T>(Func<Task<T>> work,CancellationToken ct=default)=>throw new InvalidOperationException("Invalid input must not reach a transaction"); }
    private sealed class NoAudit : IAuditWriter
    { public void Record(string action,object? entity,long? actorId,string actorType,string outcome="SUCCESS",object? before=null,object? after=null,string? failureCode=null)=>throw new NotSupportedException(); }
    [Theory]
    [InlineData("name")] [InlineData("type")] [InlineData("age")] [InlineData("count")] [InlineData("ratio")] [InlineData("ratioScale")]
    [InlineData("price")] [InlineData("priceScale")] [InlineData("warranty")] [InlineData("failure")] [InlineData("priority")] [InlineData("empty")]
    public async Task Invalid_rule_commands_are_rejected_before_transaction(string field)
    {
        var request=new ReplacementRuleWriteRequest {Name="Test",MinimumAgeMonths=60,Severity="HIGH"};
        switch(field) {
            case "name":request.Name=" ";break;
            case "type":request.AssetTypeId=0;break;
            case "age":request.MinimumAgeMonths=0;break;
            case "count":request.MinimumMaintenanceCount=0;break;
            case "ratio":request.MaximumMaintenanceCostRatio=1.1m;break;
            case "ratioScale":request.MaximumMaintenanceCostRatio=0.12345m;break;
            case "price":request.EstimatedUnitCost=-1;break;
            case "priceScale":request.EstimatedUnitCost=1.001m;break;
            case "warranty":request.RequireWarrantyExpired=true;break;
            case "failure":request.MinimumFailureCount=2;break;
            case "priority":request.Priority=-1;break;
            case "empty":request.MinimumAgeMonths=null;break;
        }
        var repo=new Repo();var actor=new Actor(Permissions.ReplacementRuleRead,Permissions.ReplacementRuleCreate,Permissions.AssetCost);
        var service=new ReplacementRuleCommandService(repo,new NoWork(),actor,new NoAudit(),new(repo,actor));
        Assert.Equal(400,(await Assert.ThrowsAsync<BusinessException>(()=>service.WriteAsync(null,request,default))).Status);
    }
    [Fact]
    public async Task Explicit_null_cost_requires_financial_permission_before_transaction()
    {
        var repo=new Repo();var actor=new Actor(Permissions.ReplacementRuleRead,Permissions.ReplacementRuleCreate);
        var service=new ReplacementRuleCommandService(repo,new NoWork(),actor,new NoAudit(),new(repo,actor));
        Assert.Equal(403,(await Assert.ThrowsAsync<BusinessException>(()=>service.WriteAsync(null,new(){Name="Rule",MinimumAgeMonths=60,Severity="HIGH",EstimatedUnitCost=null},default))).Status);
    }
    private static ReplacementRule Item(long id = 1, bool active = true) => new() {
        Id=id, Code="RULE%"+id, Name="Tuổi máy", Version=1, Priority=10, MinimumAgeMonths=60,
        IsActive=active, EstimatedUnitCost=123.45m, RowVersion=new byte[16]
    };
    [Fact]
    public async Task Read_requires_explicit_permission()
    {
        var service=new ReplacementRuleQueryService(new Repo(),new Actor());
        Assert.Equal(403,(await Assert.ThrowsAsync<BusinessException>(()=>service.ListAsync(new(),default))).Status);
        Assert.Equal(403,(await Assert.ThrowsAsync<BusinessException>(()=>service.GetAsync(1,default))).Status);
    }
    [Theory]
    [InlineData(0,20,null,"priority",null)] [InlineData(1,101,null,"priority",null)]
    [InlineData(int.MaxValue,100,null,"priority",null)] [InlineData(1,20,"Active","estimatedUnitCost",null)]
    [InlineData(1,20,"invalid","priority",null)] [InlineData(1,20,null,"priority",0L)]
    public async Task Invalid_query_is_rejected(int page,int size,string? status,string sort,long? type)
    {
        var service=new ReplacementRuleQueryService(new Repo(),new Actor(Permissions.ReplacementRuleRead));
        Assert.Equal(400,(await Assert.ThrowsAsync<BusinessException>(()=>service.ListAsync(new() {Page=page,PageSize=size,Status=status,SortBy=sort,AssetTypeId=type},default))).Status);
    }
    [Fact]
    public async Task Filters_count_and_stable_paging_preserve_inactive_history()
    {
        var repo=new Repo();repo.Items.AddRange([Item(3),Item(1),Item(2,false)]);
        var service=new ReplacementRuleQueryService(repo,new Actor(Permissions.ReplacementRuleRead));
        var result=await service.ListAsync(new(){Keyword="%",Status="Active",PageSize=1,Page=2},default);
        Assert.Equal(2,result.TotalItems);Assert.Equal(3,Assert.Single(result.Items).Id);
        Assert.False((await service.GetAsync(2,default)).IsActive);
        Assert.Null((await service.GetAsync(1,default)).EstimatedUnitCost);
        Assert.Equal(404,(await Assert.ThrowsAsync<BusinessException>(()=>service.GetAsync(99,default))).Status);
    }
    [Theory]
    [InlineData(Permissions.AssetCost)] [InlineData(Permissions.ReportCost)]
    public async Task Explicit_cost_grant_controls_financial_projection(string permission)
    {
        var repo=new Repo();repo.Items.Add(Item());
        var service=new ReplacementRuleQueryService(repo,new Actor(Permissions.ReplacementRuleRead,permission));
        Assert.Equal(123.45m,(await service.GetAsync(1,default)).EstimatedUnitCost);
    }
    [Fact]
    public async Task Redacted_price_is_absent_from_json()
    {
        var repo=new Repo();repo.Items.Add(Item());
        var service=new ReplacementRuleQueryService(repo,new Actor(Permissions.ReplacementRuleRead));
        var json=System.Text.Json.JsonSerializer.Serialize(await service.GetAsync(1,default));
        Assert.DoesNotContain("EstimatedUnitCost",json);
        Assert.DoesNotContain("123.45",json);
    }
}
