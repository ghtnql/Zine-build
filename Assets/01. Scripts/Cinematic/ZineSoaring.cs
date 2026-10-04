using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;

public class ZineSoaring : MonoBehaviour
{
    static public ZineSoaring instance;

    public PlayableDirector pd;
    public ZineCinematics zinema;
    //snake.addbody();
    private void Awake()
    {
        instance = this;
        zinema = FindObjectOfType<ZineCinematics>();
    }

    public void StartSoaring()
    {
        zinema.TurnOnBodies();
        pd.Play();
    }
}
