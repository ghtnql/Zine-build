using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZineCinematics : MonoBehaviour
{
    public float lerpDamping = 15f;
    public Transform[] fields;
    //몇 초 뒤에 게임 오버 시킬 것인지
    public float soaringTimer = 40;

    private List<Transform> bodies = new List<Transform>();
    private MeshRenderer[] renderers;
    private bool isSoaring = false;
    private float timer = 0;
    private Vector3 prePos;
    private Snake originZine;
    private void Awake()
    {
        for(int i = 0; i < 90; i ++)
        {
            AddBodyReally();
        }
        prePos = transform.position;
        originZine = FindObjectOfType<Snake>();
        renderers = GetComponentsInChildren<MeshRenderer>();
        foreach(MeshRenderer mr in renderers)
        {
            mr.enabled = false;
        }
    }

    private void Update()
    {
        MoveTails();
        if(isSoaring == true)
            Soar();
        Vector3 dir = transform.position - prePos;
        Quaternion rot = Quaternion.LookRotation(dir);
        transform.rotation = rot;
        prePos = transform.position;
    }

    //ZineSoaring이 PlayableDirector를 실행시키면서 지네 몸통들을 활성화 함
    public void TurnOnBodies()
    {
        foreach (MeshRenderer mr in renderers)
        {
            mr.enabled = true;
        }
        foreach (Transform body in bodies)
        {
            body.gameObject.SetActive(true);
        }
    }

    //PlayableDirector로부터 받는 신호, 원래의 필드를 안보이게 하기 위함
    public void ReceiveSignal()
    {
        GetComponent<MeshRenderer>().enabled = true;
        for(int i = 0; i < fields.Length; i++)
        {
            fields[i].gameObject.SetActive(false);
        }
        Debug.Log("Fucke");
        isSoaring = !isSoaring;
        StartCoroutine(ResetGameInSomeday());
    }

    IEnumerator ResetGameInSomeday()
    {
        Debug.Log("왜 리셋 안됨?");
        yield return new WaitForSeconds(soaringTimer);
        originZine.ResetGame();
    }
    public void Soar()
    {
        //transform.position += Vector3.forward;
        timer += Time.deltaTime;
        Vector3 triedVector = new Vector3(Mathf.Sin(timer * 0.5f) * 5f, timer * 0.08f, Mathf.Cos(timer * 0.5f) *5f);
        transform.position += triedVector * Time.deltaTime;
    }
    public void AddBodyReally()
    {
        GameObject obj = Resources.Load("Body") as GameObject;
        GameObject body = Instantiate(obj, transform.position, Quaternion.identity);

        bodies.Add(body.transform);
        body.SetActive(false);
    }
    private void MoveTails()
    {

        Vector3 targetPos = transform.position;
        Quaternion targetAng = transform.rotation;
        //Vector3 dirOrigin = transform.position - bodies[0].position;

        bodies[0].position = Vector3.Lerp(bodies[0].position, targetPos, lerpDamping * Time.deltaTime);
        bodies[0].rotation = Quaternion.Lerp(bodies[0].rotation, targetAng, lerpDamping * Time.deltaTime);
        
        for (int i = 1; i < bodies.Count; i++)
        {
            Vector3 dir = bodies[i].position - bodies[i - 1].position;
            bodies[i].position = Vector3.Lerp(bodies[i].position, (bodies[i-1].position + dir.normalized * 0.8f), lerpDamping * Time.deltaTime);
            bodies[i].rotation = Quaternion.Lerp(bodies[i].rotation, bodies[i-1].rotation, lerpDamping * Time.deltaTime);
        }
        /*
        foreach (Transform body in bodies)
        {
            body.position = Vector3.Lerp(body.position, targetPos, 5 * Time.deltaTime);
            body.rotation = Quaternion.Lerp(body.rotation, targetAng, 5 * Time.deltaTime);

            targetPos = body.position;
            targetAng = body.rotation;
        }*/
    }
}
