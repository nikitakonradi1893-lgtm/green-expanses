using GreenExpanses.Domain;

namespace GreenExpanses.Application.Commands;

public interface ICommand
{
    EntityId CommandId { get; }
    EntityId CorrelationId { get; }
    EntityId? CausationId { get; }
}

public sealed record AdvisoryWarning(string Code, string MessageKey);

public sealed record CommandCheckResult(
    bool Succeeded,
    string Code,
    string MessageKey,
    IReadOnlyList<EntityId> BlockingEntityIds,
    IReadOnlyList<AdvisoryWarning> AdvisoryWarnings)
{
    public static CommandCheckResult Allow(params AdvisoryWarning[] warnings) =>
        new(true, "ok", "command.ok", Array.Empty<EntityId>(), warnings);

    public static CommandCheckResult Reject(
        string code,
        string messageKey,
        IReadOnlyList<EntityId>? blockingEntityIds = null,
        IReadOnlyList<AdvisoryWarning>? advisoryWarnings = null) =>
        new(
            false,
            code,
            messageKey,
            blockingEntityIds ?? Array.Empty<EntityId>(),
            advisoryWarnings ?? Array.Empty<AdvisoryWarning>());
}

public sealed record CommandResult(
    bool Succeeded,
    string Code,
    string MessageKey,
    EntityId CommandId,
    EntityId CorrelationId,
    EntityId? CausationId,
    IReadOnlyList<EntityId> BlockingEntityIds,
    IReadOnlyList<AdvisoryWarning> AdvisoryWarnings)
{
    public static CommandResult FromCheck(ICommand command, CommandCheckResult check) =>
        new(
            check.Succeeded,
            check.Code,
            check.MessageKey,
            command.CommandId,
            command.CorrelationId,
            command.CausationId,
            check.BlockingEntityIds,
            check.AdvisoryWarnings);
}

public interface ICommandValidator<in TCommand> where TCommand : ICommand
{
    CommandCheckResult Validate(GameState state, TCommand command);
}

public interface ICommandAuthorizer<in TCommand> where TCommand : ICommand
{
    CommandCheckResult Authorize(GameState state, TCommand command);
}

public interface ICommandExecutor<in TCommand> where TCommand : ICommand
{
    CommandCheckResult Execute(GameState state, TCommand command);
}

internal interface ICommandRoute
{
    CommandResult Execute(GameState state, ICommand command);
}

internal sealed class CommandRoute<TCommand> : ICommandRoute where TCommand : ICommand
{
    private readonly IReadOnlyList<ICommandValidator<TCommand>> _validators;
    private readonly IReadOnlyList<ICommandAuthorizer<TCommand>> _authorizers;
    private readonly ICommandExecutor<TCommand> _executor;

    public CommandRoute(
        IReadOnlyList<ICommandValidator<TCommand>> validators,
        IReadOnlyList<ICommandAuthorizer<TCommand>> authorizers,
        ICommandExecutor<TCommand> executor)
    {
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
        _authorizers = authorizers ?? throw new ArgumentNullException(nameof(authorizers));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public CommandResult Execute(GameState state, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (command is not TCommand typedCommand)
        {
            throw new ArgumentException($"Expected command type {typeof(TCommand).Name}.", nameof(command));
        }

        var warnings = new List<AdvisoryWarning>();

        foreach (var validator in _validators)
        {
            var check = validator.Validate(state, typedCommand);
            warnings.AddRange(check.AdvisoryWarnings);
            if (!check.Succeeded)
            {
                return WithWarnings(typedCommand, check, warnings);
            }
        }

        foreach (var authorizer in _authorizers)
        {
            var check = authorizer.Authorize(state, typedCommand);
            warnings.AddRange(check.AdvisoryWarnings);
            if (!check.Succeeded)
            {
                return WithWarnings(typedCommand, check, warnings);
            }
        }

        var execution = _executor.Execute(state, typedCommand);
        warnings.AddRange(execution.AdvisoryWarnings);
        return WithWarnings(typedCommand, execution, warnings);
    }

    private static CommandResult WithWarnings(
        TCommand command,
        CommandCheckResult check,
        IReadOnlyList<AdvisoryWarning> warnings)
    {
        return new CommandResult(
            check.Succeeded,
            check.Code,
            check.MessageKey,
            command.CommandId,
            command.CorrelationId,
            command.CausationId,
            check.BlockingEntityIds,
            warnings.ToArray());
    }
}

public sealed class CommandBus
{
    private readonly Dictionary<Type, ICommandRoute> _routes = new();

    public void Register<TCommand>(
        ICommandExecutor<TCommand> executor,
        IEnumerable<ICommandValidator<TCommand>>? validators = null,
        IEnumerable<ICommandAuthorizer<TCommand>>? authorizers = null)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(executor);

        var type = typeof(TCommand);
        if (_routes.ContainsKey(type))
        {
            throw new InvalidOperationException($"A command route is already registered for {type.Name}.");
        }

        _routes[type] = new CommandRoute<TCommand>(
            validators?.ToArray() ?? Array.Empty<ICommandValidator<TCommand>>(),
            authorizers?.ToArray() ?? Array.Empty<ICommandAuthorizer<TCommand>>(),
            executor);
    }

    public CommandResult Execute(GameState state, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (!_routes.TryGetValue(command.GetType(), out var route))
        {
            return new CommandResult(
                false,
                "command.unregistered",
                "command.unregistered",
                command.CommandId,
                command.CorrelationId,
                command.CausationId,
                Array.Empty<EntityId>(),
                Array.Empty<AdvisoryWarning>());
        }

        return route.Execute(state, command);
    }
}
