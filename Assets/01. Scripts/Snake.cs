using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class Snake : MonoBehaviour
{
    public float speedMove = 5f;
    public float speedTurn = 120f;

    Transform coin;
    List<Transform> bodys = new List<Transform>();
    Renderer render;

    int coinCnt = 0;
    public bool isDead;
    public AudioSource aSource;

    Text txtCoin;
    Text txtTime;
    Text txtNtc;

    GameObject pnlColor;
    Image pnlOver;
    Image pnlBtn;
    bool isMobile = false;

    float startTime;
    float dir = 0;
    bool pausedForLeaderboard = false;
    float timeScaleBeforeLeaderboard = 1f;

    [Header("How many Coins to be a Dragon")]
    public int soaringGoal = 1;

    void Start()
    {
        Application.targetFrameRate = 60;
        InitGame();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && NativeLeaderboardUI.IsModalOpen)
        {
            NativeLeaderboardUI.CloseModal();
            return;
        }
        if (isDead)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
            foreach (Transform body in bodys)
            {
                body.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            }
            return;
        }

        if (isDead != true)
            MoveHead();
        MoveTails();
        SetScore();
    }

    #region 이동
    private void MoveHead()
    {
        float level = SetLevel();
        float amtMove = speedMove * Time.deltaTime * level;
        transform.Translate(Vector3.forward * amtMove, Space.Self);

        float keyMove;
        if (!isMobile)
        {
            pnlBtn.gameObject.SetActive(false);
            keyMove = Input.GetAxis("Horizontal");
        }
        else
        {
            pnlBtn.gameObject.SetActive(true);
            keyMove = dir;
        }

        float amtRot = speedTurn * keyMove * Time.deltaTime * level;
        transform.Rotate(Vector3.up * amtRot);
    }

    private void MoveTails()
    {
        float level = SetLevel();
        Vector3 targetPos = transform.position;
        Quaternion targetAng = transform.rotation;

        foreach (Transform body in bodys)
        {
            body.position = Vector3.Lerp(body.position, targetPos, 5 * Time.deltaTime * level);
            body.rotation = Quaternion.Lerp(body.rotation, targetAng, 5 * Time.deltaTime * level);
            targetPos = body.position;
            targetAng = body.rotation;
        }
    }

    private void MoveBody()
    {
        float level = SetLevel();
        float amtMove = speedMove * Time.deltaTime * level;
        for (int i = 0; i < bodys.Count; i++)
        {
            if (i == 0)
            {
                bodys[i].LookAt(transform);
            }
            else
            {
                bodys[i].LookAt(bodys[i - 1]);
                if (Math.Abs(bodys[i].transform.position.x) > 15 || Math.Abs(bodys[i].transform.position.z) > 15)
                {
                    bodys[i].transform.position = new Vector3(bodys[i - 1].position.x, bodys[i - 1].position.y, bodys[i - 1].position.z * 1.5f);
                }
            }
            bodys[i].Translate(Vector3.forward * amtMove);
        }
    }
    #endregion

    #region 세팅
    private void SetScore()
    {
        txtCoin.text = "Coin : " + bodys.Count.ToString();
        float t = Time.time - startTime;
        int h = Mathf.FloorToInt(t / 3600);
        int m = Mathf.FloorToInt(t % 3600 / 60);
        float s = t % 60;
        if (!isDead) txtTime.text = string.Format("Time : {0:00}:{1:00}:{2:00.0}", h, m, s);
    }

    private float SetLevel()
    {
        float level = 1;
        if (bodys.Count >= 10 && bodys.Count < 15)
        {
            txtNtc.text = "속도 1.1배!";
            level = 1.1f;
            aSource.pitch = 1.05f;
        }
        else if (bodys.Count >= 15 && bodys.Count < 20)
        {
            txtNtc.text = "속도 1.3배!";
            level = 1.3f;
        }
        else if (bodys.Count >= 20)
        {
            txtNtc.text = "속도 1.5배!";
            level = 1.5f;
            aSource.pitch = 1.1f;
        }

        float time = Time.time - startTime;
        if (time * 0.01f >= 1)
        {
            txtNtc.text = "속도 무한증가!";
            level *= time * 0.01f;
            aSource.pitch = 1.2f;
        }
        if (level >= 2f) aSource.pitch = 1.3f;

        foreach (Transform body in bodys)
        {
            Animator anim = body.GetComponent<Animator>();
            anim.speed = level;
        }
        return level;
    }
    #endregion

    #region 이벤트
    private void OnCollisionEnter(Collision col)
    {
        switch (col.transform.tag)
        {
            case "COIN":
                break;
            case "WALL":
            case "TAIL":
                if (isDead) return;
                ResetGame();
                break;
        }
    }

    public void ResetGame()
    {
        isDead = true;
        GameObject.Find("BtnPause").SetActive(false);
        pnlOver.gameObject.SetActive(true);
        pnlBtn.gameObject.SetActive(false);
        WebGLBridge.SetLeaderboardEnabled(true);

        int survivalMs = Mathf.Max(0, Mathf.RoundToInt((Time.time - startTime) * 1000f));
        ZineLeaderboardClient.RecordRun(bodys.Count, survivalMs);
        WebGLBridge.ShowLeaderboard(bodys.Count, survivalMs);
    }

    public void PauseForLeaderboard()
    {
        if (isDead || pausedForLeaderboard) return;
        pausedForLeaderboard = true;
        timeScaleBeforeLeaderboard = Time.timeScale;
        Time.timeScale = 0;
        WebGLBridge.SetLeaderboardEnabled(true);
    }

    public void ResumeAfterLeaderboard()
    {
        if (!pausedForLeaderboard) return;
        pausedForLeaderboard = false;
        Time.timeScale = timeScaleBeforeLeaderboard;
        WebGLBridge.SetLeaderboardEnabled(Time.timeScale <= 0f);
    }

    public void AddBody()
    {
        int bodyCnt = bodys.Count;
        if (bodyCnt == soaringGoal)
        {
            if (ZineSoaring.instance != null)
            {
                int survivalMs = Mathf.Max(0, Mathf.RoundToInt((Time.time - startTime) * 1000f));
                ZineLeaderboardClient.RecordRun(bodys.Count, survivalMs);
                ZineSoaring.instance.StartSoaring();
                isDead = true;
                WebGLBridge.SetLeaderboardEnabled(true);
            }
            else
            {
                AddBodyReally();
            }
        }
        else
        {
            AddBodyReally();
        }
    }

    public void AddBodyReally()
    {
        Vector3 pos = transform.position;
        int cnt = bodys.Count;
        GameObject obj = Resources.Load("Body") as GameObject;
        if (cnt > 0)
        {
            pos = new Vector3(bodys[cnt - 1].position.x, bodys[cnt - 1].position.y, bodys[cnt - 1].position.z * 1.6f);
        }

        obj.tag = cnt <= 2 ? "Untagged" : "TAIL";
        GameObject body = Instantiate(obj, pos, Quaternion.identity);
        StartCoroutine(SetCollider(body));
        bodys.Add(body.transform);
    }

    IEnumerator SetCollider(GameObject tail)
    {
        yield return new WaitForSeconds(0.2f);
        tail.AddComponent<SphereCollider>();
    }
    #endregion

    #region 버튼 컨트롤
    public void OnButtonClick(GameObject button)
    {
        switch (button.name)
        {
            case "BtnRetry":
                SceneManager.LoadScene(0);
                break;
            case "BtnQuit":
#if UNITY_WEBGL && !UNITY_EDITOR
                WebGLBridge.ReloadPage();
#else
                Application.Quit();
#endif
                break;
            case "BtnPause":
                isDead = true;
                button.SetActive(false);
                button.GetComponentInParent<Transform>().Find("BtnPlay").gameObject.SetActive(true);
                if (!pnlColor.activeSelf) pnlColor.SetActive(true);
                Time.timeScale = 0;
                WebGLBridge.SetLeaderboardEnabled(true);
                break;
            case "BtnPlay":
                isDead = false;
                button.SetActive(false);
                button.GetComponentInParent<Transform>().Find("BtnPause").gameObject.SetActive(true);
                if (pnlColor.activeSelf) pnlColor.SetActive(false);
                Time.timeScale = 1;
                WebGLBridge.SetLeaderboardEnabled(false);
                break;
        }
    }

    public void OnButtonPress(Button button)
    {
        switch (button.name)
        {
            case "BtnLeft": dir = -1; break;
            case "BtnRight": dir = 1; break;
        }
    }

    public void OnButtonUp()
    {
        dir = 0;
    }
    #endregion

    #region 게임 초기화
    public void InitGame()
    {
        coin = GameObject.Find("Coin").transform;
        txtCoin = GameObject.Find("TextCoin").GetComponent<Text>();
        txtTime = GameObject.Find("TextTime").GetComponent<Text>();
        txtNtc = GameObject.Find("TextNotice").GetComponent<Text>();

        startTime = Time.time;
        Time.timeScale = 0;
        WebGLBridge.SetLeaderboardEnabled(true);

        pnlColor = GameObject.Find("PanelColor");
        pnlOver = GameObject.Find("PanelOver").GetComponent<Image>();
        pnlOver.gameObject.SetActive(false);

        pnlBtn = GameObject.Find("PanelBtn").GetComponent<Image>();
        isMobile = Application.isMobilePlatform;
#if UNITY_WEBGL && !UNITY_EDITOR
        isMobile = WebGLBridge.IsMobileBrowser();
#endif
        pnlBtn.gameObject.SetActive(isMobile);
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
    #endregion
}
