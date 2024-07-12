using BlazorDatasheet.DataStructures.Intervals;
using BlazorDatasheet.Formula.Core;

namespace BlazorDatasheet.Core.Data.Filter;

public interface IColumnFilter
{
    /// <summary>
    /// Returns the visible rows based on the filter.
    /// </summary>
    /// <returns></returns>
    public bool Match(CellValue cellValue);

    /// <summary>
    /// Whether blank values should always be included.
    /// </summary>
    public bool IncludeBlanks { get; }
}