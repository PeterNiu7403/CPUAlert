using MoleWindows.Models;

namespace MoleWindows.Services;

public interface IOperationPlanValidator
{
    OperationPlanValidationResult ValidateForApply(
        OperationPlan plan,
        IReadOnlyList<OperationPlanItem> currentItems,
        bool userConfirmed,
        DateTimeOffset nowUtc);
}

public enum OperationPlanValidationCode
{
    Valid,
    ConfirmationRequired,
    Expired,
    EmptySelection,
    ContentChanged,
    UnsafeTarget
}

public sealed record OperationPlanValidationResult(
    OperationPlanValidationCode Code,
    string Message)
{
    public bool IsValid => Code == OperationPlanValidationCode.Valid;
}
