﻿using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using BlazorDatasheet.Data;
using BlazorDatasheet.Data.Events;
using BlazorDatasheet.Data.SpatialDataStructures;
using BlazorDatasheet.Render;
using Range = BlazorDatasheet.Data.Range;

namespace BlazorDatasheet.Formats;

public class ConditionalFormatManager
{
    private readonly Sheet _sheet;

    private List<ConditionalFormatAbstractBase> _registered = new();
    private Dictionary<(int row, int id), CellConditionalFormatContainer?> _cache = new();
    private bool _useCache;

    private RTree<ConditonalFormatSpatialData> _cfTree = new();

    public bool UseCache
    {
        get => _useCache;
        set
        {
            _useCache = value;
            if (!_useCache)
                _cache.Clear();
        }
    }

    public ConditionalFormatManager(Sheet sheet)
    {
        _sheet = sheet;
        _sheet.CellsChanged += HandleCellsChanged;
    }

    /// <summary>
    /// Applies the conditional format specified by "key" to all cells in a range, if the conditional formatting exists.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="range"></param>
    public void Apply(ConditionalFormatAbstractBase conditionalFormat, IFixedSizeRange? range)
    {
        if (range == null)
            return;
        if (!_registered.Contains(conditionalFormat))
        {
            _registered.Add(conditionalFormat);
            conditionalFormat.Order = _registered.Count - 1;
        }

        conditionalFormat.Add(range);
        _cfTree.Insert(new ConditonalFormatSpatialData(conditionalFormat, range));

        if (UseCache)
            ComputeAllAndCache();
        else
        {
            conditionalFormat.Prepare(_sheet);
        }
    }

    /// <summary>
    /// Applies conditional format to the whole sheet
    /// </summary>
    /// <param name="key"></param>
    public void Apply(ConditionalFormatAbstractBase format)
    {
        Apply(format, _sheet.Range);
    }

    private void HandleCellsChanged(object? sender, IEnumerable<ChangeEventArgs> args)
    {
        if (!UseCache)
        {
            // Simply prepare all cells that the conditional format belongs to (if shared)
            var handled = new HashSet<int>();
            foreach (var changeEvent in args)
            {
                var cfs = GetFormatsAppliedToPosition(changeEvent.Row, changeEvent.Col);
                foreach (var format in cfs)
                {
                    if (handled.Contains(format.Order))
                        continue;
                    // prepare format (re-compute shared format cache etch.)
                    if (format.IsShared)
                        format.Prepare(_sheet);
                    handled.Add(format.Order);
                }
            }

            return;
        }

        // collect the cell positions that are affected by the change and update them
        var set = new HashSet<(int row, int col)>();
        foreach (var changeEvent in args)
        {
            var formats = GetFormatsAppliedToPosition(changeEvent.Row, changeEvent.Col);
            foreach (var format in formats)
            {
                // Add the cell itself to the set we want to calc for
                set.Add((changeEvent.Row, changeEvent.Col));
                // Determine whether any cells are affected
                if (format.IsShared)
                    set.UnionWith(format.Positions);
            }
        }

        ComputeAllAndCache(set);
    }

    private IEnumerable<Cell> GetCellsInFormat(ConditionalFormat format)
    {
        return _sheet.GetCellsInRanges(format.Ranges);
    }

    /// <summary>
    /// Compute and store the cache. If restrictedPositions is set, limit updating the cache to those positions
    /// </summary>
    /// <param name="restrictedPositions"></param>
    public void ComputeAllAndCache(HashSet<(int row, int col)> restrictedPositions = null)
    {
        for (int i = 0; i < _registered.Count; i++)
        {
            ComputeAllAndCache(_registered[i], restrictedPositions);
        }
    }

    public void ComputeAllAndCache(ConditionalFormatAbstractBase conditionalFormat,
        HashSet<(int row, int col)> restrictedPositions = null)
    {
        // Prepare each format (useful for caching inside formats)
        conditionalFormat.Prepare(_sheet);

        var positionsInFormat = conditionalFormat.GetPositions(restrictedPositions);
        foreach (var posn in positionsInFormat)
        {
            bool apply = true;
            if (conditionalFormat.Predicate != null)
                apply = conditionalFormat.Predicate.Invoke(posn, _sheet);
            Format? formatResult = null;
            if (apply)
            {
                formatResult = conditionalFormat.CalculateFormat(posn.row, posn.col, _sheet);
            }

            CacheFormat(posn.row, posn.col, conditionalFormat.Order, formatResult, apply,
                        conditionalFormat.StopIfTrue);
        }
    }

    private IEnumerable<ConditionalFormatAbstractBase> GetFormatsAppliedToPosition(int row, int col)
    {
        //return _registered.Where(x => x.Ranges.Any(x => x.Contains(row, col)));
        return _cfTree.Search(new Envelope(col, row, col, row))
                      .Select(x => x.ConditionalFormat);
    }

    /// <summary>
    /// Applies the conditional format specified by "key" to a particular cell. If setting the format to a number of cells,
    /// prefer setting via a range.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="range"></param>
    public void Apply(ConditionalFormatAbstractBase format, int row, int col)
    {
        Apply(format, new Range(row, col));
    }

    private void CacheFormat(int row, int col, int cfIndex, Format? format, bool isTrue, bool stopIfTrue)
    {
        var result = new ConditionalFormatResult(cfIndex, format, isTrue, stopIfTrue);
        var exists = _cache.TryGetValue((row, col), out var container);
        if (!exists)
        {
            container = new CellConditionalFormatContainer();
            _cache.Add((row, col), container);
        }

        container.SetResult(result);
    }

    public Format? GetFormat(int row, int col)
    {
        if (!UseCache)
        {
            var cfs = GetFormatsAppliedToPosition(row, col);
            Format? initialFormat = null;
            foreach (var format in cfs)
            {
                var apply = format.Predicate?.Invoke((row, col), _sheet);
                if (apply == false)
                    continue;
                var calced = format.CalculateFormat(row, col, _sheet);
                if (initialFormat == null)
                    initialFormat = calced;
                else
                    initialFormat.Merge(calced);
                if (apply == true && format.StopIfTrue)
                    break;
            }

            return initialFormat;
        }

        var tuple = (row, col);
        if (!_cache.ContainsKey(tuple))
            return null;
        return _cache[tuple]?.GetMergedFormat();
    }

    internal class ConditonalFormatSpatialData : ISpatialData
    {
        internal ConditionalFormatAbstractBase _conditionalFormat;
        public ref readonly ConditionalFormatAbstractBase ConditionalFormat => ref _conditionalFormat;
        private readonly Envelope _envelope;
        public ref readonly Envelope Envelope => ref _envelope;

        internal ConditonalFormatSpatialData(ConditionalFormatAbstractBase cf, IFixedSizeRange range)
        {
            _envelope = new Envelope(range.TopLeft.Col, range.TopLeft.Row, range.BottomRight.Col,
                                     range.BottomRight.Row);
            _conditionalFormat = cf;
        }
    }
}