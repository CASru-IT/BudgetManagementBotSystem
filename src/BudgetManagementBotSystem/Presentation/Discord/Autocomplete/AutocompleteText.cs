namespace BudgetManagementBotSystem.Presentation.Discord.Autocomplete;

internal static class AutocompleteText
{
    public const int MaxSuggestions = 25;
    private const int MaxNameLength = 100;

    public static string CurrentValue(global::Discord.IAutocompleteInteraction interaction)
    {
        return interaction.Data.Current.Value?.ToString() ?? string.Empty;
    }

    public static bool Contains(string source, string value)
    {
        return source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesId(int id, string value)
    {
        return id.ToString().StartsWith(value, StringComparison.OrdinalIgnoreCase);
    }

    public static string TruncateName(string name)
    {
        if (name.Length <= MaxNameLength)
        {
            return name;
        }

        return name[..(MaxNameLength - 3)] + "...";
    }

    public static object ValueForParameter(int id, global::Discord.Interactions.IParameterInfo parameter)
    {
        var parameterType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        return parameterType == typeof(string) ? id.ToString() : id;
    }
}
