using System;
using System.Collections.Generic;

namespace SourceGit.Models {
    public class Sample {
        public string Name { get; set; }
        public int Count { get; set; }
    }

    public class Statistics {
        public int TotalYear { get; set; } = 0;
        public int TotalMonth { get; set; } = 0;
        public int TotalWeek { get; set; } = 0;

        public List<Sample> Year { get; set; } = new List<Sample>();
        public List<Sample> Month { get; set; } = new List<Sample>();
        public List<Sample> Week { get; set; } = new List<Sample>();

        public List<Sample> YearByAuthor { get; set; } = new List<Sample>();
        public List<Sample> MonthByAuthor { get; set; } = new List<Sample>();
        public List<Sample> WeekByAuthor { get; set; } = new List<Sample>();

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
                Year.Add(new Sample {
                    Name = monthNames[i],
                    Count = 0,
                });
            }

            var monthDays = DateTime.DaysInMonth(_today.Year, _today.Month);
            for (int i = 0; i < monthDays; i++) {
                Month.Add(new Sample {
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
                Week.Add(new Sample {
                    Name = weekDayNames[i],
                    Count = 0,
                });
            }
        }

        public string Since() {
            return _today.ToString("yyyy-01-01 00:00:00");
        }

        public void AddCommit(string author, double timestamp) {
            var authorTime = _utcStart.AddSeconds(timestamp);
            if (authorTime.CompareTo(_thisWeekStart) >= 0 && authorTime.CompareTo(_thisWeekEnd) < 0) {
                Week[(int)authorTime.DayOfWeek].Count++;
                TotalWeek++;
                AddByAuthor(_mapWeekByAuthor, WeekByAuthor, author);
            }

            if (authorTime.Month == _today.Month) {
                Month[authorTime.Day - 1].Count++;
                TotalMonth++;
                AddByAuthor(_mapMonthByAuthor, MonthByAuthor, author);
            }

            Year[authorTime.Month - 1].Count++;
            TotalYear++;
            AddByAuthor(_mapYearByAuthor, YearByAuthor, author);
        }

        public void Complete() {
            _mapYearByAuthor.Clear();
            _mapMonthByAuthor.Clear();
            _mapWeekByAuthor.Clear();

            YearByAuthor.Sort((l, r) => r.Count - l.Count);
            MonthByAuthor.Sort((l, r) => r.Count - l.Count);
            WeekByAuthor.Sort((l, r) => r.Count - l.Count);
        }

        private void AddByAuthor(Dictionary<string, Sample> map, List<Sample> collection, string author) {
            if (map.ContainsKey(author)) {
                map[author].Count++;
            } else {
                var sample = new Sample { Name = author, Count = 1 };
                map.Add(author, sample);
                collection.Add(sample);
            }
        }

        private DateTime _utcStart;
        private DateTime _today;
        private DateTime _thisWeekStart;
        private DateTime _thisWeekEnd;

        private Dictionary<string, Sample> _mapYearByAuthor = new Dictionary<string, Sample>();
        private Dictionary<string, Sample> _mapMonthByAuthor = new Dictionary<string, Sample>();
        private Dictionary<string, Sample> _mapWeekByAuthor = new Dictionary<string, Sample>();
    }
}
