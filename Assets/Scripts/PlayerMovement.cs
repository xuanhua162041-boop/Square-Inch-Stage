using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("粒子拨片")]
    public GameObject ink;

    [Header("音效")]
    public AudioClip runAudioClip;
    public AudioClip jumpAudioClip;
    public AudioClip landAudioClip;

    [Header("移动参数")]
    public float maxMoveSpeed = 5f;
    public float groundAcceleration = 45f;
    public float groundDeceleration = 55f;
    public float airAcceleration = 22f;
    public float airDeceleration = 18f;

    [Header("跳跃参数")]
    public float jumpForce = 8f;
    public int extraAirJumps = 1;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;
    public float fallGravityMultiplier = 2.2f;
    public float jumpCutGravityMultiplier = 2.2f;
    public float maxFallSpeed = 20f;

    [Header("检测参数")]
    public Transform groundCheck;
    public Transform deathCheck;
    public LayerMask Shadow;
    public float groundCheckRadius = 0.25f;
    public float deathCheckRadius = 0.12f;
    public float deathConfirmTime = 0.08f;

    [Header("配置人物朝向")]
    public Vector3 initialScale = new Vector3(1, 1, 1);

    public bool isGround;
    public bool isJump;
    public bool isDeath;

    private Rigidbody rb;
    private Animator anim;
    private float defaultXScale;
    private bool moveBan = false;
    private bool jumpHeld;
    private bool jumpReleased;
    private float moveInput;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float deathContactTimer;
    private int airJumpsRemaining;
    private bool wasGrounded;

    private void Start()
    {
        MemoryItem.CollectMemoryAction += BanInput;
        MemoryCanvasUI.OnVideoEndAction += OpenInput;

        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        isDeath = false;

        transform.localScale = initialScale;
        defaultXScale = Mathf.Sign(initialScale.x);
        airJumpsRemaining = extraAirJumps;
    }

    private void OnDestroy()
    {
        MemoryItem.CollectMemoryAction -= BanInput;
        MemoryCanvasUI.OnVideoEndAction -= OpenInput;
    }

    private void Update()
    {
        if (isDeath) return;

        moveInput = moveBan ? 0f : Input.GetAxisRaw("Horizontal");
        jumpHeld = Input.GetButton("Jump");

        if (!moveBan && Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }

        if (Input.GetButtonUp("Jump"))
        {
            jumpReleased = true;
        }

        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        UpdateFacing();
    }

    private void FixedUpdate()
    {
        if (isDeath) return;

        UpdateGroundState();
        UpdateDeathState();

        if (isDeath)
        {
            Death();
            return;
        }

        ApplyHorizontalMovement();
        HandleJump();
        ApplyBetterGravity();
        SwitchAnim();
    }

    void UpdateGroundState()
    {
        wasGrounded = isGround;
        isGround = groundCheck != null && Physics.CheckSphere(groundCheck.position, groundCheckRadius, Shadow);
        anim.SetBool("isGround", isGround);

        if (isGround)
        {
            coyoteCounter = coyoteTime;
            airJumpsRemaining = extraAirJumps;
            isJump = false;

            if (!wasGrounded)
            {
                InkEmitter();
            }
        }
        else if (coyoteCounter > 0f)
        {
            coyoteCounter -= Time.fixedDeltaTime;
        }
    }

    void UpdateDeathState()
    {
        bool inDeathZone = deathCheck != null && Physics.CheckSphere(deathCheck.position, deathCheckRadius, Shadow);

        if (inDeathZone)
        {
            deathContactTimer += Time.fixedDeltaTime;
            isDeath = deathContactTimer >= deathConfirmTime;
        }
        else
        {
            deathContactTimer = 0f;
            isDeath = false;
        }
    }

    void ApplyHorizontalMovement()
    {
        if (moveBan)
        {
            moveInput = 0f;
        }

        float targetSpeed = moveInput * maxMoveSpeed;
        float accel = Mathf.Abs(targetSpeed) > 0.01f
            ? (isGround ? groundAcceleration : airAcceleration)
            : (isGround ? groundDeceleration : airDeceleration);

        float newX = Mathf.MoveTowards(rb.velocity.x, targetSpeed, accel * Time.fixedDeltaTime);
        rb.velocity = new Vector3(newX, rb.velocity.y, 0f);
    }

    void HandleJump()
    {
        if (moveBan) return;
        if (jumpBufferCounter <= 0f) return;

        if (coyoteCounter > 0f)
        {
            PerformJump();
            coyoteCounter = 0f;
            return;
        }

        if (!isGround && airJumpsRemaining > 0)
        {
            airJumpsRemaining--;
            PerformJump();
        }
    }

    void PerformJump()
    {
        jumpBufferCounter = 0f;
        jumpReleased = false;
        isJump = true;

        float jumpVelocity = Mathf.Max(rb.velocity.y, 0f);
        rb.velocity = new Vector3(rb.velocity.x, jumpVelocity, 0f);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
    }

    void ApplyBetterGravity()
    {
        if (isGround) return;

        float gravityMultiplier = 1f;
        if (rb.velocity.y < 0f)
        {
            gravityMultiplier = fallGravityMultiplier;
        }
        else if (!jumpHeld || jumpReleased)
        {
            gravityMultiplier = jumpCutGravityMultiplier;
        }

        Vector3 extraGravity = Physics.gravity * (gravityMultiplier - 1f);
        rb.velocity += extraGravity * Time.fixedDeltaTime;

        if (rb.velocity.y < -maxFallSpeed)
        {
            rb.velocity = new Vector3(rb.velocity.x, -maxFallSpeed, 0f);
        }

        jumpReleased = false;
    }

    void UpdateFacing()
    {
        if (Mathf.Abs(moveInput) < 0.01f) return;

        float direction = Mathf.Sign(moveInput);
        transform.localScale = new Vector3(direction * defaultXScale, initialScale.y, initialScale.z);
    }

    void SwitchAnim()
    {
        anim.SetFloat("running", Mathf.Abs(rb.velocity.x));

        if (rb.velocity.y > 0.1f && !isGround)
        {
            anim.SetBool("jumping", true);
            anim.SetBool("falling", false);
        }
        else if (rb.velocity.y < -0.1f && !isGround)
        {
            anim.SetBool("jumping", false);
            anim.SetBool("falling", true);
        }
        else if (isGround)
        {
            anim.SetBool("jumping", false);
            anim.SetBool("falling", false);
        }
    }

    public void Death()
    {
        Debug.Log("玩家已死亡");
        anim.SetBool("die", true);
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (deathCheck != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(deathCheck.position, deathCheckRadius);
        }
    }

    public enum SoundType
    {
        Run,
        Jump,
        Land
    }

    public void playerSoundSFX(SoundType soundType)
    {
        switch (soundType)
        {
            case SoundType.Run:
                AudioManager.Instance.PlayLoop(runAudioClip);
                break;
            case SoundType.Jump:
                AudioManager.Instance.StopLoop(runAudioClip);
                AudioManager.Instance.PlaySFX(jumpAudioClip);
                break;
            case SoundType.Land:
                AudioManager.Instance.StopLoop(runAudioClip);
                AudioManager.Instance.PlaySFX(landAudioClip);
                break;
        }
    }

    public void StopLoopSoundSFX()
    {
        AudioManager.Instance.StopLoop(runAudioClip);
    }

    public void BanInput(MemoryType item)
    {
        moveBan = true;
        Input.imeCompositionMode = IMECompositionMode.Off;
    }

    public void OpenInput()
    {
        moveBan = false;
        Input.imeCompositionMode = IMECompositionMode.On;
    }

    public void InkEmitter()
    {
        if (ink == null || groundCheck == null) return;

        GameObject obj = Instantiate(ink);
        obj.transform.localScale = transform.localScale * -1f;
        obj.transform.position = groundCheck.position;
    }
}
