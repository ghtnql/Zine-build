using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Integration verification: production GET invoked explicitly; no server writes.
public static class ZineNativeVerification
{
    static int stage;
    static double next;
    static Vector3 before;
    static Quaternion heading;
    static NativeLeaderboardUI ui;
    static Snake snake;
    static string output;
    static bool previousOptions;
    static EnterPlayModeOptions previousFlags;
    static EditorWindow gameWindow;
    static int errors;
    static double networkDeadline;
    static ZineLeaderboardEntry[] expected;
    [Serializable] private sealed class LiveResponse { public ZineLeaderboardEntry[] entries; }
    static Button NamedButton(string name) { return Resources.FindObjectsOfTypeAll<Button>().First(b => b.name==name && b.gameObject.scene.IsValid()); }
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static void Run()
    {
        int width=1600, height=760;
        int.TryParse(Environment.GetEnvironmentVariable("ZINE_VERIFY_WIDTH") ?? "1600",out width);
        int.TryParse(Environment.GetEnvironmentVariable("ZINE_VERIFY_HEIGHT") ?? "760",out height);
        Check(width>=640 && height>=320,"Verification viewport is explicit and usable");
        output = Path.GetFullPath("Builds/Verification-Live-"+width); Directory.CreateDirectory(output);
        string fixture=Environment.GetEnvironmentVariable("ZINE_EXPECTED_RANKING_JSON");
        Check(!string.IsNullOrEmpty(fixture), "Explicit live API comparison fixture supplied");
        expected=JsonUtility.FromJson<LiveResponse>(File.ReadAllText(fixture)).entries;
        Check(expected != null && expected.Length>0, "Live server records exist");
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Exception || type == LogType.Error) errors++; };
        previousOptions = EditorSettings.enterPlayModeOptionsEnabled;
        previousFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene("Assets/00. Scenes/OriginalScene.unity");
        var gameType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var game = EditorWindow.GetWindow(gameType);
        game.position = new Rect(0, 0, width, height);
        game.Show(); game.Focus(); gameWindow=game;
        EditorApplication.update += Tick;
        next = EditorApplication.timeSinceStartup + 5;
        EditorApplication.isPlaying = true;
    }
    static T Field<T>(string name) { return (T)typeof(NativeLeaderboardUI).GetField(name, Private).GetValue(ui); }
    static void Call(string name, params object[] args) { typeof(NativeLeaderboardUI).GetMethod(name, Private).Invoke(ui, args); }
    static void Check(bool okay, string message) { if (!okay) throw new Exception(message); Debug.Log("ZINE_CHECK " + message); }
    static void Capture(string name)
    {
        RenderTexture source = null;
        Type type = gameWindow.GetType();
        while(type != null && source == null)
        {
            foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                if (typeof(RenderTexture).IsAssignableFrom(field.FieldType)) source = field.GetValue(gameWindow) as RenderTexture;
            foreach(var prop in type.GetProperties(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                if (typeof(RenderTexture).IsAssignableFrom(prop.PropertyType) && prop.GetIndexParameters().Length==0)
                    source = prop.GetValue(gameWindow,null) as RenderTexture;
            type=type.BaseType;
        }
        Check(source!=null, "Game view render texture exists");
        RenderTexture previous=RenderTexture.active; RenderTexture.active=source;
        var texture=new Texture2D(source.width,source.height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0);texture.Apply();RenderTexture.active=previous;
        Check(texture != null && texture.width > 500, "Rendered frame: " + name);
        File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }
    static void Tick()
    {
        if (gameWindow!=null) { gameWindow.Focus(); gameWindow.Repaint(); }
        EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if (EditorApplication.timeSinceStartup < next || !EditorApplication.isPlaying) return;
        if (stage==0 && (Time.frameCount<10 || UnityEngine.Object.FindObjectOfType<Snake>()==null || typeof(Snake).GetField("txtCoin",Private).GetValue(UnityEngine.Object.FindObjectOfType<Snake>())==null)) return;
        next = EditorApplication.timeSinceStartup + 0.5;
        try
        {
            if (stage==2 && ZineLeaderboardClient.CurrentLeaderboard.Length==0)
            {
                if (EditorApplication.timeSinceStartup>networkDeadline) throw new Exception("Live Unity GET timed out: "+ZineLeaderboardClient.LastError);
                return;
            }
            switch(stage++)
            {
                case 0:
                    ui = UnityEngine.Object.FindObjectOfType<NativeLeaderboardUI>(); snake = UnityEngine.Object.FindObjectOfType<Snake>();
                    Check(ui != null && snake != null, "Native UI created in real scene");
                    Capture("01-start");
                    snake.OnButtonClick(GameObject.Find("BtnPlay"));
                    typeof(Snake).GetField("isMobile", Private).SetValue(snake, true);
                    before = snake.transform.position; heading = snake.transform.rotation;
                    snake.OnButtonPress(NamedButton("BtnLeft"));
                    break;
                case 1:
                    Check(Vector3.Distance(before,snake.transform.position)>0.1f, "Gameplay moves forward");
                    Check(Quaternion.Angle(heading,snake.transform.rotation)>1f, "Left control turns snake");
                    snake.OnButtonUp(); Capture("02-gameplay");
                    Field<Button>("openButton").onClick.Invoke();
                    Check(NativeLeaderboardUI.IsModalOpen && Time.timeScale==0, "Ranking pauses gameplay");
                    var client=(ZineLeaderboardClient)typeof(ZineLeaderboardClient).GetMethod("EnsureInstance",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
                    var routine=(IEnumerator)typeof(ZineLeaderboardClient).GetMethod("LoadLeaderboardRoutine",Private).Invoke(client,new object[]{10});
                    client.StartCoroutine(routine); networkDeadline=EditorApplication.timeSinceStartup+35;
                    break;
                case 2:
                    var live=ZineLeaderboardClient.CurrentLeaderboard;
                    Check(live.Length==expected.Length,"Unity HTTP GET returns same record count as web API");
                    for (int i=0;i<live.Length;i++)
                    {
                        Check(live[i].rank==expected[i].rank && live[i].nickname==expected[i].nickname && live[i].body_count==expected[i].body_count && live[i].survival_ms==expected[i].survival_ms,"Live record preserved at rank "+live[i].rank);
                        var row=Field<RectTransform>("rowsContent").Find("RankRow"+live[i].rank);
                        Check(row!=null,"Four-column row exists for rank "+live[i].rank);
                        Check(row.Find("NicknameCell").GetComponent<Text>().text==live[i].nickname,"Nickname rendered literally at rank "+live[i].rank);
                        Check(row.Find("CoinCell").GetComponent<Text>().text==live[i].body_count.ToString(),"Coin cell matches server at rank "+live[i].rank);
                    }
                    Capture("03-ranking-live");
                    var input=Field<InputField>("nicknameInput"); string saved=input.text;
                    input.text="취소해야함"; Field<Button>("cancelButton").onClick.Invoke();
                    Check(input.text==saved, "Cancel restores saved nickname");
                    var rows=Field<RectTransform>("rowsScrollRect"); rows.GetComponent<ScrollRect>().verticalNormalizedPosition=0;
                    Check(Field<RectTransform>("rowsContent").Find("RankRow"+expected[expected.Length-1].rank)!=null, "Last existing rank reachable in scroll");
                    break;
                case 3:
                    Capture("04-ranking-live-bottom");
                    Field<Button>("closeButton").onClick.Invoke();
                    Check(!NativeLeaderboardUI.IsModalOpen && Time.timeScale==1, "Close resumes gameplay");
                    heading=snake.transform.rotation; snake.OnButtonPress(NamedButton("BtnRight")); break;
                case 4:
                    Check(Quaternion.Angle(heading,snake.transform.rotation)>1, "Right control turns snake"); snake.OnButtonUp();
                    NativeLeaderboardUI.ShowGameOver(5,12000); break;
                case 5:
                    Check(NativeLeaderboardUI.IsModalOpen && Field<Text>("runText").text.Contains("5"), "Game-over UI formats explicit local test values");
                    Capture("05-gameover-local-ui-test"); NativeLeaderboardUI.CloseModal();
                    Check(!NativeLeaderboardUI.IsModalOpen, "Back closes ranking");
                    Check(errors==0,"No runtime errors");
                    File.WriteAllText(Path.Combine(output,"result.json"), "{\"passed\":true,\"liveRecordCount\":"+expected.Length+",\"runtimeErrors\":"+errors+",\"screenWidth\":"+Screen.width+",\"screenHeight\":"+Screen.height+",\"realDeviceKeyboardVerified\":false}");
                    Finish(0);break;
            }
        }
        catch(Exception ex) { Debug.LogException(ex); File.WriteAllText(Path.Combine(output,"failure.txt"),ex.ToString()); Finish(1); }
    }
    static void Finish(int code)
    {
        EditorApplication.update-=Tick; Time.timeScale=1;
        EditorApplication.isPlaying=false;
        EditorSettings.enterPlayModeOptionsEnabled=previousOptions;
        EditorSettings.enterPlayModeOptions=previousFlags;
        EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
    }
}
