using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class GrappleHook : MonoBehaviour, IInteractable
{
    public GrappleHook other;
    public LineRenderer laser;
    //public LineRenderer rope;
    public GrappleRope rope;
    public Transform hook;
    public Transform hookHome;
    public Transform head;
    public Transform shaft;

    [field: SerializeField]
    public Transform InteractFrom { get; set; }
    public bool IsInteracting { get; set; }

    [Space]
    public float turnSpeed = 5f;
    public LayerMask targetMask;
    //public Vector3 defaultRot = Vector3.up * 180;
    public Vector3 offset;
    public Vector3 aimOffset;

    public Material red;
    public Material green;

    bool IInteractable.FixedPosition => true;
    public Transform grabbedTarget { get; private set; }
    Vector3 grabbedPos;
    public bool grabbing { get; private set; }
    public bool targetOnHook { get; private set; }

    //readonly WaitForSeconds wait = new WaitForSeconds(1f);
    //readonly float wait = 1f;
    readonly float travel = 2f;

    public void OnInteract()
    {
        if (grabbing) return;
        IsInteracting = !IsInteracting;
    }

    private void LateUpdate()
    {
        if (grabbing && grabbedTarget != null)
        {
            Quaternion cam = Quaternion.LookRotation(head.position.DirectionTo(grabbedTarget.position + grabbedPos));
            //head.rotation = Quaternion.Slerp(head.rotation, cam * Quaternion.Euler(aimOffset), turnSpeed * Time.deltaTime);
            //shaft.rotation = Quaternion.Slerp(shaft.rotation, Quaternion.Euler(cam.eulerAngles.With(x: 0, z: 0) + aimOffset), turnSpeed * Time.deltaTime);
            head.rotation = Quaternion.Slerp(head.rotation, cam * Quaternion.Euler(aimOffset), turnSpeed * Time.deltaTime);
            Quaternion headRot = head.rotation;
            shaft.rotation = Quaternion.Euler(0, head.eulerAngles.y, 0);
            head.rotation = headRot; // Prevent springy
            laser.enabled = false;
        }
        else if (IsInteracting)
        {
            //Quaternion cam = Quaternion.LookRotation(FPSCamera.ViewDir);
            //shaft.rotation = Quaternion.Slerp(shaft.rotation, Quaternion.Euler(cam.eulerAngles.With(x: 0, z: 0) + aimOffset), turnSpeed * Time.deltaTime);
            //head.rotation = Quaternion.Slerp(head.rotation, cam * Quaternion.Euler(aimOffset), turnSpeed * Time.deltaTime);
            Quaternion desired = Quaternion.Euler(Mathf.Clamp(-FPSCamera.YRot, -10, 40), FPSCamera.Transform.eulerAngles.y + 180, 0);
            head.rotation = Quaternion.Slerp(head.rotation, desired, turnSpeed * Time.deltaTime);
            Quaternion headRot = head.rotation;
            shaft.rotation = Quaternion.Euler(0, head.eulerAngles.y, 0);
            head.rotation = headRot; // Prevent springy
            //head.localRotation = Quaternion.Euler(head.localEulerAngles.WithX(Mathf.Clamp(FPSCamera.YRot, -10, 40)));
            laser.enabled = true;
            laser.SetPosition(0, laser.transform.position);
            laser.SetPosition(1, laser.transform.forward * 1000 + laser.transform.position);
            TryShoot();
        }
        else
        {
            //head.rotation = Quaternion.Slerp(head.rotation, desired, turnSpeed * Time.deltaTime);
            head.localRotation = Quaternion.Slerp(head.localRotation,
                Quaternion.Euler(offset), turnSpeed * Time.deltaTime);
            Quaternion headRot = head.rotation;
            shaft.rotation = Quaternion.Euler(0, head.eulerAngles.y, 0);
            head.rotation = headRot; // Prevent springy

            //shaft.localRotation = Quaternion.Slerp(shaft.rotation,
            //    Quaternion.Euler(offset), turnSpeed * Time.deltaTime);

            //head.localRotation = Quaternion.Slerp(head.localRotation,
            //    Quaternion.Euler(offset), turnSpeed * Time.deltaTime);

            laser.enabled = false;
        }

        rope.show = grabbing && grabbedTarget != null;
        //rope.show = true;
        rope.start = rope.transform.position;
        rope.end = hook.position;
        rope.DrawRope();
        //rope.SetPosition(0, rope.transform.position);
        //rope.SetPosition(1, hook.position);
    }

    void TryShoot()
    {
        if (grabbing)
        {
            laser.sharedMaterial = green;
            return;
        }

        if (Physics.Raycast(laser.transform.position, laser.transform.forward, out RaycastHit hit, Mathf.Infinity, targetMask) && hit.transform != other.grabbedTarget)
        {
            laser.sharedMaterial = green;

            if (PlayerInputs.Primary && !grabbing)
            {
                grabbedTarget = hit.transform;
                grabbedPos = hit.transform.position.DirectionTo_NoNormalize(hit.point);
                StartCoroutine(GrabTarget());
                AudioManager.Play(new Audio("GrappleLaunch").SetPosition(transform.position).SetParent(transform));
            }
        }
        else
        {
            laser.sharedMaterial = red;
        }
    }

    IEnumerator GrabTarget()
    {
        //rope.Reset();
        grabbing = true;
        targetOnHook = false;
        IsInteracting = false;
        float time = 0f;

        while (time < travel)
        {
            time += Time.deltaTime;

            if (grabbedTarget != null)
            {
                hook.transform.position = Vector3.Lerp(hookHome.position,
                    grabbedTarget.position + grabbedPos, Remap.Float(time, 0, travel, 0, 1));
            }
            yield return null;
        }

        //yield return wait;


        Vector3 hookOffset = hook.position.DirectionTo_NoNormalize(grabbedTarget != null ? grabbedTarget.position : Vector3.zero);
        Vector3 grab = hook.position;
        targetOnHook = true;

        while (time > 0)
        {
            time -= Time.deltaTime;
            hook.position = Vector3.Lerp(hookHome.position,
                grab, Remap.Float(time, 0, travel, 0, 1));
            if (grabbedTarget != null)
            {
                grabbedTarget.position = hook.position + hookOffset;
                if (time < 0.2f)
                {
                    grabbedTarget.GetComponent<Cache>().OnReelIn();
                    grabbedTarget = null;
                }
            }
            yield return null;
        }

        grabbing = false;
        targetOnHook = false;
    }

    public float GetTurnAmount()
    {
        if (grabbedTarget == null || !targetOnHook)
            return 0f;
        //return Vector3.Dot(-Airship.instance.transform.forward, transform.position.DirectionTo(grabbedTarget.position));

        float dot = Vector3.Dot(-Airship.instance.transform.forward, transform.position.Flattened()
            .DirectionTo(grabbedTarget.position.Flattened()));
        float val;
        if (dot > 0)
            val = Remap.Float(dot, 1f, 0f, 0f, 0.5f);
        else
            val = Remap.Float(dot, 0f, -1f, 0.5f, 1f);

        float sideDot = Vector3.Dot(-Airship.instance.transform.right, transform.position.Flattened()
            .DirectionTo(grabbedTarget.position.Flattened()));

        return val * Mathf.Sign(sideDot);
    }
}