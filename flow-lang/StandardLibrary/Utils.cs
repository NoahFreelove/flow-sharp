namespace FlowLang.StandardLibrary;

public static class Utils
{
    private static int RAND_SEED = 0;
    private static Random? fixed_gen = null;

    private static Random GetRand(bool fixed_rng = false)
    {
        if (fixed_rng)
        {
            if (fixed_gen == null)
            {
                if (RAND_SEED == 0)
                {
                    RAND_SEED = Random.Shared.Next();
                }

                fixed_gen = new Random(RAND_SEED);
            }

            return fixed_gen;
        }

        return Random.Shared;
    }

    public static void ResetGen()
    {
        fixed_gen = new Random(RAND_SEED);
    }

    public static void SetSeed(int seed)
    {
        RAND_SEED = seed;
        ResetGen();
    }

    public static long Rand(bool fixed_rng = false)
    {
        return GetRand(fixed_rng).NextInt64();
    }

    public static int IRand(bool fixed_rng = false)
    {
        return GetRand(fixed_rng).Next();
    }

    public static float FRand(bool fixed_rng = false)
    {
        return GetRand(fixed_rng).NextSingle();
    }

    public static double DRand(bool fixed_rng = false)
    {
        return GetRand(fixed_rng).NextDouble();
    }

    public static bool BRand(bool fixed_rng = false)
    {
        return GetRand(fixed_rng).NextSingle() < 0.5f;
    }
}