using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody rb;
    private Collider coll;
    private Animator anim;

    public float speed, jumpForce;
    public Transform groundCheck;
    public LayerMask ground;

    public bool isGround, isJump;

    [Header("配置人物朝向")]
    public Vector3 initialScale = new Vector3(1,1,1);
    private float defaultXScale;//记录初始x轴缩放

    bool jumpPressed;
    int jumpCount;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        coll = GetComponent<Collider>();
        anim = GetComponent<Animator>();

        //运行时应用初始朝向
        transform.localScale = initialScale;
        defaultXScale = Mathf.Sign(initialScale.x);//Mathf.Sign 输出-1\ 1

    }
    private void OnValidate()//编辑器中实时生效
    {
        if (gameObject.activeInHierarchy)
        {
            transform.localScale = initialScale;
            defaultXScale = MathF.Sign(initialScale.x);
        }
    }



    private void Update()
    {
        if (Input.GetButtonDown("Jump") && jumpCount > 0)
        {
            jumpPressed = true;

        }
    }

    private void FixedUpdate()
    {
        isGround = (Physics.OverlapSphere(groundCheck.position, 0.1f, ground).Length > 0) ? true : false;
        anim.SetBool("isGround", isGround);
        GroundMovement();
        Jump();
        SwitchAnim();
    }
    void GroundMovement()
    {
        float horizontalMove = Input.GetAxisRaw("Horizontal");
        rb.velocity = new Vector3(horizontalMove * speed, rb.velocity.y, 0);
        if (horizontalMove != 0)//进行左右反转
        {
            transform.localScale = new Vector3(horizontalMove*defaultXScale, 1, 1);
        }
    
    }

    void Jump()
    {
        if (isGround)
        {
            jumpCount = 2;
            isJump = false;
        }
        if (jumpPressed && isGround)
        {
            isJump = true;
            rb.velocity = new Vector3(rb.velocity.x, jumpForce, 0);
            jumpCount--;
            jumpPressed = false;
        }
        else if (jumpPressed && jumpCount > 0 && isJump)
        {
            rb.velocity = new Vector3(rb.velocity.x, jumpForce, 0);
            jumpCount--;
            jumpPressed = false;

        }
    }

    void SwitchAnim()
    {
        anim.SetFloat("running", Mathf.Abs(rb.velocity.x));
        if (isGround)
        {
            anim.SetBool("falling", false);
        }
        else if (!isGround && rb.velocity.y > 0)
        {
            anim.SetBool("jumping", true);
        }
        else if (rb.velocity.y < 0)
        {
            anim.SetBool("jumping", false);
            anim.SetBool("falling", true);

        }
    }
}
