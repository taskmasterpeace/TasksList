namespace TasksList.Core.Contexts;

public sealed record ContextMatchRule(
    string ProcessPath,
    string WindowClass,
    string WindowTitle,
    string? ProjectHint);

public sealed record ObservedWindow(
    string ProcessPath,
    string WindowClass,
    string WindowTitle,
    string? ProjectHint);

public static class ContextMatcher
{
    public static bool IsMatch(ContextMatchRule rule, ObservedWindow observed)
    {
        if (!string.Equals(rule.ProcessPath, observed.ProcessPath, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(rule.WindowClass, observed.WindowClass, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.ProjectHint is not null && observed.ProjectHint is not null &&
            string.Equals(rule.ProjectHint, observed.ProjectHint, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(rule.WindowTitle, observed.WindowTitle, StringComparison.OrdinalIgnoreCase);
    }
}
