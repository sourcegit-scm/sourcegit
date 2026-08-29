namespace DevBoard.Models
{
    public enum DevSpaceLayout
    {
        Auto = 0,
        OneByTwo = 1,
        TwoByTwo = 2,
        ThreeByThree = 3,
        FourByFour = 4,
    }

    public static class DevSpaceLayoutExtensions
    {
        public static int GetRows(this DevSpaceLayout layout, int sessionCount)
        {
            return layout switch
            {
                DevSpaceLayout.OneByTwo => 1,
                DevSpaceLayout.TwoByTwo => 2,
                DevSpaceLayout.ThreeByThree => 3,
                DevSpaceLayout.FourByFour => 3,
                _ => sessionCount switch
                {
                    <= 2 => 1,
                    <= 4 => 2,
                    _ => 3,
                },
            };
        }

        public static int GetColumns(this DevSpaceLayout layout, int sessionCount)
        {
            return layout switch
            {
                DevSpaceLayout.OneByTwo => 2,
                DevSpaceLayout.TwoByTwo => 2,
                DevSpaceLayout.ThreeByThree => 3,
                DevSpaceLayout.FourByFour => 3,
                _ => sessionCount switch
                {
                    <= 1 => 1,
                    2 => 2,
                    <= 4 => 2,
                    _ => 3,
                },
            };
        }

        public static int GetCapacity(this DevSpaceLayout layout, int sessionCount)
        {
            return layout.GetRows(sessionCount) * layout.GetColumns(sessionCount);
        }
    }
}
