using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public class StatisticsSample(string name)
    {
        public string Name { get; set; } = name;
        public int Count { get; set; } = 0;
    }

    public class StatisticsReport
    {
        public int Total { get; set; } = 0;
        public List<StatisticsSample> Samples { get; set; } = new List<StatisticsSample>();
        public List<StatisticsSample> ByAuthor { get; set; } = new List<StatisticsSample>();

        public void AddCommit(int index, string author)
        {
            Total++;
            Samples[index].Count++;

            if (_mapUsers.TryGetValue(author, out var value))
            {
                value.Count++;
            }
            else
            {
                var sample = new StatisticsSample(author);
                sample.Count++;

                _mapUsers.Add(author, sample);
                ByAuthor.Add(sample);
            }
        }

        public void Complete()
        {
            ByAuthor.Sort((l, r) => r.Count - l.Count);
            _mapUsers.Clear();
        }

        private readonly Dictionary<string, StatisticsSample> _mapUsers = new Dictionary<string, StatisticsSample>();
    }

    public class Statistics
    {
        public StatisticsReport Year { get; set; } = new StatisticsReport();
        public StatisticsReport Month { get; set; } = new StatisticsReport();
        public StatisticsReport Week { get; set; } = new StatisticsReport();

        public Statistics()
        {
            _today = DateTime.Today;
            _thisWeekStart = _today.AddSeconds(-(int)_today.DayOfWeek * 3600 * 24 - _today.Hour * 3600 - _today.Minute * 60 - _today.Second);
            _thisWeekEnd = _thisWeekStart.AddDays(7);

            for (int i = 0; i < 12; i++)
                Year.Samples.Add(new StatisticsSample(""));

            var monthDays = DateTime.DaysInMonth(_today.Year, _today.Month);
            for (int i = 0; i < monthDays; i++)
                Month.Samples.Add(new StatisticsSample($"{i + 1}"));

            string[] weekDayNames = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];
            for (int i = 0; i < weekDayNames.Length; i++)
                Week.Samples.Add(new StatisticsSample(weekDayNames[i]));
        }

        public string Since()
        {
            return _today.AddMonths(-11).ToString("yyyy-MM-01 00:00:00");
        }

        public void AddCommit(string author, double timestamp)
        {
            var time = DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime();
            if (time.CompareTo(_thisWeekStart) >= 0 && time.CompareTo(_thisWeekEnd) < 0)
                Week.AddCommit((int)time.DayOfWeek, author);

            if (time.Month == _today.Month)
                Month.AddCommit(time.Day - 1, author);

            Year.AddCommit(time.Month - 1, author);
        }

        public void Complete()
        {
            Year.Complete();
            Month.Complete();
            Week.Complete();

            // Year is start from 11 months ago from now.
            var thisYear = _today.Year;
            var start = _today.AddMonths(-11);
            if (start.Month == 1)
            {
                for (int i = 0; i < 12; i++)
                    Year.Samples[i].Name = $"{thisYear}/{i + 1:00}";
            }
            else
            {
                var lastYearIdx = start.Month - 1;
                var lastYearMonths = Year.Samples.GetRange(lastYearIdx, 12 - lastYearIdx);
                for (int i = 0; i < lastYearMonths.Count; i++)
                    lastYearMonths[i].Name = $"{thisYear - 1}/{lastYearIdx + i + 1:00}";

                var thisYearMonths = Year.Samples.GetRange(0, lastYearIdx);
                for (int i = 0; i < thisYearMonths.Count; i++)
                    thisYearMonths[i].Name = $"{thisYear}/{i + 1:00}";

                Year.Samples.Clear();
                Year.Samples.AddRange(lastYearMonths);
                Year.Samples.AddRange(thisYearMonths);
            }
        }

        private readonly DateTime _today;
        private readonly DateTime _thisWeekStart;
        private readonly DateTime _thisWeekEnd;
    }
}
