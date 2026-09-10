using GreenExpanses.Application.Commands;
using GreenExpanses.Domain;
using GreenExpanses.Simulation;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class CommandBusTests
{
    [Fact]
    public void Pipeline_RunsValidateAuthorizeExecuteInOrder()
    {
        var calls = new List<string>();
        var bus = new CommandBus();
        bus.Register(
            new ProbeExecutor(calls),
            [new ProbeValidator(calls)],
            [new ProbeAuthorizer(calls)]);
        var command = ProbeCommand.Create();

        var result = bus.Execute(GameStateFactory.Create(42UL), command);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "validate", "authorize", "execute" }, calls);
        Assert.Equal(command.CommandId, result.CommandId);
        Assert.Equal(command.CorrelationId, result.CorrelationId);
        Assert.Equal(command.CausationId, result.CausationId);
    }

    [Fact]
    public void ValidationFailure_StopsPipelineBeforeAuthorizationAndExecution()
    {
        var calls = new List<string>();
        var blocker = new EntityId(Guid.NewGuid());
        var bus = new CommandBus();
        bus.Register(
            new ProbeExecutor(calls),
            [new ProbeValidator(calls, CommandCheckResult.Reject("invalid", "command.invalid", [blocker]))],
            [new ProbeAuthorizer(calls)]);

        var result = bus.Execute(GameStateFactory.Create(42UL), ProbeCommand.Create());

        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal(new[] { "validate" }, calls);
        Assert.Equal(blocker, Assert.Single(result.BlockingEntityIds));
    }

    [Fact]
    public void AuthorizationFailure_StopsBeforeExecution()
    {
        var calls = new List<string>();
        var bus = new CommandBus();
        bus.Register(
            new ProbeExecutor(calls),
            [new ProbeValidator(calls)],
            [new ProbeAuthorizer(calls, CommandCheckResult.Reject("forbidden", "command.forbidden"))]);

        var result = bus.Execute(GameStateFactory.Create(42UL), ProbeCommand.Create());

        Assert.False(result.Succeeded);
        Assert.Equal("forbidden", result.Code);
        Assert.Equal(new[] { "validate", "authorize" }, calls);
    }

    [Fact]
    public void AdvisoryWarnings_AreAccumulatedAcrossStages()
    {
        var calls = new List<string>();
        var bus = new CommandBus();
        bus.Register(
            new ProbeExecutor(calls, CommandCheckResult.Allow(new AdvisoryWarning("execute.warning", "execute.warning"))),
            [new ProbeValidator(calls, CommandCheckResult.Allow(new AdvisoryWarning("validate.warning", "validate.warning")))],
            [new ProbeAuthorizer(calls, CommandCheckResult.Allow(new AdvisoryWarning("authorize.warning", "authorize.warning")))]);

        var result = bus.Execute(GameStateFactory.Create(42UL), ProbeCommand.Create());

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.AdvisoryWarnings.Count);
        Assert.Equal(new[] { "validate.warning", "authorize.warning", "execute.warning" }, result.AdvisoryWarnings.Select(static x => x.Code));
    }

    [Fact]
    public void UnregisteredCommand_ReturnsStructuredFailure()
    {
        var command = ProbeCommand.Create();
        var result = new CommandBus().Execute(GameStateFactory.Create(42UL), command);

        Assert.False(result.Succeeded);
        Assert.Equal("command.unregistered", result.Code);
        Assert.Equal(command.CorrelationId, result.CorrelationId);
    }

    [Fact]
    public void DuplicateRegistration_IsRejected()
    {
        var bus = new CommandBus();
        bus.Register(new ProbeExecutor([]));

        Assert.Throws<InvalidOperationException>(() => bus.Register(new ProbeExecutor([])));
    }

    private sealed record ProbeCommand(
        EntityId CommandId,
        EntityId CorrelationId,
        EntityId? CausationId) : ICommand
    {
        public static ProbeCommand Create() => new(
            new EntityId(Guid.NewGuid()),
            new EntityId(Guid.NewGuid()),
            new EntityId(Guid.NewGuid()));
    }

    private sealed class ProbeValidator : ICommandValidator<ProbeCommand>
    {
        private readonly List<string> _calls;
        private readonly CommandCheckResult _result;

        public ProbeValidator(List<string> calls, CommandCheckResult? result = null)
        {
            _calls = calls;
            _result = result ?? CommandCheckResult.Allow();
        }

        public CommandCheckResult Validate(GameState state, ProbeCommand command)
        {
            _calls.Add("validate");
            return _result;
        }
    }

    private sealed class ProbeAuthorizer : ICommandAuthorizer<ProbeCommand>
    {
        private readonly List<string> _calls;
        private readonly CommandCheckResult _result;

        public ProbeAuthorizer(List<string> calls, CommandCheckResult? result = null)
        {
            _calls = calls;
            _result = result ?? CommandCheckResult.Allow();
        }

        public CommandCheckResult Authorize(GameState state, ProbeCommand command)
        {
            _calls.Add("authorize");
            return _result;
        }
    }

    private sealed class ProbeExecutor : ICommandExecutor<ProbeCommand>
    {
        private readonly List<string> _calls;
        private readonly CommandCheckResult _result;

        public ProbeExecutor(List<string> calls, CommandCheckResult? result = null)
        {
            _calls = calls;
            _result = result ?? CommandCheckResult.Allow();
        }

        public CommandCheckResult Execute(GameState state, ProbeCommand command)
        {
            _calls.Add("execute");
            return _result;
        }
    }
}
