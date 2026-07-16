namespace DatabaseExplorer.Core.Models;

/// <summary>
/// A selectable row-limit preset for the navigation panel's row-limit picker, used to
/// cap how many rows are loaded when browsing a large table.
/// </summary>
/// <param name="DisplayName">The text shown in the picker, e.g. "1,000 rows".</param>
/// <param name="Value">
/// The actual row cap to pass to <see cref="Interfaces.IDatabaseQueryService.GetObjectDataAsync"/>,
/// or null to mean "no limit — load everything".
/// </param>
public sealed record RowLimitOption(string DisplayName, int? Value);
