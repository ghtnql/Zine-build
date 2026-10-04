using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Integration verification only: editor networking is disabled; no server writes.
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
    static Button NamedButton(string name) { return Resources.FindObjectsOfTypeAll<Button>().First(b => b.name==name && b.gameObject.scene.IsValid()); }
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static void Run()
    {
        output = Path.GetFullPath("Builds/Verification"); Directory.CreateDirectory(output);
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Exception || type == LogType.Error) errors++; };
        previousOptions = EditorSettings.enterPlayModeOptionsEnabled;
        previousFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene("Assets/00. Scenes/OriginalScene.unity");
        var gameType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var game = EditorWindow.GetWindow(gameType);
        game.position = new Rect(0, 0, 1600, 760);
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
                    var entries=Enumerable.Range(1,10).Select(i=>new ZineLeaderboardEntry {rank=i,nickname="테스트"+i,body_count=21-i,survival_ms=i*10000}).ToArray();
                    Call("OnLeaderboardLoaded", (object)entries); break;
                case 2:
                    Capture("03-ranking");
                    var input=Field<InputField>("nicknameInput"); string saved=input.text;
                    input.text="취소해야함"; Field<Button>("cancelButton").onClick.Invoke();
                    Check(input.text==saved, "Cancel restores saved nickname");
                    var rows=Field<RectTransform>("rowsScrollRect"); rows.GetComponent<ScrollRect>().verticalNormalizedPosition=0;
                    Check(Field<Text>("rowsText").text.Contains("10위"), "All ten ranks loaded into scroll");
                    break;
                case 3:
                    Capture("04-ranking-bottom");
                    Field<Button>("closeButton").onClick.Invoke();
                    Check(!NativeLeaderboardUI.IsModalOpen && Time.timeScale==1, "Close resumes gameplay");
                    heading=snake.transform.rotation; snake.OnButtonPress(NamedButton("BtnRight")); break;
                case 4:
                    Check(Quaternion.Angle(heading,snake.transform.rotation)>1, "Right control turns snake"); snake.OnButtonUp();
                    NativeLeaderboardUI.ShowGameOver(5,12000); break;
                case 5:
                    Check(NativeLeaderboardUI.IsModalOpen && Field<Text>("runText").text.Contains("5"), "Game-over ranking shows actual run values");
                    Capture("05-gameover"); NativeLeaderboardUI.CloseModal();
                    Check(!NativeLeaderboardUI.IsModalOpen, "Back closes ranking");
                    Check(errors==0,"No runtime errors");
                    File.WriteAllText(Path.Combine(output,"result.json"), "{\"passed\":true,\"runtimeErrors\":"+errors+",\"screenWidth\":"+Screen.width+",\"screenHeight\":"+Screen.height+",\"realDeviceKeyboardVerified\":false}");
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
