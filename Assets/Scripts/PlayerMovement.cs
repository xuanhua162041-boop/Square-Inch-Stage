using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody rb;
    private Collider coll; // 建议用 CapsuleCollider
    private Animator anim;

    [Header("移动参数")]
    public float speed = 5f;
    public float jumpForce = 8f;

    [Header("检测参数")]
    public Transform groundCheck;
    public Transform deathCheck;
    public LayerMask Shadow;
    // 【修改点1】增大检测半径，防止动态网格抖动导致检测丢失
    public float groundCheckRadius = 0.25f;

    public bool isGround, isJump, isDeath;

    [Header("配置人物朝向")]
    public Vector3 initialScale = new Vector3(1, 1, 1);
    private float defaultXScale;

    bool jumpPressed;
    int jumpCount;

    private void Start()
    {
        isDeath = false;
        rb = GetComponent<Rigidbody>();
        coll = GetComponent<Collider>();
        anim = GetComponent<Animator>();

        transform.localScale = initialScale;
        defaultXScale = Mathf.Sign(initialScale.x);
    }

    private void Update()
    {
        // 输入放在 Update 是对的
        if (Input.GetButtonDown("Jump") && jumpCount > 0 && !isDeath)
        {
            jumpPressed = true;
        }
    }

    private void FixedUpdate()
    {
        // 如果已经死亡，不再进行物理运算
        if (isDeath) return;

        // --- 1. 地面检测 ---
        // 建议半径改为 0.2f (比原来的 0.1f 大一点)，防止动态影子抖动导致判定为离地
        isGround = Physics.CheckSphere(groundCheck.position, 0.2f, Shadow);

        // --- 2. 死亡检测 (核心修复) ---
        // 逻辑：检测“心脏”位置是否被影子填满。
        // 参数说明：
        // - 位置：deathCheck.position (必须在胸口中心)
        // - 半径：0.2f (必须比你的胶囊体半径小一圈，确保不碰到正常的墙壁和地面)
        // - 判定：只要这里面有东西 (CheckSphere返回true)，说明影子挤进身体了 -> 夹死
        if (Physics.CheckSphere(deathCheck.position, 0.2f, Shadow))
        {
            isDeath = true;
        }
        else
        {
            isDeath = false;
        }

        // --- 3. 状态与行为更新 ---
        anim.SetBool("isGround", isGround);

        GroundMovement();
        Jump();
        SwitchAnim();

        if (isDeath)
        {
            Death();
        }
    }
    void GroundMovement()
    {
        float horizontalMove = Input.GetAxisRaw("Horizontal");

        // 【修改点3】防止“钻地”现象
        // 直接修改 velocity.y 会覆盖物理引擎的“反推力”。
        // 当我们在地面上，且没有跳跃时，不要强制锁定 y 轴速度，让物理引擎处理支撑。

        Vector3 targetVelocity = new Vector3(horizontalMove * speed, rb.velocity.y, 0);
        rb.velocity = targetVelocity;

        // 【修改点4】安全的翻转朝向
        // 频繁翻转刚体的 Scale 可能会导致碰撞体重建从而穿模。
        // 如果只是 visuals (Sprite) 翻转，建议只翻转子物体 SpriteRenderer。
        // 如果必须翻转父物体，请确保翻转时没有和墙体发生重叠。
        if (horizontalMove != 0)
        {
            // 简单的防抖动处理
            float direction = Mathf.Sign(horizontalMove);
            transform.localScale = new Vector3(direction * defaultXScale, 1, 1);
        }
    }

    void Jump()
    {
        if (isGround)
        {
            // 只有当下降速度很小时才重置跳跃次数，防止刚起跳瞬间被重置
            if (rb.velocity.y <= 0.1f)
            {
                jumpCount = 2;
                isJump = false;
            }
        }

        if (jumpPressed)
        {
            // 执行跳跃
            rb.velocity = new Vector3(rb.velocity.x, jumpForce, 0); // 这里直接覆盖Y是可以的，因为是瞬间力

            // 只有落地状态下扣第一次，或者二段跳扣次数
            if (isGround || jumpCount > 0)
            {
                jumpCount--;
            }

            jumpPressed = false;
            isJump = true;
        }
    }

    void SwitchAnim()
    {
        anim.SetFloat("running", Mathf.Abs(rb.velocity.x));

        // 简单的状态机逻辑优化
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
        rb.velocity = Vector3.zero; // 死亡停止移动
        rb.isKinematic = true; // 停止物理运算
    }

    // 【新增】调试辅助：在 Scene 窗口画出检测范围
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}