﻿using BlazorDatasheet.Core.Data;
using BlazorDatasheet.Core.Data.Cells;
using BlazorDatasheet.DataStructures.Geometry;
using BlazorDatasheet.Formula.Core;

namespace BlazorDatasheet.Core.Commands.Data;

public class SetCellValuesCommand : IUndoableCommand
{
    private readonly object[][]? _values = null;
    private readonly CellValue[][]? _cellValues = null;
    private readonly IRegion? _region = null;
    private readonly object? _singleValue = null;
    private readonly CellValue? _singleCellValue = null;
    private readonly int _row;
    private readonly int _col;
    private readonly CellStoreRestoreData _restoreData = new();

    /// <summary>
    /// Creates a command to set multiple cell values starting at position <paramref name="row"/>/<paramref name="col"/>
    /// </summary>
    /// <param name="values">The values to set. values[row] is the row offset from <paramref name="row"/>. Each values[row][col] is the col offset from <paramref name="col"/></param>
    /// <param name="row"></param>
    /// <param name="col"></param>
    public SetCellValuesCommand(int row, int col, object[][] values)
    {
        _values = values;
        _row = row;
        _col = col;
    }

    /// <summary>
    /// Creates a command to set multiple cell values starting at position <paramref name="row"/>/<paramref name="col"/>
    /// </summary>
    /// <param name="values">The values to set. values[row] is the row offset from <paramref name="row"/>. Each values[row][col] is the col offset from <paramref name="col"/></param>
    /// <param name="row"></param>
    /// <param name="col"></param>
    public SetCellValuesCommand(int row, int col, CellValue[][] values)
    {
        _cellValues = values;
        _row = row;
        _col = col;
    }

    /// <summary>
    /// Sets all the cells in the region <paramref name="region"/> to <paramref name="value"/>
    /// </summary>
    /// <param name="region"></param>
    /// <param name="value"></param>
    public SetCellValuesCommand(IRegion region, object? value)
    {
        _region = region;
        _singleValue = value;
    }

    public bool Execute(Sheet sheet)
    {
        if (_values != null)
            ExecuteSetObjectArrayData(sheet);
        else if (_cellValues != null)
            ExecuteSetCellValueData(sheet);
        else if (_region != null && _singleCellValue != null)
            ExecuteSetRegionDataAsCellValue(sheet);
        else if (_region != null)
            ExecuteSetRegionData(sheet);
        else
            return false;

        return true;
    }

    private void ExecuteSetObjectArrayData(Sheet sheet)
    {
        sheet.ScreenUpdating = false;
        var rowEnd = _row + _values!.Length;
        var colEnd = _col;

        for (int i = 0; i < _values.Length; i++)
        {
            for (int j = 0; j < _values[i].Length; j++)
            {
                _restoreData.Merge(sheet.Cells.SetValueImpl(_row + i, _col + j, _values[i][j]));
                colEnd = Math.Max(colEnd, j + _col);
            }
        }

        sheet.MarkDirty(new Region(_row, rowEnd, _col, colEnd));
        sheet.ScreenUpdating = true;
    }

    private void ExecuteSetCellValueData(Sheet sheet)
    {
        sheet.ScreenUpdating = false;
        var rowEnd = _row + _cellValues!.Length;
        var colEnd = _col;

        for (int i = 0; i < _cellValues.Length; i++)
        {
            for (int j = 0; j < _cellValues[i].Length; j++)
            {
                _restoreData.Merge(sheet.Cells.SetValueImpl(_row + i, _col + j, _cellValues[i][j]));
                colEnd = Math.Max(colEnd, j + _col);
            }
        }

        sheet.MarkDirty(new Region(_row, rowEnd, _col, colEnd));
        sheet.ScreenUpdating = true;
    }


    private void ExecuteSetRegionData(Sheet sheet)
    {
        sheet.ScreenUpdating = false;

        for (int row = _region!.Top; row <= _region!.Bottom; row++)
        {
            for (int col = _region.Left; col <= _region.Right; col++)
            {
                _restoreData.Merge(sheet.Cells.SetValueImpl(row, col, _singleValue));
            }
        }

        sheet.MarkDirty(_region);
        sheet.ScreenUpdating = true;
    }

    private void ExecuteSetRegionDataAsCellValue(Sheet sheet)
    {
        sheet.ScreenUpdating = false;

        for (int row = _region!.Top; row <= _region!.Bottom; row++)
        {
            for (int col = _region.Left; col <= _region.Right; col++)
            {
                _restoreData.Merge(sheet.Cells.SetValueImpl(row, col, _singleCellValue!));
            }
        }

        sheet.MarkDirty(_region);
        sheet.ScreenUpdating = true;
    }

    public bool Undo(Sheet sheet)
    {
        sheet.ScreenUpdating = false;
        sheet.Cells.Restore(_restoreData);
        sheet.ScreenUpdating = true;
        return true;
    }
}