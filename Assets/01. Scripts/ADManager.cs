using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

public sealed class ADManager : MonoBehaviour
{
    private static ADManager instance;
    private InterstitialAd interstitial;
    private ZineAdConfig config;
    private bool sdkReady;
    private bool loading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        ADManager existing = FindObjectOfType<ADManager>();
        if (existing != null)
        {
            existing.BecomeSingleton();
            return;
        }

        GameObject go = new GameObject("ADManager");
        go.AddComponent<ADManager>().BecomeSingleton();
    }

    private void Awake()
    {
        BecomeSingleton();
    }

    private void Start()
    {
        if (instance != this) return;
        config = Resources.Load<ZineAdConfig>("ZineAdConfig");
        BeginConsentAndAds();
    }

    private void BecomeSingleton()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void BeginConsentAndAds()
    {
#if UNITY_IOS || UNITY_ANDROID
        var parameters = new ConsentRequestParameters
        {
            TagForUnderAgeOfConsent = false
        };

        ConsentInformation.Update(parameters, updateError =>
        {
            if (updateError != null)
            {
                Debug.LogWarning("Ad consent update failed: " + updateError.Message);
                if (ConsentInformation.CanRequestAds()) InitializeAds();
                return;
            }

            ConsentForm.LoadAndShowConsentFormIfRequired(showError =>
            {
                if (showError != null)
                    Debug.LogWarning("Ad consent form failed: " + showError.Message);

                if (ConsentInformation.CanRequestAds())
                    InitializeAds();
            });
        });
#endif
    }

    private void InitializeAds()
    {
        if (sdkReady) return;
        MobileAds.Initialize(_ =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                sdkReady = true;
                LoadInterstitial();
            });
        });
    }

    private string GetInterstitialId()
    {
        if (config == null) return string.Empty;
#if UNITY_IOS
        return config.iosInterstitialId ?? string.Empty;
#elif UNITY_ANDROID
        return config.androidInterstitialId ?? string.Empty;
#else
        return string.Empty;
#endif
    }

    private void LoadInterstitial()
    {
        if (!sdkReady || loading) return;

        string adUnitId = GetInterstitialId();
        if (string.IsNullOrWhiteSpace(adUnitId))
        {
            Debug.LogWarning("Interstitial ad unit ID is not configured.");
            return;
        }

        loading = true;
        if (interstitial != null)
        {
            interstitial.Destroy();
            interstitial = null;
        }

        InterstitialAd.Load(adUnitId, new AdRequest(), (ad, error) =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                loading = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning("Interstitial ad load failed: " + error);
                    return;
                }

                interstitial = ad;
                interstitial.OnAdFullScreenContentClosed += HandleAdFinished;
                interstitial.OnAdFullScreenContentFailed += _ => HandleAdFinished();
            });
        });
    }

    private void HandleAdFinished()
    {
        MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            if (interstitial != null)
            {
                interstitial.Destroy();
                interstitial = null;
            }
            LoadInterstitial();
        });
    }

    public static void ShowGameOverInterstitial()
    {
#if UNITY_IOS || UNITY_ANDROID
        if (instance == null) return;
        if (instance.interstitial != null && instance.interstitial.CanShowAd())
        {
            instance.interstitial.Show();
        }
        else
        {
            instance.LoadInterstitial();
        }
#endif
    }

    public static void ShowPrivacyOptions()
    {
#if UNITY_IOS || UNITY_ANDROID
        ConsentForm.ShowPrivacyOptionsForm(error =>
        {
            if (error != null)
                Debug.LogWarning("Privacy options form failed: " + error.Message);
        });
#endif
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (interstitial != null)
        {
            interstitial.Destroy();
            interstitial = null;
        }
    }
}
