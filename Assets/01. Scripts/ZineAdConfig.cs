using UnityEngine;

[CreateAssetMenu(menuName = "Zine/Ad Config")]
public sealed class ZineAdConfig : ScriptableObject
{
    public string iosInterstitialId;
    public string androidInterstitialId;
}
