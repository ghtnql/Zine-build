using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class ZineLocale
{
    public static bool IsKorean
    {
        get
        {
#if UNITY_EDITOR
            string verify = Environment.GetEnvironmentVariable("ZINE_VERIFY_LOCALE");
            if (!string.IsNullOrEmpty(verify))
            {
                string v = verify.Trim().ToLowerInvariant();
                if (v == "ko") return true;
                if (v == "en") return false;
            }
#endif
            return Application.systemLanguage == SystemLanguage.Korean;
        }
    }

    public static string T(string ko, string en) => IsKorean ? ko : en;

    public static void ApplySceneLabels()
    {
        if (!IsKorean) return;
        Font koFont = Resources.Load<Font>("ZineRankingFont");
        Text[] allTexts = Resources.FindObjectsOfTypeAll<Text>();
        foreach (Text t in allTexts)
        {
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            string s = t.text;
            string replaced = null;
            switch (s)
            {
                case "Quit": replaced = "종료"; break;
                case "Retry": replaced = "다시 하기"; break;
                case "Game Over": replaced = "게임 종료"; break;
                case "Coin :": replaced = "코인 :"; break;
                case "Time :": replaced = "시간 :"; break;
            }
            if (replaced == null)
            {
                if (s.StartsWith("Coin :", StringComparison.Ordinal))
                    replaced = "코인 :" + s.Substring("Coin :".Length);
                else if (s.StartsWith("Time :", StringComparison.Ordinal))
                    replaced = "시간 :" + s.Substring("Time :".Length);
            }
            if (replaced == null) continue;
            t.text = replaced;
            if (koFont != null) t.font = koFont;
        }
    }
}
