using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public struct ZineScore
{
    public int bodyCount;
    public int survivalMs;

    public ZineScore(int bodyCount, int survivalMs)
    {
        this.bodyCount = bodyCount;
        this.survivalMs = survivalMs;
    }
}

[Serializable]
public class ZineLeaderboardEntry
{
    public int rank;
    public string nickname;
    public int body_count;
    public int survival_ms;
    public string submitted_at;
}

/// <summary>
/// Stores the device's best run on every platform and owns the native leaderboard API.
/// WebGL continues to use WebGLBridge and never sends duplicate API requests from here.
/// </summary>
public sealed class ZineLeaderboardClient : MonoBehaviour
{
    private const string ApiBaseUrl = "https://zine3dranking.duckdns.org";
    private const int RequestTimeoutSeconds = 10;
    private const float PendingRetrySeconds = 20f;

    private const string LocalBestBodyKey = "zine.localBest.bodyCount";
    private const string LocalBestTimeKey = "zine.localBest.survivalMs";
    private const string PendingBodyKey = "zine.pendingScore.bodyCount";
    private const string PendingTimeKey = "zine.pendingScore.survivalMs";
    private const string PendingExistsKey = "zine.pendingScore.exists";
    private const string SyncedBodyKey = "zine.syncedScore.bodyCount";
    private const string SyncedTimeKey = "zine.syncedScore.survivalMs";
    private const string SyncedExistsKey = "zine.syncedScore.exists";
    private const string PlayerIdKey = "zine.leaderboard.playerId";
    private const string EditTokenKey = "zine.leaderboard.editToken";
    private const string NicknameKey = "zine.leaderboard.nickname";

    private static ZineLeaderboardClient instance;
    private bool scoreRequestRunning;
    private bool playerRequestRunning;
    private bool leaderboardRequestRunning;
    private bool deleteRequestRunning;

    public static event Action<ZineScore> LocalBestChanged;
    public static event Action<ZineLeaderboardEntry[]> LeaderboardLoaded;
    public static event Action<string> NicknameSaved;
    public static event Action<string> RequestFailed;
    public static event Action PlayerDeleted;

    public static ZineLeaderboardEntry[] CurrentLeaderboard { get; private set; } = new ZineLeaderboardEntry[0];
    public static string LastError { get; private set; } = string.Empty;
    public static bool IsRequestInProgress
    {
        get
        {
            return instance != null &&
                   (instance.scoreRequestRunning || instance.playerRequestRunning || instance.leaderboardRequestRunning || instance.deleteRequestRunning);
        }
    }

    public static bool HasPlayer
    {
        get { return !string.IsNullOrEmpty(PlayerPrefs.GetString(EditTokenKey, string.Empty)); }
    }

    public static string Nickname
    {
        get { return PlayerPrefs.GetString(NicknameKey, string.Empty); }
    }

    public static ZineScore LocalBest
    {
        get
        {
            return new ZineScore(
                Mathf.Max(0, PlayerPrefs.GetInt(LocalBestBodyKey, 0)),
                Mathf.Max(0, PlayerPrefs.GetInt(LocalBestTimeKey, 0)));
        }
    }

    public static bool HasLocalBest
    {
        get { return PlayerPrefs.HasKey(LocalBestBodyKey) && PlayerPrefs.HasKey(LocalBestTimeKey); }
    }

    public static bool HasPendingScore
    {
        get { return PlayerPrefs.GetInt(PendingExistsKey, 0) == 1; }
    }

    public static ZineScore PendingScore
    {
        get
        {
            return new ZineScore(
                Mathf.Max(0, PlayerPrefs.GetInt(PendingBodyKey, 0)),
                Mathf.Max(0, PlayerPrefs.GetInt(PendingTimeKey, 0)));
        }
    }

    private static bool NativeApiEnabled
    {
        get
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        CurrentLeaderboard = new ZineLeaderboardEntry[0];
        LastError = string.Empty;
        LocalBestChanged = null;
        LeaderboardLoaded = null;
        NicknameSaved = null;
        RequestFailed = null;
        PlayerDeleted = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BootstrapNativeClient()
    {
        if (NativeApiEnabled)
        {
            EnsureInstance();
        }
    }

    /// <summary>
    /// Saves a run immediately. A worse run never replaces a better local or pending score.
    /// </summary>
    public static void RecordRun(int bodyCount, int survivalMs)
    {
        ZineScore score = new ZineScore(Mathf.Max(0, bodyCount), Mathf.Max(0, survivalMs));
        ZineScore localBest = LocalBest;
        bool isNewBest = !HasLocalBest || IsBetter(score, localBest);

        if (isNewBest)
        {
            PlayerPrefs.SetInt(LocalBestBodyKey, score.bodyCount);
            PlayerPrefs.SetInt(LocalBestTimeKey, score.survivalMs);
        }

        if (NativeApiEnabled)
        {
            ZineScore bestToSend = LocalBest;
            ZineScore pending = PendingScore;
            bool serverMayNeedBest = PlayerPrefs.GetInt(SyncedExistsKey, 0) != 1 ||
                                     IsBetter(bestToSend, SyncedScore);
            if (serverMayNeedBest && (!HasPendingScore || IsBetter(bestToSend, pending)))
            {
                PlayerPrefs.SetInt(PendingBodyKey, bestToSend.bodyCount);
                PlayerPrefs.SetInt(PendingTimeKey, bestToSend.survivalMs);
                PlayerPrefs.SetInt(PendingExistsKey, 1);
            }
        }

        // Flush even when this run was not a new best, so game-over is always a persistence boundary.
        PlayerPrefs.Save();

        if (isNewBest)
        {
            LocalBestChanged?.Invoke(LocalBest);
        }

        if (NativeApiEnabled)
        {
            EnsureInstance().TrySubmitPending();
        }
    }

    /// <summary>
    /// Creates a player on first use, or updates the existing player's nickname.
    /// Listen to NicknameSaved or RequestFailed for completion.
    /// </summary>
    public static void SaveNickname(string nickname)
    {
        if (!NativeApiEnabled)
        {
            Fail("네이티브 앱에서만 랭킹 계정을 변경할 수 있습니다.");
            return;
        }

        nickname = nickname == null ? string.Empty : nickname.Trim();
        if (nickname.Length < 2 || nickname.Length > 16)
        {
            Fail("닉네임은 2자 이상 16자 이하여야 합니다.");
            return;
        }

        ZineLeaderboardClient client = EnsureInstance();
        if (client.playerRequestRunning || client.deleteRequestRunning)
        {
            Fail("닉네임 요청이 이미 진행 중입니다.");
            return;
        }

        client.StartCoroutine(HasPlayer
            ? client.UpdateNicknameRoutine(nickname)
            : client.CreatePlayerRoutine(nickname));
    }

    /// <summary>
    /// Loads the public leaderboard asynchronously. Listen to LeaderboardLoaded or RequestFailed.
    /// </summary>
    public static void LoadLeaderboard(int limit = 50)
    {
        if (!NativeApiEnabled)
        {
            Fail("네이티브 앱에서만 Unity 랭킹을 불러올 수 있습니다.");
            return;
        }

        ZineLeaderboardClient client = EnsureInstance();
        if (client.leaderboardRequestRunning)
        {
            return;
        }

        client.StartCoroutine(client.LoadLeaderboardRoutine(Mathf.Clamp(limit, 1, 100)));
    }

    /// <summary>
    /// Deletes the registered player and their scores on the server.
    /// The caller provides the confirmation UI. Local credentials are cleared only
    /// after the server confirms {deleted:true}. Listen to PlayerDeleted or RequestFailed.
    /// </summary>
    public static void DeletePlayer()
    {
        if (!NativeApiEnabled)
        {
            Fail("네이티브 앱에서만 랭킹 계정을 삭제할 수 있습니다.");
            return;
        }

        ZineLeaderboardClient client = EnsureInstance();
        if (client.playerRequestRunning || client.scoreRequestRunning || client.deleteRequestRunning)
        {
            Fail("다른 랭킹 요청이 진행 중에는 계정을 삭제할 수 없습니다.");
            return;
        }

        client.StartCoroutine(client.DeletePlayerRoutine());
    }

    /// <summary>
    /// Explicitly retries the locally pending best score, if a player is registered.
    /// </summary>
    public static void RetryPendingScore()
    {
        if (NativeApiEnabled)
        {
            EnsureInstance().TrySubmitPending();
        }
    }

    public static bool IsBetter(ZineScore candidate, ZineScore current)
    {
        return candidate.bodyCount > current.bodyCount ||
               (candidate.bodyCount == current.bodyCount && candidate.survivalMs > current.survivalMs);
    }

    private static ZineLeaderboardClient EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject host = new GameObject("ZineLeaderboardClient");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<ZineLeaderboardClient>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        StartCoroutine(RetryLoop());
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            TrySubmitPending();
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused)
        {
            TrySubmitPending();
        }
    }

    private IEnumerator RetryLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(PendingRetrySeconds);
            TrySubmitPending();
        }
    }

    private void TrySubmitPending()
    {
        if (!NativeApiEnabled || scoreRequestRunning || deleteRequestRunning || !HasPendingScore || !HasPlayer)
        {
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            SetError("인터넷에 연결되어 있지 않습니다.");
            return;
        }

        StartCoroutine(SubmitScoreRoutine(PendingScore));
    }

    private IEnumerator CreatePlayerRoutine(string nickname)
    {
        playerRequestRunning = true;
        NicknamePayload payload = new NicknamePayload { nickname = nickname };
        using (UnityWebRequest request = CreateJsonRequest("POST", "/api/v1/players", JsonUtility.ToJson(payload)))
        {
            yield return request.SendWebRequest();

            if (RequestSucceeded(request))
            {
                PlayerResponse response = JsonUtility.FromJson<PlayerResponse>(request.downloadHandler.text);
                if (response != null && !string.IsNullOrEmpty(response.edit_token))
                {
                    PlayerPrefs.SetString(PlayerIdKey, response.player_id ?? string.Empty);
                    PlayerPrefs.SetString(EditTokenKey, response.edit_token);
                    PlayerPrefs.SetString(NicknameKey, response.nickname ?? nickname);
                    PlayerPrefs.Save();
                    ClearError();
                    NicknameSaved?.Invoke(Nickname);
                }
                else
                {
                    SetError("플레이어 등록 응답이 올바르지 않습니다.");
                }
            }
            else
            {
                SetRequestError(request, "플레이어를 등록하지 못했습니다.");
            }
        }

        playerRequestRunning = false;
        TrySubmitPending();
    }

    private IEnumerator UpdateNicknameRoutine(string nickname)
    {
        playerRequestRunning = true;
        NicknamePayload payload = new NicknamePayload { nickname = nickname };
        using (UnityWebRequest request = CreateJsonRequest("PATCH", "/api/v1/players/me", JsonUtility.ToJson(payload), true))
        {
            yield return request.SendWebRequest();

            if (RequestSucceeded(request))
            {
                PlayerResponse response = JsonUtility.FromJson<PlayerResponse>(request.downloadHandler.text);
                string savedNickname = response != null && !string.IsNullOrEmpty(response.nickname)
                    ? response.nickname
                    : nickname;
                PlayerPrefs.SetString(NicknameKey, savedNickname);
                PlayerPrefs.Save();
                ClearError();
                NicknameSaved?.Invoke(savedNickname);
            }
            else
            {
                HandleAuthorizationFailure(request);
                SetRequestError(request, "닉네임을 변경하지 못했습니다.");
            }
        }

        playerRequestRunning = false;
    }

    private IEnumerator DeletePlayerRoutine()
    {
        deleteRequestRunning = true;
        using (UnityWebRequest request = CreateJsonRequest("DELETE", "/api/v1/players/me", "{}", true))
        {
            yield return request.SendWebRequest();

            if (RequestSucceeded(request))
            {
                DeletePlayerResponse response = null;
                try
                {
                    response = JsonUtility.FromJson<DeletePlayerResponse>(request.downloadHandler.text);
                }
                catch (ArgumentException)
                {
                    // Fall through to the invalid-response error below.
                }

                if (response != null && response.deleted)
                {
                    PlayerPrefs.DeleteKey(PlayerIdKey);
                    PlayerPrefs.DeleteKey(EditTokenKey);
                    PlayerPrefs.DeleteKey(NicknameKey);
                    PlayerPrefs.DeleteKey(PendingBodyKey);
                    PlayerPrefs.DeleteKey(PendingTimeKey);
                    PlayerPrefs.DeleteKey(PendingExistsKey);
                    PlayerPrefs.DeleteKey(SyncedBodyKey);
                    PlayerPrefs.DeleteKey(SyncedTimeKey);
                    PlayerPrefs.DeleteKey(SyncedExistsKey);
                    PlayerPrefs.Save();
                    CurrentLeaderboard = new ZineLeaderboardEntry[0];
                    ClearError();
                    PlayerDeleted?.Invoke();
                    LoadLeaderboard();
                }
                else
                {
                    SetError("계정 삭제 응답이 올바르지 않습니다.");
                }
            }
            else
            {
                SetRequestError(request, "계정을 삭제하지 못했습니다.");
            }
        }

        deleteRequestRunning = false;
    }

    private IEnumerator SubmitScoreRoutine(ZineScore submittedScore)
    {
        scoreRequestRunning = true;
        bool shouldSubmitNewerPending = false;
        ScorePayload payload = new ScorePayload
        {
            body_count = submittedScore.bodyCount,
            survival_ms = submittedScore.survivalMs
        };

        using (UnityWebRequest request = CreateJsonRequest("POST", "/api/v1/scores", JsonUtility.ToJson(payload), true))
        {
            yield return request.SendWebRequest();

            if (RequestSucceeded(request))
            {
                PlayerPrefs.SetInt(SyncedBodyKey, submittedScore.bodyCount);
                PlayerPrefs.SetInt(SyncedTimeKey, submittedScore.survivalMs);
                PlayerPrefs.SetInt(SyncedExistsKey, 1);
                ZineScore pendingNow = PendingScore;
                if (HasPendingScore && !IsBetter(pendingNow, submittedScore))
                {
                    ClearPendingScore();
                }
                else if (HasPendingScore)
                {
                    PlayerPrefs.Save();
                    shouldSubmitNewerPending = true;
                }

                ClearError();
            }
            else
            {
                HandleAuthorizationFailure(request);
                SetRequestError(request, "최고 기록을 서버에 등록하지 못했습니다.");
            }
        }

        scoreRequestRunning = false;

        // A better run may have completed while this request was in flight.
        if (shouldSubmitNewerPending && HasPlayer)
        {
            TrySubmitPending();
        }
    }

    private IEnumerator LoadLeaderboardRoutine(int limit)
    {
        leaderboardRequestRunning = true;
        using (UnityWebRequest request = UnityWebRequest.Get(ApiBaseUrl + "/api/v1/leaderboard?limit=" + limit))
        {
            request.timeout = RequestTimeoutSeconds;
            yield return request.SendWebRequest();

            if (RequestSucceeded(request))
            {
                LeaderboardResponse response = JsonUtility.FromJson<LeaderboardResponse>(request.downloadHandler.text);
                CurrentLeaderboard = response != null && response.entries != null
                    ? response.entries
                    : new ZineLeaderboardEntry[0];
                ClearError();
                LeaderboardLoaded?.Invoke(CurrentLeaderboard);
            }
            else
            {
                SetRequestError(request, "랭킹을 불러오지 못했습니다.");
            }
        }

        leaderboardRequestRunning = false;
    }

    private static UnityWebRequest CreateJsonRequest(string method, string path, string json, bool authorized = false)
    {
        UnityWebRequest request = new UnityWebRequest(ApiBaseUrl + path, method);
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Accept", "application/json");
        request.timeout = RequestTimeoutSeconds;

        if (authorized)
        {
            request.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString(EditTokenKey, string.Empty));
        }

        return request;
    }

    private static bool RequestSucceeded(UnityWebRequest request)
    {
        return request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300;
    }

    private static void HandleAuthorizationFailure(UnityWebRequest request)
    {
        if (request.responseCode != 401)
        {
            return;
        }

        PlayerPrefs.DeleteKey(PlayerIdKey);
        PlayerPrefs.DeleteKey(EditTokenKey);
        PlayerPrefs.Save();
    }

    private static void ClearPendingScore()
    {
        PlayerPrefs.DeleteKey(PendingBodyKey);
        PlayerPrefs.DeleteKey(PendingTimeKey);
        PlayerPrefs.DeleteKey(PendingExistsKey);
        PlayerPrefs.Save();
    }

    private static ZineScore SyncedScore
    {
        get
        {
            return new ZineScore(
                Mathf.Max(0, PlayerPrefs.GetInt(SyncedBodyKey, 0)),
                Mathf.Max(0, PlayerPrefs.GetInt(SyncedTimeKey, 0)));
        }
    }

    private static void SetRequestError(UnityWebRequest request, string fallback)
    {
        string message = fallback;
        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            message = "네트워크 연결을 확인해 주세요.";
        }
        else
        {
            ErrorResponse response = null;
            string responseText = request.downloadHandler == null ? string.Empty : request.downloadHandler.text;
            if (!string.IsNullOrEmpty(responseText))
            {
                try
                {
                    response = JsonUtility.FromJson<ErrorResponse>(responseText);
                }
                catch (ArgumentException)
                {
                    // Keep the user-facing fallback for non-JSON proxy/server errors.
                }
            }

            if (response != null && !string.IsNullOrEmpty(response.error))
            {
                message = response.error;
            }
        }

        SetError(message);
    }

    private static void ClearError()
    {
        LastError = string.Empty;
    }

    private static void Fail(string message)
    {
        SetError(message);
    }

    private static void SetError(string message)
    {
        LastError = message;
        RequestFailed?.Invoke(message);
    }

    [Serializable]
    private class NicknamePayload
    {
        public string nickname;
    }

    [Serializable]
    private class ScorePayload
    {
        public int body_count;
        public int survival_ms;
    }

    [Serializable]
    private class PlayerResponse
    {
        public string player_id;
        public string edit_token;
        public string nickname;
    }

    [Serializable]
    private class DeletePlayerResponse
    {
        public bool deleted;
    }

    [Serializable]
    private class LeaderboardResponse
    {
        public ZineLeaderboardEntry[] entries;
    }

    [Serializable]
    private class ErrorResponse
    {
        public string error;
    }
}
