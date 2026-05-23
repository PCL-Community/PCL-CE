namespace PCL.Core.App.Tasks;

public class ProgressSmoother(double smoothFactor = 0.1)
{
    private double _smoothed;

    public double Update(double raw)
    {
        _smoothed = raw <= 0 || raw >= 1 || _smoothed >= raw
            ? raw
            : _smoothed * (1 - smoothFactor) + raw * smoothFactor;

        return _smoothed;
    }

    public void Reset() => _smoothed = 0;
}