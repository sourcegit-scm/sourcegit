using System;
using System.Collections.Generic;

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

    public class StaticsticsAuthor(string name, int count)
    {
        public string Name { get; set; } = name;
        public int Count { get; set; } = count;
    }

    public class StaticsticsSample(DateTime time, int count)
    {
        public DateTime Time { get; set; } = time;
        public int Count { get; set; } = count;
    }

    public class StatisticsReport
    {
        public static readonly string[] WEEKDAYS = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

        public int Total { get; set; } = 0;
        public List<StaticsticsAuthor> Authors { get; set; } = new List<StaticsticsAuthor>();
        public List<ISeries> Series { get; set; } = new List<ISeries>();
        public List<Axis> XAxes { get; set; } = new List<Axis>();
        public List<Axis> YAxes { get; set; } = new List<Axis>();

        public StatisticsReport(StaticsticsMode mode, DateTime start)
        {
            _mode = mode;

            YAxes = [new Axis() {
                TextSize = 10,
                MinLimit = 0,
                SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray) { StrokeThickness = .6f }
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

        public void AddCommit(DateTime time, string author)
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
                    Fill = new SolidColorPaint(SKColors.Green),
                    Padding = 1,
                }
            );

            foreach (var kv in _mapUsers)
                Authors.Add(new StaticsticsAuthor(kv.Key, kv.Value));

            Authors.Sort((l, r) => r.Count - l.Count);

            _mapUsers.Clear();
            _mapSamples.Clear();
        }

        private StaticsticsMode _mode = StaticsticsMode.All;
        private Dictionary<string, int> _mapUsers = new Dictionary<string, int>();
        private Dictionary<DateTime, int> _mapSamples = new Dictionary<DateTime, int>();
    }

    public class Statistics
    {
        public StatisticsReport All { get; set; }
        public StatisticsReport Month { get; set; }
        public StatisticsReport Week { get; set; }

        public Statistics()
        {
            _today = DateTime.Now.ToLocalTime().Date;
            _thisWeekStart = _today.AddSeconds(-(int)_today.DayOfWeek * 3600 * 24);
            _thisMonthStart = _today.AddDays(1 - _today.Day);

            All = new StatisticsReport(StaticsticsMode.All, DateTime.MinValue);
            Month = new StatisticsReport(StaticsticsMode.ThisMonth, _thisMonthStart);
            Week = new StatisticsReport(StaticsticsMode.ThisWeek, _thisWeekStart);
        }

        public void AddCommit(string author, double timestamp)
        {
            var time = DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime();
            if (time >= _thisWeekStart)
                Week.AddCommit(time, author);

            if (time >= _thisMonthStart)
                Month.AddCommit(time, author);

            All.AddCommit(time, author);
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
