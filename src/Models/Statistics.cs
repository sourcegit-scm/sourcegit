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
    public enum StatisticsMode
    {
        All,
        ThisMonth,
        ThisWeek,
    }

    public class StatisticsAuthor(User user, int count)
    {
        public User User { get; set; } = user;
        public int Count { get; set; } = count;
    }

    public class StatisticsReport
    {
        public int Total { get; set; } = 0;
        public List<StatisticsAuthor> Authors { get; set; } = new();
        public List<ISeries> Series { get; set; } = new();
        public List<Axis> XAxes { get; set; } = new();
        public List<Axis> YAxes { get; set; } = new();
        public StatisticsAuthor SelectedAuthor { get => _selectedAuthor; set => ChangeAuthor(value); }

        public StatisticsReport(StatisticsMode mode, DateTime start)
        {
            _mode = mode;

            YAxes.Add(new Axis()
            {
                TextSize = 10,
                MinLimit = 0,
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x40808080)) { StrokeThickness = 1 }
            });

            if (mode == StatisticsMode.ThisWeek)
            {
                for (int i = 0; i < 7; i++)
                    _mapSamples.Add(start.AddDays(i), 0);

                XAxes.Add(new DateTimeAxis(TimeSpan.FromDays(1), v => WEEKDAYS[(int)v.DayOfWeek]) { TextSize = 10 });
            }
            else if (mode == StatisticsMode.ThisMonth)
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

            DateTime normalized;
            if (_mode == StatisticsMode.ThisWeek || _mode == StatisticsMode.ThisMonth)
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

            if (_mapUserSamples.TryGetValue(author, out var vus))
            {
                if (vus.TryGetValue(normalized, out var n))
                    vus[normalized] = n + 1;
                else
                    vus.Add(normalized, 1);
            }
            else
            {
                _mapUserSamples.Add(author, new Dictionary<DateTime, int>
                {
                    { normalized, 1 }
                });
            }
        }

        public void Complete()
        {
            foreach (var kv in _mapUsers)
                Authors.Add(new StatisticsAuthor(kv.Key, kv.Value));

            Authors.Sort((l, r) => r.Count - l.Count);

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

            _mapUsers.Clear();
            _mapSamples.Clear();
        }

        public void ChangeColor(uint color)
        {
            _fillColor = color;

            var fill = new SKColor(color);

            if (Series.Count > 0 && Series[0] is ColumnSeries<DateTimePoint> total)
                total.Fill = new SolidColorPaint(_selectedAuthor == null ? fill : fill.WithAlpha(51));

            if (Series.Count > 1 && Series[1] is ColumnSeries<DateTimePoint> user)
                user.Fill = new SolidColorPaint(fill);
        }

        public void ChangeAuthor(StatisticsAuthor author)
        {
            if (author == _selectedAuthor)
                return;

            _selectedAuthor = author;
            Series.RemoveRange(1, Series.Count - 1);
            if (author == null || !_mapUserSamples.TryGetValue(author.User, out var userSamples))
            {
                ChangeColor(_fillColor);
                return;
            }

            var samples = new List<DateTimePoint>();
            foreach (var kv in userSamples)
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

            ChangeColor(_fillColor);
        }

        private static readonly string[] WEEKDAYS = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];
        private StatisticsMode _mode;
        private Dictionary<User, int> _mapUsers = new();
        private Dictionary<DateTime, int> _mapSamples = new();
        private Dictionary<User, Dictionary<DateTime, int>> _mapUserSamples = new();
        private StatisticsAuthor _selectedAuthor = null;
        private uint _fillColor = 255;
    }

    public class Statistics
    {
        public StatisticsReport All { get; }
        public StatisticsReport Month { get; }
        public StatisticsReport Week { get; }

        public Statistics()
        {
            var today = DateTime.Now.ToLocalTime().Date;
            var weekOffset = (7 + (int)today.DayOfWeek - (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek) % 7;
            _thisWeekStart = today.AddDays(-weekOffset);
            _thisMonthStart = today.AddDays(1 - today.Day);

            All = new StatisticsReport(StatisticsMode.All, DateTime.MinValue);
            Month = new StatisticsReport(StatisticsMode.ThisMonth, _thisMonthStart);
            Week = new StatisticsReport(StatisticsMode.ThisWeek, _thisWeekStart);
        }

        public void AddCommit(string author, double timestamp)
        {
            var emailIdx = author.IndexOf('±');
            var email = author.Substring(emailIdx + 1).ToLower(CultureInfo.CurrentCulture);
            if (!_users.TryGetValue(email, out var user))
            {
                user = User.FindOrAdd(author);
                _users.Add(email, user);
            }

            var time = DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime();
            if (time >= _thisWeekStart)
                Week.AddCommit(time, user);

            if (time >= _thisMonthStart)
                Month.AddCommit(time, user);

            All.AddCommit(time, user);
        }

        public void Complete()
        {
            _users.Clear();

            All.Complete();
            Month.Complete();
            Week.Complete();
        }

        private readonly DateTime _thisMonthStart;
        private readonly DateTime _thisWeekStart;
        private readonly Dictionary<string, User> _users = new();
    }
}
