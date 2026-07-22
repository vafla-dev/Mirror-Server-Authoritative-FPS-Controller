//#define DEBUG_MOVEMENT

using Mirror;
using System;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class MirrorFPSController : NetworkBehaviour
{
    #region Data

    [Header("Data")]
    public Camera mainCamera;

    [Space, Header("NetSettings")]
    [SerializeField, SyncVar] private float speedMovement = 6;
    [SerializeField] private float maximumPermissibleDifferenceInPosition = 3;
    [SerializeField, Min(1)] private float maximumAllowableDifferenceInGravitationalAcceleration = 1;

    [Space, Header("Settings")]
    [SerializeField] private float sensitivity = 2;
    [SerializeField] public KeyCode useKey = KeyCode.E;
    [Space]
    [SerializeField] private LayerMask layerUse;
    [SerializeField] private float distanceUse = 4;

    [Space, Header("Physics")]
    [SerializeField] private float initialFallRate = -1;
    [SerializeField] private float accelerationOfGravity = 24.88f;
    [SerializeField] private float jumpForce = 7f;

#if DEBUG_MOVEMENT

    [Space, Header("Debug")]
    [SerializeField] private GameObject prefabPlayerDebug;
    [NonSerialized] private GameObject playerDebug;

    [SerializeField] private GameObject prefabPlayerDebugTarget;
    [NonSerialized] private GameObject playerDebugTarget;
    [Space]
    [SerializeField] private bool isDebug;

#endif


    private CharacterController characterController;

    private float rotX;
    private float rotY;

    private Vector3 corentPosForServer;
    private Vector3 targetForServer;
    private Vector2 rotServer;

    private float gravity = -1;

    #endregion

    #region MonoBehaviour

    private void Start()
    {
        if (!isLocalPlayer)
        {
            Destroy(mainCamera.gameObject);
        }

#if DEBUG_MOVEMENT

        if (isLocalPlayer && isDebug)
        {
            playerDebug = Instantiate(prefabPlayerDebug, transform.position, Quaternion.Euler(0, 0, 0));
            playerDebugTarget = Instantiate(prefabPlayerDebugTarget, transform.position, Quaternion.Euler(0, 0, 0));
        }

#endif

        if (!isLocalPlayer && !NetworkServer.active) return;

        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!isLocalPlayer && NetworkServer.active) MoveByServer();

        if (!isLocalPlayer) return;

        Vector3 motion = Vector3.zero;

        CheckUse();

        InputMove(ref motion);

        CalculateGravity(ref motion);

        Rotate();

        MoveByClient(motion);
    }

    #endregion

    #region Movement

    [Client]
    private void InputMove(ref Vector3 motion)
    {
        if (!CenMove()) return;

        motion = transform.right * Input.GetAxis("Horizontal") + transform.forward * Input.GetAxis("Vertical");

        if (Vector3.Distance(motion, Vector3.zero) > 1)
        {
            motion = motion.normalized;
        }

        motion = motion * Time.deltaTime * speedMovement;
    }

    [Client]
    private void MoveByClient(Vector3 motion)
    {
        if (motion.y == 0)
            motion.y = -0.01f;

        characterController.Move(motion);

        if (isServer)
            RpcSetPos(transform.position, 0);
        else
        {
            Vector3 vector = motion / Time.deltaTime / speedMovement;
            vector.y = motion.y;

            CmdSetMoveTarget(transform.position, transform.position + vector);
#if DEBUG_MOVEMENT
            playerDebugTarget.transform.position = transform.position + vector;
#endif
        }
    }

    private float smoothMovementOnServer;

    [Server]
    private void MoveByServer()
    {
        Vector3 derectional = targetForServer - transform.position;

        if (Vector3.Distance(derectional, Vector3.zero) > 1)
            derectional.Normalize();

        float to = 0.1f;

        if (Vector3.Distance(targetForServer, transform.position) > 0.01)
        {
            to = Vector3.Distance(derectional, Vector3.zero);
        }

        smoothMovementOnServer = Mathf.MoveTowards(smoothMovementOnServer, to, 3 * Time.deltaTime);

        derectional = derectional * speedMovement * smoothMovementOnServer * Time.deltaTime;

        if (Vector3.Distance(derectional, Vector3.zero) < Vector3.Distance(targetForServer - transform.position, Vector3.zero))
            P(transform.position,
                corentPosForServer,
                targetForServer,
                Vector3.Distance(derectional, Vector3.zero),
                ref derectional);

        CalculateGravityByServer(ref derectional);

        characterController.Move(derectional);

        RpcSetPos(transform.position, gravity);

        bool P(Vector3 point, Vector3 startPoint, Vector3 endPoint, float radius, ref Vector3 delta)
        {
            Vector3 M = startPoint - point;
            Vector3 N = endPoint - startPoint;

            float a = (N.x * N.x) + (N.y * N.y) + (N.z * N.z);
            float b = (2 * M.x * N.x) + (2 * M.y * N.y) + (2 * M.z * N.z);
            float c = (M.x * M.x) + (M.y * M.y) + (M.z * M.z) - radius * radius;

            //at² + bt + c = 0

            float d = b * b - 4 * a * c;

            if (d < 0)
                return false;

            d = Mathf.Sqrt(d);

            float[] x = new float[2];

            x[0] = (-b - d) / (2 * a);
            x[1] = (-b + d) / (2 * a);

            float t = -1;

            foreach (float value in x)
            {
                if (value >= 0 && value <= 1)
                    if (value > t)
                        t = value;
            }
            if (t >= 0 && t <= 1)
            {
                delta = startPoint + N * t - point;

                return true;
            }

            t = Vector3.Dot(point - startPoint, N) / N.sqrMagnitude;

            if (t >= 0 && t <= 1)
            {
                delta = startPoint + N * t - point;

                delta.Normalize();

                delta *= radius;

                return true;
            }

            return false;
        }
    }

    [Command]
    private void CmdSetMoveTarget(Vector3 corentPos, Vector3 target)
    {
        corentPosForServer = corentPos;
        targetForServer = target;
    }

    [ClientRpc]
    private void RpcSetPos(Vector3 pos, float gravity)
    {
        if (isLocalPlayer)
        {
#if DEBUG_MOVEMENT
            if (isDebug && playerDebug != null)
                playerDebug.transform.position = pos;
#endif

            Vector3 diff = pos - transform.position;

            bool thatIsCorrect = (diff.x * diff.x + diff.z * diff.z) <= (maximumPermissibleDifferenceInPosition * maximumPermissibleDifferenceInPosition);

            if (!NetworkServer.active)
                if ((this.gravity * gravity > 0
                    && (Mathf.Abs(this.gravity - gravity) > maximumAllowableDifferenceInGravitationalAcceleration + jumpForce))
                    || !thatIsCorrect)
                    this.gravity = gravity;

            if (thatIsCorrect) return;

            characterController.enabled = false;

            transform.position = pos;

            characterController.enabled = true;

            return;
        }
        if (characterController != null)
            characterController.enabled = false;

        transform.position = pos;

        if (characterController != null)
            characterController.enabled = true;
    }

    [ClientRpc]
    private void RpcSetPos(Vector3 pos)
    {
        if (characterController != null)
            characterController.enabled = false;

        transform.position = pos;

        if (characterController != null)
            characterController.enabled = true;
    }

    #endregion

    #region Rotation

    [Client]
    private void Rotate()
    {
        if (!CenRotate()) return;

        rotX += Input.GetAxis("Mouse X") * sensitivity;
        rotY -= Input.GetAxis("Mouse Y") * sensitivity;

        rotY = Mathf.Clamp(rotY, -85, 85);

        mainCamera.transform.localRotation = Quaternion.Euler(rotY, 0, 0);

        transform.rotation = Quaternion.Euler(0, rotX, 0);

        CmdSetRot(new Vector2(rotX, rotY));
    }

    [Command]
    private void CmdSetRot(Vector2 rot)
    {
        this.rotServer = rot;

        RpcSetRot(rot);
    }

    [ClientRpc]
    private void RpcSetRot(Vector2 rot)
    {
        if (isLocalPlayer)
        {
#if DEBUG_MOVEMENT
            if (!isDebug || playerDebug == null) return;

            playerDebug.transform.rotation = Quaternion.Euler(0, rot.x, 0);
#endif
            return;
        }

        transform.rotation = Quaternion.Euler(0, rot.x, 0);
    }

    #endregion

    #region Physics

    [Client]
    private void CalculateGravity(ref Vector3 vector)
    {
        if (!GlobalControler.Instance.roundHasStarted) return;

        if (characterController.isGrounded)
        {
            gravity = initialFallRate;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (!NetworkServer.active)
                    CmdJump();
                gravity = jumpForce;
            }

            vector.y = gravity * Time.deltaTime;

            return;
        }

        gravity -= accelerationOfGravity * Time.deltaTime;

        vector.y = gravity * Time.deltaTime;
    }

    private bool isJumping = false;

    [Server]
    private void CalculateGravityByServer(ref Vector3 vector)
    {
        if (!GlobalControler.Instance.roundHasStarted) return;

        if (characterController.isGrounded)
        {
            gravity = initialFallRate;

            if (isJumping)
            {
                gravity = jumpForce;

                isJumping = false;
            }

            vector.y = gravity * Time.deltaTime;

            return;
        }

        gravity -= accelerationOfGravity * Time.deltaTime;

        vector.y = gravity * Time.deltaTime;
    }

    [Command]
    private void CmdJump()
    {
        if (!GlobalControler.Instance.roundHasStarted) return;

        isJumping = true;
    }

    #endregion

    #region Use

    private void CheckUse()
    {
        if (!Input.GetKeyDown(useKey)) return;

        if (Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out RaycastHit hit, distanceUse, layerUse))
        {
            if (hit.transform.TryGetComponent<IInteractable>(out IInteractable used))
            {
                used.Use();
            }
        }
    }

    #endregion

    #region Methods

    public void Move(Vector3 vector)
    {
        if (!isLocalPlayer && !NetworkServer.active)
        {
            Debug.LogError("a client cannot move other clients");

            return;
        }

        NewPos(vector);

        if (NetworkServer.active)
        {
            RpcSetPos(vector);

            return;
        }

        void NewPos(Vector3 pos)
        {
            characterController.enabled = false;

            transform.position = pos;

            characterController.enabled = true;
        }
    }

    #endregion

    #region Cen

    /// <summary>
    /// Return false if player movement should be blocked.
    /// </summary>
    protected virtual bool CenMove()
    {
        return true;
    }

    /// <summary>
    /// Return false if camera rotation should be blocked.
    /// </summary>
    protected virtual bool CenRotate()
    {
        return true;
    }

    #endregion
}

public interface IInteractable
{
    void Use();
}
