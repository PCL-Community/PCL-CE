namespace PCL.Core.Utils;

internal static class EaseUtils
{
    // 预计算常量
    private const double BounceN1 = 7.5625;
    private const double BounceInvD1 = 0.3636363636363636;             // 1 / 2.75
    private const double BounceThreshold2 = 0.7272727272727273;        // 2 / 2.75
    private const double BounceThreshold3 = 0.9090909090909091;        // 2.5 / 2.75
    private const double BounceOffset1 = 0.5454545454545454;           // 1.5 / 2.75
    private const double BounceOffset2 = 0.8181818181818182;           // 2.25 / 2.75
    private const double BounceOffset3 = 0.9545454545454546;           // 2.625 / 2.75
           
    internal const double ElasticLn2Times10 = 6.931471805599453;        // Math.Log(2d) * 10d
    internal const double ElasticPiTimes6Point5 = 20.420352248333657;   // Math.PI * 6.5d
    
    internal static double Bounce(double progress)
    {
        switch (progress)
        {
            case < BounceInvD1:
                return BounceN1 * progress * progress;
            case < BounceThreshold2:
                progress -= BounceOffset1;
                return BounceN1 * progress * progress + 0.75;
            case < BounceThreshold3:
                progress -= BounceOffset2;
                return BounceN1 * progress * progress + 0.9375;
            default:
                progress -= BounceOffset3;
                return BounceN1 * progress * progress + 0.984375;
        }
    }
}