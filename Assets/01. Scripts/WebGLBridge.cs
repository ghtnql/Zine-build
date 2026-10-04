using System.Runtime.InteropServices;

public static class WebGLBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int Zine_IsMobileBrowser();

    [DllImport("__Internal")]
    private static extern void Zine_ReloadPage();

    [DllImport("__Internal")]
    private static extern void Zine_ShowLeaderboard(int bodyCount, int survivalMs);

    [DllImport("__Internal")]
    private static extern void Zine_SetLeaderboardEnabled(int enabled);
#endif

    public static bool IsMobileBrowser()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return Zine_IsMobileBrowser() == 1;
#else
        return false;
#endif
    }

    public static void ReloadPage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Zine_ReloadPage();
#endif
    }

    public static void ShowLeaderboard(int bodyCount, int survivalMs)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Zine_ShowLeaderboard(bodyCount, survivalMs);
#else
        NativeLeaderboardUI.ShowGameOver(bodyCount, survivalMs);
#endif
    }

    public static void SetLeaderboardEnabled(bool enabled)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Zine_SetLeaderboardEnabled(enabled ? 1 : 0);
#else
        NativeLeaderboardUI.SetEnabled(enabled);
#endif
    }
}
