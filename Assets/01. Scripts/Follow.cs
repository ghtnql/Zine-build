using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Follow : MonoBehaviour
{
    public Transform target;
    public float speedMove = 10;
    public float speedRot = 20;

    // Update is called once per frame
    void LateUpdate()
    {
        // 이동
        Vector3 pos = transform.position;
        Vector3 tarPos = target.position;
        pos = Vector3.Lerp(pos, tarPos, speedMove * Time.deltaTime); // Lerp : 포지션을 타겟포지션으로 speed만큼 이동
        //if (pos.y <= 10) pos.y = 10;
        transform.position = pos;

        // 회전
        Quaternion rot = transform.rotation;
        Quaternion tarRot = target.rotation;
        rot = Quaternion.Lerp(rot, tarRot, speedRot * Time.deltaTime); // Lerp : Linear Interpolation(선형 보간)의 준말
        //if (pos.y <= 10) rot.x = 0;
        transform.rotation = rot;
    }
}
