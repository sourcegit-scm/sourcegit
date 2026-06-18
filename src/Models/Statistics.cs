using System;
using System.Collections.Generic;
using System.Globalization;

namespace SourceGit.Models
{
    public enum StatisticsMode
    {
        All = 0,
        ThisMonth,
        ThisWeek,
    }

    public class StatisticsAuthor(User user, int count)
    {
        public User User { get; set; } = user;
        public int Count { get; set; } = count;
    }

    public class StatisticsSamples
    {
        public DateTime StartTime { get; }
        public DateTime EndTime { get; }
        public int Count { get; }
        public int MaxValue { get; }
        public bool HasSpecialUser => _user != null;

        public StatisticsSamples(StatisticsMode mode, DateTime start, DateTime end, Dictionary<DateTime, int> all, int maxValue)
        {
            _mode = mode;
            _all = all;

            StartTime = start;
            EndTime = end;

            if (maxValue < 8)
                MaxValue = 8;
            else if (maxValue < 16)
                MaxValue = 16;
            else
                MaxValue = (int)(Math.Floor(maxValue / 6.0) * 8.0);

            Count = mode switch
            {
                StatisticsMode.All => (end.Year - start.Year) * 12 + (end.Month - start.Month) + 1,
                _ => all.Count,
            };
        }

        public void WithUser(Dictionary<DateTime, int> user)
        {
            _user = user;
        }

        public (string, int, int) GetSample(DateTime time)
        {
            var label = _mode switch
            {
                StatisticsMode.All => time.ToString("yyyy/MM"),
                StatisticsMode.ThisMonth => time.ToString("MM/dd"),
                _ => s_weekdays[(int)time.DayOfWeek]
            };

            var total = _all.GetValueOrDefault(time, 0);
            var user = _user?.GetValueOrDefault(time, 0) ?? 0;
            return (label, total, user);
        }

        public DateTime NextSampleTime(DateTime time)
        {
            if (_mode != StatisticsMode.All)
                return time.AddDays(-1);

            if (time.Month == 1)
                return new DateTime(time.Year - 1, 12, 1).ToLocalTime().Date;

            return new DateTime(time.Year, time.Month - 1, 1).ToLocalTime().Date;
        }

        private static readonly string[] s_weekdays = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"];
        private StatisticsMode _mode;
        private Dictionary<DateTime, int> _all;
        private Dictionary<DateTime, int> _user;
    }

    public class StatisticsReport
    {
        public StatisticsMode Mode { get; }
        public int Total { get; set; } = 0;
        public List<StatisticsAuthor> Authors { get; set; } = new();

        public StatisticsReport(StatisticsMode mode, DateTime start)
        {
            Mode = mode;

            if (mode == StatisticsMode.ThisWeek)
            {
                _minSampleTime = start;
                _maxSampleTime = start.AddDays(6);

                for (int i = 0; i < 7; i++)
                    _all.Add(start.AddDays(i), 0);
            }
            else if (mode == StatisticsMode.ThisMonth)
            {
                var now = DateTime.Now;
                var maxDays = DateTime.DaysInMonth(now.Year, now.Month);
                _minSampleTime = start;
                _maxSampleTime = start.AddDays(maxDays - 1);
                for (int i = 0; i < maxDays; i++)
                    _all.Add(start.AddDays(i), 0);
            }
        }

        public void AddCommit(DateTime time, User author)
        {
            Total++;

            DateTime normalized;
            if (Mode == StatisticsMode.ThisWeek || Mode == StatisticsMode.ThisMonth)
            {
                normalized = time.Date;
            }
            else
            {
                normalized = new DateTime(time.Year, time.Month, 1).ToLocalTime().Date;

                if (normalized < _minSampleTime)
                    _minSampleTime = normalized;
                if (normalized > _maxSampleTime)
                    _maxSampleTime = normalized;
            }

            if (_all.TryGetValue(normalized, out var vs))
                _all[normalized] = vs + 1;
            else
                _all.Add(normalized, 1);

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
                var added = new Dictionary<DateTime, int>();
                added.Add(normalized, 1);
                _mapUserSamples.Add(author, added);
            }
        }

        public void Complete()
        {
            foreach (var kv in _mapUsers)
                Authors.Add(new StatisticsAuthor(kv.Key, kv.Value));

            _mapUsers.Clear();
            Authors.Sort((l, r) => r.Count - l.Count);

            foreach (var kv in _all)
            {
                if (kv.Value > _maxSampleValue)
                    _maxSampleValue = kv.Value;
            }
        }

        public StatisticsSamples GetSamples(StatisticsAuthor withUser)
        {
            var samples = new StatisticsSamples(Mode, _minSampleTime, _maxSampleTime, _all, _maxSampleValue);
            if (withUser != null && _mapUserSamples.TryGetValue(withUser.User, out var userSamples))
                samples.WithUser(userSamples);
            return samples;
        }

        private DateTime _minSampleTime = DateTime.MaxValue;
        private DateTime _maxSampleTime = DateTime.MinValue;
        private int _maxSampleValue = 0;
        private Dictionary<DateTime, int> _all = new();
        private Dictionary<User, int> _mapUsers = new();
        private Dictionary<User, Dictionary<DateTime, int>> _mapUserSamples = new();
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

            var time = DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime().Date;
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
