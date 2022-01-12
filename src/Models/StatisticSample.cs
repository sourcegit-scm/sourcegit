namespace SourceGit.Models {
    /// <summary>
    ///     统计图表样品
    /// </summary>
    public class StatisticSample {
        /// <summary>
        ///     在图表中的顺序
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        ///     样品名
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        ///     提交个数
        /// </summary>
        public int Count { get; set; }
    }
}
