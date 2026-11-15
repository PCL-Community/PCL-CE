namespace PCL;

public sealed class WpfLogFilter : ILogFilter
{
    public string FilterUserName(string raw, char filterChar)
    {
        return McLogFilter.FilterUserName(raw, filterChar);
    }

    public string FilterAccessToken(string raw, char filterChar)
    {
        return McLogFilter.FilterAccessToken(raw, filterChar);
    }
}

public sealed class WpfRestrictedFeatures : IRestrictedFeatures
{
    public bool IsRestricted => RestrictedFeaturePolicy.IsRestricted;

    public bool Check(string feature)
    {
        return RestrictedFeaturePolicy.Check(feature);
    }

    public void ThrowIfRestricted(string feature)
    {
        RestrictedFeaturePolicy.ThrowIfRestricted(feature);
    }
}