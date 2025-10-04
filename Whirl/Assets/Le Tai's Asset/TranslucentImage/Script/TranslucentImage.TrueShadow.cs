#if LETAI_TRUESHADOW

using System;
using LeTai.TrueShadow.PluginInterfaces;

namespace LeTai.Asset.TranslucentImage
{
public partial class TranslucentImage : ITrueShadowCustomHashProviderV2
{
    public event Action<int> trueShadowCustomHashChanged;

    partial void UpdateTrueShadowHash()
    {
        trueShadowCustomHashChanged?.Invoke(
            HashUtils.CombineHashCodes(
                paraformConfig.CornerRadii.GetHashCode(),
                (int)(paraformConfig.CornerCurvature * 100)
            )
        );
    }
}
}
#endif

namespace LeTai.Asset.TranslucentImage
{
public partial class TranslucentImage
{
    partial void UpdateTrueShadowHash();
}
}
