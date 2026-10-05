using System;

namespace DazPose.Performer
{
    /// <summary>Pure arithmetic for the three wardrobe layers.</summary>
    public static class WardrobeLayerState
    {
        public const int Base = 0;
        public const int Layer1 = 1;
        public const int Layer2 = 2;
        public const int EmptyCeiling = -1;
        public const int AllLayers = 0b111;

        public static int VisibleMask(int populatedMask, int ceiling)
        {
            ValidateMask(populatedMask);
            ValidateCeiling(ceiling);
            if (ceiling < 0) return 0;
            return populatedMask & ((1 << (ceiling + 1)) - 1);
        }

        public static int HighestVisible(int visibleMask)
        {
            ValidateMask(visibleMask);
            for (int i = Layer2; i >= Base; --i)
                if ((visibleMask & (1 << i)) != 0) return i;
            return EmptyCeiling;
        }

        public static int LowestHidden(int populatedMask, int ceiling)
        {
            int visible = VisibleMask(populatedMask, ceiling);
            int hidden = populatedMask & ~visible;
            for (int i = Base; i <= Layer2; ++i)
                if ((hidden & (1 << i)) != 0) return i;
            return EmptyCeiling;
        }

        public static int RemoveHighestCeiling(int populatedMask, int ceiling)
        {
            int visible = VisibleMask(populatedMask, ceiling);
            int highest = HighestVisible(visible);
            if (highest == EmptyCeiling) return ceiling;
            return HighestVisible(visible & ~(1 << highest));
        }

        public static int AddLowestCeiling(int populatedMask, int ceiling)
        {
            int lowest = LowestHidden(populatedMask, ceiling);
            return lowest == EmptyCeiling ? ceiling : Math.Max(ceiling, lowest);
        }

        public static bool IsFullyDressed(int populatedMask, int ceiling)
        {
            return VisibleMask(populatedMask, ceiling) == populatedMask;
        }

        public static void ValidateMask(int mask)
        {
            if ((mask & ~AllLayers) != 0) throw new ArgumentOutOfRangeException(nameof(mask));
        }

        public static void ValidateCeiling(int ceiling)
        {
            if (ceiling < EmptyCeiling || ceiling > Layer2) throw new ArgumentOutOfRangeException(nameof(ceiling));
        }
    }
}
