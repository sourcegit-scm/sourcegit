using System;
using System.Collections.Generic;
using System.Globalization;

using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

using SkiaSharp;

namespace SourceGit.Models
{
    public enum StaticsticsMode
    {
        All,
        ThisMonth,
        ThisWeek,
    }

    public class StaticsticsAuthor(User user, int count)
    {
        public User User { get; set; } = user;
        public int Count { get; set; } = count;
    }

    public class StaticsticsSample(DateTime time, int count)
    {
        public DateTime Time { get; set; } = time;
        public int Count { get; set; } = count;
    }

    public class StatisticsReport
    {
        private const float OPACITY_DIMMED = 0.3f;

        public static readonly string[] WEEKDAYS = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

        public int Total { get; set; } = 0;
        public List<StaticsticsAuthor> Authors { get; set; } = new List<StaticsticsAuthor>();
        public List<ISeries> Series { get; set; } = new List<ISeries>();
        public List<Axis> XAxes { get; set; } = new List<Axis>();
        public List<Axis> YAxes { get; set; } = new List<Axis>();
        
        private StaticsticsAuthor _selectedAuthor = null;
        public StaticsticsAuthor SelectedAuthor
        {
            get => _selectedAuthor;
            set
            {
                _selectedAuthor = value;

                UpdateHighlighting();
            }
        }

        public StatisticsReport(StaticsticsMode mode, DateTime start)
        {
            _mode = mode;

            YAxes = [new Axis()
            {
                TextSize = 10,
                MinLimit = 0,
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x40808080)) { StrokeThickness = 1 }
            }];

            if (mode == StaticsticsMode.ThisWeek)
            {
                for (int i = 0; i < 7; i++)
                    _mapSamples.Add(start.AddDays(i), 0);

                XAxes.Add(new DateTimeAxis(TimeSpan.FromDays(1), v => WEEKDAYS[(int)v.DayOfWeek]) { TextSize = 10 });
            }
            else if (mode == StaticsticsMode.ThisMonth)
            {
                var now = DateTime.Now;
                var maxDays = DateTime.DaysInMonth(now.Year, now.Month);
                for (int i = 0; i < maxDays; i++)
                    _mapSamples.Add(start.AddDays(i), 0);

                XAxes.Add(new DateTimeAxis(TimeSpan.FromDays(1), v => $"{v:MM/dd}") { TextSize = 10 });
            }
            else
            {
                XAxes.Add(new DateTimeAxis(TimeSpan.FromDays(30), v => $"{v:yyyy/MM}") { TextSize = 10 });
            }
        }

        public void AddCommit(DateTime time, User author)
        {
            Total++;

            var normalized = DateTime.MinValue;
            if (_mode == StaticsticsMode.ThisWeek || _mode == StaticsticsMode.ThisMonth)
                normalized = time.Date;
            else
                normalized = new DateTime(time.Year, time.Month, 1).ToLocalTime();

            if (_mapSamples.TryGetValue(normalized, out var vs))
                _mapSamples[normalized] = vs + 1;
            else
                _mapSamples.Add(normalized, 1);

            if (_mapUsers.TryGetValue(author, out var vu))
                _mapUsers[author] = vu + 1;
            else
                _mapUsers.Add(author, 1);
                
            if (!_authorCommitsByDate.TryGetValue(author, out var authorDates))
            {
                authorDates = new Dictionary<DateTime, int>();

                _authorCommitsByDate.Add(author, authorDates);
            }
            
            if (authorDates.TryGetValue(normalized, out var authorDateCount))
                authorDates[normalized] = authorDateCount + 1;
            else
                authorDates.Add(normalized, 1);
        }

        public void Complete()
        {
            var samples = new List<DateTimePoint>();
            foreach (var kv in _mapSamples)
                samples.Add(new DateTimePoint(kv.Key, kv.Value));

            Series.Add(
                new ColumnSeries<DateTimePoint>()
                {
                    Values = samples,
                    Stroke = null,
                    Fill = null,
                    Padding = 1,
                }
            );

            foreach (var kv in _mapUsers)
                Authors.Add(new StaticsticsAuthor(kv.Key, kv.Value));

            Authors.Sort((l, r) => r.Count - l.Count);

            _mapUsers.Clear();
            _mapSamples.Clear();
        }

        public void ChangeColor(uint color)
        {
            _currentColor = color;

            UpdateHighlighting();
        }
        
        private void UpdateHighlighting()
        {
            if (Series.Count == 0)
                return;
            
            var skColor = new SKColor(_currentColor);
            
            if (_selectedAuthor == null)
            {
                if (Series.Count > 1)
                    Series.RemoveAt(1);
                
                if (Series is [ColumnSeries<DateTimePoint> series])
                    series.Fill = new SolidColorPaint(skColor);
            }
            else
            {
                var dimmedColor = new SKColor(skColor.Red, skColor.Green, skColor.Blue, (byte)(255 * OPACITY_DIMMED));
                
                if (Series is [ColumnSeries<DateTimePoint> series, ..])
                    series.Fill = new SolidColorPaint(dimmedColor);
                
                if (_authorCommitsByDate.TryGetValue(_selectedAuthor.User, out var authorData))
                {
                    var highlightSamples = new List<DateTimePoint>();

                    foreach (var kv in authorData)
                        highlightSamples.Add(new DateTimePoint(kv.Key, kv.Value));
                    
                    highlightSamples.Sort((a, b) => a.DateTime.CompareTo(b.DateTime));
                    
                    if (Series.Count > 1)
                    {
                        if (Series[1] is ColumnSeries<DateTimePoint> highlightSeries)
                            highlightSeries.Values = highlightSamples;
                    }
                    else
                    {
                        Series.Add(new ColumnSeries<DateTimePoint>
                        {
                            Values = highlightSamples,
                            Stroke = null,
                            Fill = new SolidColorPaint(skColor),
                            Padding = 1,
                        });
                    }
                }
            }
        }
        
        public void ResetSelection()
        {
            _selectedAuthor = null;

            UpdateHighlighting();
        }

        private StaticsticsMode _mode = StaticsticsMode.All;
        private Dictionary<User, int> _mapUsers = new Dictionary<User, int>();
        private Dictionary<DateTime, int> _mapSamples = new Dictionary<DateTime, int>();
        private Dictionary<User, Dictionary<DateTime, int>> _authorCommitsByDate = new Dictionary<User, Dictionary<DateTime, int>>();
        private uint _currentColor;
    }

    public class Statistics
    {
        public StatisticsReport All { get; set; }
        public StatisticsReport Month { get; set; }
        public StatisticsReport Week { get; set; }

        public Statistics()
        {
            _today = DateTime.Now.ToLocalTime().Date;
            var weekOffset = (7 + (int)_today.DayOfWeek - (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek) % 7;
            _thisWeekStart = _today.AddDays(-weekOffset);
            _thisMonthStart = _today.AddDays(1 - _today.Day);

            All = new StatisticsReport(StaticsticsMode.All, DateTime.MinValue);
            Month = new StatisticsReport(StaticsticsMode.ThisMonth, _thisMonthStart);
            Week = new StatisticsReport(StaticsticsMode.ThisWeek, _thisWeekStart);
        }

        public void AddCommit(string author, double timestamp)
        {
            var user = User.FindOrAdd(author);

            var time = DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime();
            if (time >= _thisWeekStart)
                Week.AddCommit(time, user);

            if (time >= _thisMonthStart)
                Month.AddCommit(time, user);

            All.AddCommit(time, user);
        }

        public void Complete()
        {
            All.Complete();
            Month.Complete();
            Week.Complete();
        }

        private readonly DateTime _today;
        private readonly DateTime _thisMonthStart;
        private readonly DateTime _thisWeekStart;
    }
}
