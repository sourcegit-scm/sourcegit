namespace SourceGit.Models
{
    public enum DevSpaceLayout
    {
        Auto = 0,
        OneByOne = 1,
        TwoByTwo = 2,
        ThreeByThree = 3,
        FourByFour = 4,
    }

    public static class DevSpaceLayoutExtensions
    {
        public static int GetDimension(this DevSpaceLayout layout, int sessionCount)
        {
            if (layout != DevSpaceLayout.Auto)
                return (int)layout;
            if (sessionCount <= 1)
                return 1;
            if (sessionCount <= 4)
                return 2;
            if (sessionCount <= 9)
                return 3;
            return 4;
        }

        public static int GetCapacity(this DevSpaceLayout layout, int sessionCount)
        {
            var dimension = layout.GetDimension(sessionCount);
            return dimension * dimension;
        }
    }
}
