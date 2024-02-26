using System;
using System.Collections.Generic;

namespace SourceGit.Models {
    public class StatisticsSample {
        public string Name { get; set; }
        public int Count { get; set; }
    }

    public class StatisticsReport {
        public int Total { get; set; } = 0;
        public List<StatisticsSample> Samples { get; set; } = new List<StatisticsSample>();
        public List<StatisticsSample> ByCommitter { get; set; } = new List<StatisticsSample>();

        public void AddCommit(int index, string committer) {
            Total++;
            Samples[index].Count++;

            if (_mapByCommitter.ContainsKey(committer)) {
                _mapByCommitter[committer].Count++;
            } else {
                var sample = new StatisticsSample() { Name = committer, Count = 1 };
                _mapByCommitter.Add(committer, sample);
                ByCommitter.Add(sample);
            }
        }

        public void Complete() {
            ByCommitter.Sort((l, r) => r.Count - l.Count);
            _mapByCommitter.Clear();
        }

        private Dictionary<string, StatisticsSample> _mapByCommitter = new Dictionary<string, StatisticsSample>();
    }

    public class Statistics {
        public StatisticsReport Year { get; set; } = new StatisticsReport();
        public StatisticsReport Month { get; set; } = new StatisticsReport();
        public StatisticsReport Week { get; set; } = new StatisticsReport();

        public Statistics() {
            _utcStart = DateTime.UnixEpoch;
            _today = DateTime.Today;
            _thisWeekStart = _today.AddSeconds(-(int)_today.DayOfWeek * 3600 * 24 - _today.Hour * 3600 - _today.Minute * 60 - _today.Second);
            _thisWeekEnd = _thisWeekStart.AddDays(7);

            string[] monthNames = [
                "Jan",
                "Feb",
                "Mar",
                "Apr",
                "May",
                "Jun",
                "Jul",
                "Aug",
                "Sep",
                "Oct",
                "Nov",
                "Dec",
            ];

            for (int i = 0; i < monthNames.Length; i++) {
                Year.Samples.Add(new StatisticsSample {
                    Name = monthNames[i],
                    Count = 0,
                });
            }

            var monthDays = DateTime.DaysInMonth(_today.Year, _today.Month);
            for (int i = 0; i < monthDays; i++) {
                Month.Samples.Add(new StatisticsSample {
                    Name = $"{i + 1}",
                    Count = 0,
                });
            }

            string[] weekDayNames = [
                "SUN",
                "MON",
                "TUE",
                "WED",
                "THU",
                "FRI",
                "SAT",
            ];

            for (int i = 0; i < weekDayNames.Length; i++) {
                Week.Samples.Add(new StatisticsSample {
                    Name = weekDayNames[i],
                    Count = 0,
                });
            }
        }

        public string Since() {
            return _today.ToString("yyyy-01-01 00:00:00");
        }

        public void AddCommit(string committer, double timestamp) {
            var time = _utcStart.AddSeconds(timestamp).ToLocalTime();
            if (time.CompareTo(_thisWeekStart) >= 0 && time.CompareTo(_thisWeekEnd) < 0) {
                Week.AddCommit((int)time.DayOfWeek, committer);
            }

            if (time.Month == _today.Month) {
                Month.AddCommit(time.Day - 1, committer);
            }

            Year.AddCommit(time.Month - 1, committer); 
        }

        public void Complete() {
            Year.Complete();
            Month.Complete();
            Week.Complete();
        }

        private DateTime _utcStart;
        private DateTime _today;
        private DateTime _thisWeekStart;
        private DateTime _thisWeekEnd;
    }
}
