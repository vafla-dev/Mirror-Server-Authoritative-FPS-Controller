//#define DEBUG_MOVEMENT

using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace Mirror_Server_Authoritative_FPS_Controller
{
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class MirrorFPSController : NetworkBehaviour
    {
        #region Data

        #region Structures

        private struct PlayerSnapshot
        {
            public Vector3 position;
            public double netTime;

            public bool isGrounded;

            public PlayerSnapshot(Vector3 pos, bool isGrounded)
            {
                position = pos;

                netTime = NetworkTime.time;

                this.isGrounded = isGrounded;
            }

            public PlayerSnapshot(Vector3 pos, double netTime)
            {
                position = pos;

                this.netTime = netTime;

                isGrounded = false;
            }
        }

        #endregion

        [Header("Data")]
        public Transform rotMainCamera;
        public Transform interpolationCameraRot;

        [Space, Header("NetSettings")]
        [SerializeField, SyncVar] public float speedMovement = 6;
        [SerializeField, SyncVar] public float boostWhileRunning = 2f;
        [SerializeField, SyncVar] public bool noclip = false;
        [SerializeField, SyncVar] public float jumpForce = 7f;
        //[SerializeField] public float delayInterpolation = 3f;
        [SerializeField] private float maximumPermissibleDifference = 45;

        [Space, Header("Settings")]
        [SerializeField] private float inputSmoothing = 9;
        [SerializeField] protected float sensitivity = 2;
        [SerializeField] protected KeyCode sprintingKey = KeyCode.LeftShift;
        [SerializeField] protected KeyCode crouchKey = KeyCode.LeftControl;
        [SerializeField] protected KeyCode jumpingKey = KeyCode.Space;
        [SerializeField] protected KeyCode forwardKey = KeyCode.W;
        [SerializeField] protected KeyCode leftKey = KeyCode.A;
        [SerializeField] protected KeyCode beackKey = KeyCode.S;
        [SerializeField] protected KeyCode rightKey = KeyCode.D;

        [Space, Header("Physics")]
        [SerializeField] private float initialFallRate = -1;
        [SerializeField] private float accelerationOfGravity = 24.88f;


#if DEBUG_MOVEMENT

        [Space, Header("Debug")]
        [SerializeField] private GameObject prefabPlayerDebug;
        [NonSerialized] private GameObject playerDebug;

        [SerializeField] private GameObject prefabPlayerDebugTarget;
        [NonSerialized] private GameObject playerDebugTarget;

#endif

        public CharacterController characterController { get; private set; }

        private Character character;

        private Vector2 smoothing;

        private bool isGrounded = false;

        private float gravity = -1;

        #region Server

        private Dictionary<uint, NetPlayerPacket> netPlayerPackets = new Dictionary<uint, NetPlayerPacket>();
        private uint lastTick = 0;

        private Vector2 rotServer;

        /// <summary>
        /// Client can't use this method because hi has protected fields rotX end rotY
        /// </summary>
        public Vector2 RotServer
        {
            set
            {
                rotServer = value;

                RpcSetRot(value);
            }
            get
            {
                return rotServer;
            }
        }

        #endregion

        #region LocalPlayer

        private List<NetPlayerPacket> lastClientInput = new List<NetPlayerPacket>();

        protected float rotX;
        protected float rotY;

        private bool jump = false;


        private Vector3 lastPos;

        protected float noclipSpeed = 10;

        #endregion

        #region clients

        private PlayerSnapshot nowSnapshot;
        private PlayerSnapshot lastSnapshot;

        #endregion

        #endregion

        #region MonoBehaviour

        protected virtual void Start()
        {
            if (!isLocalPlayer)
            {
                Destroy(rotMainCamera.gameObject);
            }

#if DEBUG_MOVEMENT

            if (isLocalPlayer)
            {
                playerDebug = Instantiate(prefabPlayerDebug, transform.position, Quaternion.Euler(0, 0, 0));
                playerDebugTarget = Instantiate(prefabPlayerDebugTarget, transform.position, Quaternion.Euler(0, 0, 0));
            }

#endif

            character = GetComponent<Character>();

            if (!isLocalPlayer && !NetworkServer.active) return;

            characterController = GetComponent<CharacterController>();
        }

        protected virtual void Update()
        {
            if (!isLocalPlayer)
            {
                if (NetworkServer.active) return;

                double t = nowSnapshot.netTime - lastSnapshot.netTime;

                double netT = NetworkTime.time;//- delayInterpolation;

                double a = (netT - lastSnapshot.netTime) / t;

                if (characterController != null)
                    characterController.enabled = false;

                transform.position = Vector3.Lerp(lastSnapshot.position, nowSnapshot.position, Mathf.Clamp((float)a, 0.0001f, 1));

                if (characterController != null)
                    characterController.enabled = true;

                return;
            }

            if (Input.GetKeyDown(jumpingKey))
                jump = true;

            Rotate();
        }

        private uint currentTick = 0;

        /// <summary>
        /// One game tick
        /// </summary>
        private void FixedUpdate()
        {
            if (!NetworkServer.active && !isLocalPlayer) return;

            currentTick++;

            NetPlayerPacket packet = new NetPlayerPacket();

            lastPos = transform.position;

            if (isLocalPlayer)
            {
                packet = new NetPlayerPacket
                {
                    tick = currentTick,

                    w = Input.GetKey(forwardKey),
                    a = Input.GetKey(leftKey),
                    s = Input.GetKey(beackKey),
                    d = Input.GetKey(rightKey),

                    jumping = jump,

                    rotX = this.rotX,
                    rotY = this.rotY,
                };

            }
            else
            {
                if (netPlayerPackets.Count > 0)
                {
                    uint first = lastTick;

                    foreach (KeyValuePair<uint, NetPlayerPacket> pair in netPlayerPackets)
                    {
                        if (pair.Key < first)
                        {
                            first = pair.Key;
                        }
                    }


                    packet = netPlayerPackets[first];

                    netPlayerPackets.Remove(first);
                }
            }

            Move(packet);

            jump = false;
        }

        #endregion

        #region Movement

        private void Move(NetPlayerPacket packet)
        {
            if (!CanMove(!isLocalPlayer)) return;

            Vector2 targetSmoothing = new Vector2(Convert.ToInt32(packet.w) - Convert.ToInt32(packet.s), Convert.ToInt32(packet.a) - Convert.ToInt32(packet.d));

            smoothing = Vector2.MoveTowards(smoothing, targetSmoothing, Time.fixedDeltaTime * inputSmoothing);

            if (CanRotate(!isLocalPlayer))
            {
                rotServer = new Vector2(packet.rotX, packet.rotY);

                transform.rotation = Quaternion.Euler(0, packet.rotX, 0);
            }

            float y = CalculateGravity(packet);

            Vector3 lastPos = transform.position;

            if (characterController != null && characterController.enabled)
                characterController.Move(((transform.forward * smoothing.x - transform.right * smoothing.y).normalized + Vector3.up * y) * speedMovement * Time.fixedDeltaTime);

            isGrounded = characterController.isGrounded;

            if (NetworkServer.active)
            {
                Vector3 diff = transform.position - lastPos;

                RpcSetPos(new PlayerSnapshot(transform.position, isGrounded), new Vector2(diff.x, diff.z), packet.tick, false);
            }
            else
            {
                for (int i = 0; i < 2; i++)
                    CmdSendInput(packet, lastClientInput.ToArray());

                lastClientInput.Add(packet);

                if (lastClientInput.Count > 3)
                    lastClientInput.RemoveAt(0);
            }
        }

        [Command(channel = Channels.Unreliable)]
        private void CmdSendInput(NetPlayerPacket packet, NetPlayerPacket[] last)
        {
            if (packet.tick <= lastTick) return;

            if (netPlayerPackets.Count > 40)
            {
                lastTick = packet.tick;

                return;
            }

            netPlayerPackets.Add(packet.tick, packet);

            foreach (NetPlayerPacket packet1 in last)
            {
                if (packet1.tick <= lastTick) continue;

                if (!netPlayerPackets.ContainsKey(packet1.tick))
                {
                    netPlayerPackets.Add(packet1.tick, packet1);
                }
            }

            lastTick = packet.tick;
        }

        [ClientRpc]
        private void RpcSetPos(PlayerSnapshot snapshot, Vector2 vector, uint tick, bool immediately)
        {
            if (!NetworkClient.active) return;

            if (immediately)
            {
                if (characterController != null)
                    characterController.enabled = false;

                transform.position = snapshot.position;

                if (characterController != null)
                    characterController.enabled = true;


                if (!isLocalPlayer)
                {
                    lastSnapshot = snapshot;

                    nowSnapshot = snapshot;

                    if (character != null)
                    {
                        character.SetTarget(vector);
                        character.SetBoolIsGrounded(snapshot.isGrounded);
                    }
                }

                return;
            }

            if (!NetworkServer.active && isLocalPlayer)
            {
#if DEBUG_MOVEMENT

                if (playerDebug != null)
                    playerDebug.transform.position = snapshot.position;

#endif

                if (currentTick - tick < 40)
                    if (Vector3.Distance(snapshot.position, transform.position) > maximumPermissibleDifference)
                    {
                        if (characterController != null)
                            characterController.enabled = false;

                        transform.position = snapshot.position;

                        if (characterController != null)
                            characterController.enabled = true;
                    }

                return;
            }

            if (character != null)
            {
                Vector3 vector3 = transform.forward * vector.x + transform.right * vector.y;

                vector = new Vector2(vector3.x, vector3.z);
                character.SetTarget(vector);

                character.SetBoolIsGrounded(snapshot.isGrounded);
            }

            lastSnapshot = new PlayerSnapshot(transform.position, nowSnapshot.netTime);

            nowSnapshot = snapshot;
        }

        #endregion

        #region Rotation

        [Client]
        protected virtual void Rotate()
        {
            float alphaPos = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;

            Vector3 directional = lastPos - transform.position;

            interpolationCameraRot.position = lastPos - directional * alphaPos;

            if (!CanRotate())
            {
                rotMainCamera.localRotation = Quaternion.Euler(rotY, 0, 0);

                transform.rotation = Quaternion.Euler(0, rotX, 0);

                CmdSetRot(new Vector2(rotX, rotY));

                return;
            }

            float realSensitivity = sensitivity * SensitivityFactor();

            rotX += Input.GetAxis("Mouse X") * realSensitivity;
            rotY -= Input.GetAxis("Mouse Y") * realSensitivity;

            rotY = Mathf.Clamp(rotY, -90, 90);

            rotMainCamera.localRotation = Quaternion.Euler(rotY, 0, 0);

            transform.rotation = Quaternion.Euler(0, rotX, 0);

            CmdSetRot(new Vector2(rotX, rotY));
        }

        [Command]
        protected void CmdSetRot(Vector2 rot)
        {
            if (!CanRotate(true)) return;

            rot.y = Mathf.Clamp(rot.y, -90, 90);

            this.rotServer = rot;

            RpcSetRot(rot);

            if (!NetworkClient.active)
            {
                character?.SetRotHed(rot.y);

                transform.rotation = Quaternion.Euler(0, rot.x, 0);
            }
        }

        [ClientRpc]
        protected virtual void RpcSetRot(Vector2 rot)
        {
            character?.SetRotHed(rot.y);

            if (isLocalPlayer)
            {
#if DEBUG_MOVEMENT
                if (playerDebug == null) return;

                playerDebug.transform.rotation = Quaternion.Euler(0, rot.x, 0);
#endif
                return;
            }

            transform.rotation = Quaternion.Euler(0, rot.x, 0);
        }

        protected virtual float SensitivityFactor() => 1;

        #endregion

        #region Physics

        private float CalculateGravity(NetPlayerPacket packet)
        {
            if (!GravityCanBeCalculated(!isLocalPlayer)) return 0;

            float y = 0;

            if (isLocalPlayer)
                if (noclip)
                {
                    if (!CanMove()) return 0;

                    y += (Input.GetKey(jumpingKey)) ? 1 : 0;
                    y -= (Input.GetKey(KeyCode.C)) ? 1 : 0;

                    y *= Time.fixedDeltaTime * (noclipSpeed * (Input.GetKey(sprintingKey) ? 2 : 1));

                    return y;
                }

            if (isGrounded)
            {
                gravity = initialFallRate;

                if (packet.jumping && CanJump(!isLocalPlayer))
                {
                    gravity = jumpForce;

                    if (NetworkServer.active)
                        RpcPlayAnimJump();
                }

                y = gravity * Time.fixedDeltaTime;

                return y;
            }

            gravity -= accelerationOfGravity * Time.fixedDeltaTime;

            y = gravity * Time.fixedDeltaTime;

            return y;
        }

        [ClientRpc]
        private void RpcPlayAnimJump()
        {
            if (character == null) return;

            character.PlayAnumJump();
            character.SetBoolIsGrounded(false);
        }

        #endregion

        #region Cen

        /// <summary>
        /// Return false if player movement should be blocked.
        /// </summary>
        protected virtual bool CanMove(bool isServer = false)
        {
            return true;
        }

        /// <summary>
        /// Return false if camera rotation should be blocked.
        /// </summary>
        protected virtual bool CanRotate(bool isServer = false)
        {
            return true;
        }

        /// <summary>
        /// Return false if gravity calculations should be stopped.
        /// </summary>
        protected virtual bool GravityCanBeCalculated(bool isServer = false)
        {
            return true;
        }

        /// <summary>
        /// Return false if the ability to jump needs to be disabled.
        /// </summary>
        protected virtual bool CanJump(bool isServer = false)
        {
            return true;
        }

        #endregion

        #region Methods

        public void SetPosition(Vector3 position, bool notMandatory = false)
        {
            if (characterController != null)
                characterController.enabled = false;

            transform.position = position;

            if (characterController != null)
                characterController.enabled = true;

            if (!NetworkServer.active)
                return;

            if (notMandatory)
                RpcSetPos(new PlayerSnapshot(position, isGrounded), Vector2.zero, lastTick, false);
            else
                RpcSetPos(new PlayerSnapshot(position, 0), Vector2.zero, lastTick, true);
        }

        #endregion
    }

    internal struct NetPlayerPacket
    {
        public uint tick;

        public bool w, a, s, d;
        public bool jumping, sprinting, crouch;

        public float rotX;
        public float rotY;
    }
}
