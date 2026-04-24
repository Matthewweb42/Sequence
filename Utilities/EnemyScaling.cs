namespace Sequence.Utilities;

public static class EnemyScaling
{
    public static float GetDamageMultiplier(int playerSequence)
    {
        return playerSequence switch
        {
            9 => 1.00f,
            8 => 1.15f,
            7 => 1.30f,
            6 => 1.45f,
            5 => 1.60f,
            _ => 1.00f,
        };
    }

    public static float GetHpMultiplier(int playerSequence)
    {
        return playerSequence switch
        {
            9 => 1.00f,
            8 => 1.20f,
            7 => 1.45f,
            6 => 1.75f,
            5 => 2.10f,
            _ => 1.00f,
        };
    }
}
